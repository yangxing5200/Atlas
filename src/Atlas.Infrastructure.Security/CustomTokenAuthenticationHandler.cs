using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Net.Http.Headers;

namespace Atlas.Infrastructure.Security
{
    /// <summary>
    /// 自定义 Token 认证方案配置。
    /// </summary>
    public class CustomTokenAuthenticationOptions : AuthenticationSchemeOptions
    {
        public string TokenHeaderName { get; set; } = "Authorization";
        public string TokenPrefix { get; set; } = "Bearer";
        public bool EnableQueryStringToken { get; set; }
        public bool EnableCustomHeader { get; set; } = true;
        public string CookieName { get; set; } = "atlas-auth-token";
        public string LoginPath { get; set; } = "/login";
    }

    /// <summary>
    /// ASP.NET Core 认证处理器，将 Atlas Token 转换为 ClaimsPrincipal。
    /// </summary>
    /// <remarks>
    /// 这里只做凭据提取和基础验证；TokenVersion 的数据库兜底校验由后续中间件完成。
    /// </remarks>
    public sealed class CustomTokenAuthenticationHandler : AuthenticationHandler<CustomTokenAuthenticationOptions>
    {
        private readonly ITokenService _tokenService;

        public CustomTokenAuthenticationHandler(
            IOptionsMonitor<CustomTokenAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            ITokenService tokenService)
            : base(options, logger, encoder, clock)
        {
            _tokenService = tokenService;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var token = ExtractToken();
            if (string.IsNullOrEmpty(token))
            {
                return AuthenticateResult.NoResult();
            }

            try
            {
                var tokenInfo = await _tokenService.ValidateTokenAsync(token);
                if (tokenInfo == null)
                {
                    Logger.LogDebug("Token validation failed");
                    return AuthenticateResult.Fail("Invalid or expired token");
                }

                // 保留 Atlas 内部 claim 名称，后续租户上下文和版本校验中间件依赖这些字段。
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, tokenInfo.UserId.ToString()),
                    new Claim("uid", tokenInfo.UserId.ToString()),
                    new Claim("sid", tokenInfo.StoreId.ToString()),
                    new Claim("tid", tokenInfo.TenantId.ToString()),
                    new Claim("uname", tokenInfo.UserName ?? tokenInfo.UserId.ToString()),
                    new Claim("token", token),
                    new Claim("token_version", tokenInfo.TokenVersion.ToString()), // ✅ 新增
                    new Claim("session_id", tokenInfo.SessionId ?? "") // ✅ 新增
                };

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                Logger.LogDebug("Authentication successful for user {UserId}", tokenInfo.UserId);
                return AuthenticateResult.Success(ticket);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Authentication error");
                return AuthenticateResult.Fail($"Authentication error: {ex.Message}");
            }
        }

        private string? ExtractToken()
        {
            string? token = null;

            // 读取顺序体现安全优先级：标准 Authorization 头优先，其次 Cookie，再按配置允许低优先级来源。
            if (Request.Headers.TryGetValue(Options.TokenHeaderName, out var authHeader))
            {
                var authHeaderString = authHeader.FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeaderString) &&
                    authHeaderString.StartsWith(Options.TokenPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var prefixLength = Options.TokenPrefix.Length;
                    if (authHeaderString.Length > prefixLength)
                    {
                        var remainder = authHeaderString.Substring(prefixLength).TrimStart(' ', ':', '\t');
                        if (!string.IsNullOrWhiteSpace(remainder))
                        {
                            token = remainder;
                        }
                    }
                }
            }

            // Cookie 适合浏览器场景，仍复用同一套 Token 验证逻辑。
            if (string.IsNullOrEmpty(token))
            {
                if (Request.Cookies.TryGetValue(Options.CookieName, out var cookieToken))
                {
                    if (!string.IsNullOrWhiteSpace(cookieToken))
                    {
                        token = cookieToken;
                    }
                }
            }

            // Query string 默认关闭，通常只用于 WebSocket/SSE 等无法设置请求头的场景。
            if (string.IsNullOrEmpty(token) && Options.EnableQueryStringToken)
            {
                if (Request.Query.TryGetValue("access_token", out var queryToken))
                {
                    var value = queryToken.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        token = value;
                    }
                }
            }

            // 自定义头用于兼容旧客户端或网关转发场景。
            if (string.IsNullOrEmpty(token) && Options.EnableCustomHeader)
            {
                if (Request.Headers.TryGetValue("X-Access-Token", out var customHeader))
                {
                    var value = customHeader.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        token = value;
                    }
                }
            }

            return token;
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            var isAjaxOrApi = Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                || Request.Headers[HeaderNames.Accept].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
                || Request.Path.StartsWithSegments("/api");

            if (isAjaxOrApi)
            {
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                Response.ContentType = "application/json; charset=utf-8";
                return Response.WriteAsync("{\"code\":401,\"message\":\"Unauthorized - Token missing or expired\"}");
            }
            else
            {
                Response.StatusCode = StatusCodes.Status302Found;
                Response.Headers[HeaderNames.Location] = Options.LoginPath;
                return Task.CompletedTask;
            }
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            var isAjaxOrApi = Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                || Request.Headers[HeaderNames.Accept].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
                || Request.Path.StartsWithSegments("/api");

            Response.StatusCode = StatusCodes.Status403Forbidden;

            if (isAjaxOrApi)
            {
                Response.ContentType = "application/json; charset=utf-8";
                return Response.WriteAsync("{\"code\":403,\"message\":\"Forbidden - Insufficient permissions\"}");
            }

            return Task.CompletedTask;
        }
    }
}
