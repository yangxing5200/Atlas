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
    public class CustomTokenAuthenticationOptions : AuthenticationSchemeOptions
    {
        public string TokenHeaderName { get; set; } = "Authorization";
        public string TokenPrefix { get; set; } = "Bearer";
        public bool EnableQueryStringToken { get; set; } = true;
        public bool EnableCustomHeader { get; set; } = true;
        public string CookieName { get; set; } = "atlas-auth-token";
        public string LoginPath { get; set; } = "/login"; // ✅ 可配置的登录路径
    }

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

                // 创建Claims
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, tokenInfo.UserId.ToString()),
                    new Claim("uid", tokenInfo.UserId.ToString()),
                    new Claim("sid", tokenInfo.StoreId.ToString()),
                    new Claim("tid", tokenInfo.TenantId.ToString()),
                    new Claim("uname", tokenInfo.UserName ?? tokenInfo.UserId.ToString()),
                    new Claim("token", token),
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

            // 1. Authorization header
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

            // 2. ✅ Cookie（使用配置的名称）
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

            // 3. Query string
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

            // 4. Custom header
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
            // ✅ 改进：检测请求类型
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
                // ✅ 使用配置的登录路径
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