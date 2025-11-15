using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Atlas.Infrastructure.Security
{
    /// <summary>
    /// 自定义Token认证选项
    /// </summary>
    public class CustomTokenAuthenticationOptions : AuthenticationSchemeOptions
    {
        public string TokenHeaderName { get; set; } = "Authorization";
        public string TokenPrefix { get; set; } = "Bearer";
        public bool EnableQueryStringToken { get; set; } = true; // 支持从URL获取token
        public bool EnableCustomHeader { get; set; } = true; // 支持自定义header
    }
    /// <summary>
    /// 高性能自定义Token认证处理器
    /// </summary>
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

        /// <summary>
        /// 认证处理
        /// </summary>
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // 1. 提取Token
            var token = ExtractToken();
            if (string.IsNullOrEmpty(token))
            {
                return AuthenticateResult.NoResult();
            }
            try
            {
                // 2. 异步验证Token
                var tokenInfo = await _tokenService.ValidateTokenAsync(token);
                if (tokenInfo == null)
                {
                    return AuthenticateResult.Fail("Invalid or expired token");
                }

                // 3. 创建Claims（最小化claims数量以提升性能）
                var claims = new[]
                {
                    new Claim("uid", tokenInfo.UserId.ToString()),
                    new Claim("sid", tokenInfo.StoreId.ToString()),
                    new Claim("tid", tokenInfo.TenantId.ToString()),
                    new Claim("uname", tokenInfo.UserId.ToString()), 
                    new Claim("token", token),
                };

                // 添加额外信息（如果有）
                if (!string.IsNullOrEmpty(tokenInfo.Extra))
                {
                    claims = claims.Append(new Claim("ext", tokenInfo.Extra)).ToArray();
                }

                // 4. 创建身份和票据
                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                return AuthenticateResult.Success(ticket);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Authentication failed");
                return AuthenticateResult.Fail($"Authentication error: {ex.Message}");
            }
        }

        /// <summary>
        /// 高效提取Token（避免多次字符串操作）
        /// </summary>
        private string? ExtractToken()
        {
            string? token = null;

            // 1. 优先从Authorization header获取
            if (Request.Headers.TryGetValue(Options.TokenHeaderName, out var authHeader))
            {
                var authHeaderString = authHeader.FirstOrDefault(); // 获取第一个值
                if (!string.IsNullOrEmpty(authHeaderString) &&
                    authHeaderString.StartsWith(Options.TokenPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    // 安全地提取token，处理各种格式（Bearer token, Bearer: token等）
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

            // 2. 从Cookie获取
            if (string.IsNullOrEmpty(token))
            {
                if (Request.Cookies.TryGetValue("lovelypets-auth-token", out var cookieToken))
                {
                    if (!string.IsNullOrWhiteSpace(cookieToken))
                    {
                        token = cookieToken;
                    }
                }
            }

            // 3. 从查询字符串获取
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

            // 4. 从自定义header获取
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

        /// <summary>
        /// 处理401挑战
        /// </summary>
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    || Request.Headers["Accept"].Any(v => v.Contains("application/json"));

            if (isAjax)
            {
                Response.StatusCode = 401;
                Response.ContentType = "application/json";
                return Response.WriteAsync("{\"code\":401,\"message\":\"Token expired\"}");
            }
            else
            {
                Response.StatusCode = 302;
                Response.Headers["Location"] = "/login";
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// 处理403禁止访问
        /// </summary>
        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 403;
            return Task.CompletedTask;
        }
    }
}