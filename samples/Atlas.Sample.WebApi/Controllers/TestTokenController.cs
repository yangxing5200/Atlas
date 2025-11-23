using Atlas.Core.Services;
using Atlas.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Atlas.Sample.WebApi.Controllers
{
    /// <summary>
    /// 测试工具 - 仅用于开发/测试环境
    /// </summary>
    [ApiController]
    [Route("api/test")]
    public class TestTokenController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _configuration;

        public TestTokenController(ITokenService tokenService, IConfiguration configuration)
        {
            _tokenService = tokenService;
            _configuration = configuration;
        }

        /// <summary>
        /// 生成测试Token
        /// </summary>
        /// <remarks>
        /// ⚠️ 仅用于开发/测试环境，生产环境请禁用此接口
        /// 
        /// 使用示例:
        /// ```json
        /// {
        ///   "tenantId": 1001,
        ///   "storeId": 2001,
        ///   "userId": 3001,
        ///   "userName": "test_user",
        ///   "extra": "role=admin",
        ///   "expirationMinutes": 60
        /// }
        /// ```
        /// </remarks>
        [HttpPost("generate-token")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(GenerateTokenResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesResponseType(typeof(ErrorResponse), 403)]
        public IActionResult GenerateToken([FromBody] GenerateTokenRequest request)
        {
            // ✅ 安全检查：仅允许非生产环境调用
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
            if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, new ErrorResponse
                {
                    Code = 403,
                    Message = "此接口仅在开发/测试环境可用"
                });
            }

            try
            {
                // 创建测试用户身份
                var testIdentity = new TestIdentity
                {
                    TenantId = request.TenantId,
                    StoreId = request.StoreId,
                    UserId = request.UserId,
                    UserName = request.UserName
                };

                // 生成Token
                var token = _tokenService.GenerateToken(
                    testIdentity
                );

                // 计算过期时间
                var expiresAt = DateTime.UtcNow.AddMinutes(request.ExpirationMinutes ?? 60);

                return Ok(new GenerateTokenResponse
                {
                    Success = true,
                    Token = token,
                    ExpiresAt = expiresAt,
                    ExpiresIn = (int)(expiresAt - DateTime.UtcNow).TotalSeconds,
                    TokenInfo = new TokenInfoDto
                    {
                        TenantId = request.TenantId,
                        StoreId = request.StoreId,
                        UserId = request.UserId,
                        UserName = request.UserName,
                        Extra = request.Extra
                    },
                    Usage = new UsageInfo
                    {
                        AuthorizationHeader = $"Bearer {token}",
                        CookieName = "lovelypets-auth-token",
                        CookieValue = token,
                        QueryString = $"?access_token={token}",
                        CustomHeader = new Dictionary<string, string>
                        {
                            { "X-Access-Token", token }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ErrorResponse
                {
                    Code = 400,
                    Message = "Token生成失败",
                    Detail = ex.Message
                });
            }
        }

        /// <summary>
        /// 验证Token
        /// </summary>
        [HttpPost("validate-token")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ValidateTokenResponse), 200)]
        public async Task<IActionResult> ValidateToken([FromBody] ValidateTokenRequest request)
        {
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
            if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, new ErrorResponse
                {
                    Code = 403,
                    Message = "此接口仅在开发/测试环境可用"
                });
            }

            var tokenInfo = await _tokenService.ValidateTokenAsync(request.Token);

            if (tokenInfo == null)
            {
                return Ok(new ValidateTokenResponse
                {
                    IsValid = false,
                    Message = "Token无效或已过期"
                });
            }

            return Ok(new ValidateTokenResponse
            {
                IsValid = true,
                Message = "Token有效",
                TokenInfo = new TokenInfoDto
                {
                    TenantId = tokenInfo.TenantId,
                    StoreId = tokenInfo.StoreId,
                    UserId = tokenInfo.UserId,
                    UserName = tokenInfo.UserName,
                },
                ExpiresAt = tokenInfo.GetExpiryDateTime(),
                RemainingSeconds = (int)(tokenInfo.ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            });
        }

        /// <summary>
        /// 批量生成多个用户的Token
        /// </summary>
        [HttpPost("generate-batch")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(BatchTokenResponse), 200)]
        public IActionResult GenerateBatchTokens([FromBody] BatchTokenRequest request)
        {
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
            if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, new ErrorResponse
                {
                    Code = 403,
                    Message = "此接口仅在开发/测试环境可用"
                });
            }

            var tokens = new List<TokenItem>();

            foreach (var user in request.Users)
            {
                try
                {
                    var testIdentity = new TestIdentity
                    {
                        TenantId = user.TenantId,
                        StoreId = user.StoreId,
                        UserId = user.UserId,
                        UserName = user.UserName
                    };

                    var token = _tokenService.GenerateToken(testIdentity);

                    tokens.Add(new TokenItem
                    {
                        UserId = user.UserId,
                        UserName = user.UserName,
                        Token = token,
                        Success = true
                    });
                }
                catch (Exception ex)
                {
                    tokens.Add(new TokenItem
                    {
                        UserId = user.UserId,
                        UserName = user.UserName,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            return Ok(new BatchTokenResponse
            {
                Total = tokens.Count,
                Succeeded = tokens.Count(t => t.Success),
                Failed = tokens.Count(t => !t.Success),
                Tokens = tokens
            });
        }
    }

    #region DTOs

    /// <summary>
    /// 生成Token请求
    /// </summary>
    public class GenerateTokenRequest
    {
        /// <summary>
        /// 租户ID
        /// </summary>
        [Required]
        [Range(1, long.MaxValue, ErrorMessage = "TenantId必须大于0")]
        public long TenantId { get; set; }

        /// <summary>
        /// 店铺ID
        /// </summary>
        [Required]
        [Range(1, long.MaxValue, ErrorMessage = "StoreId必须大于0")]
        public long StoreId { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        [Required]
        [Range(1, long.MaxValue, ErrorMessage = "UserId必须大于0")]
        public long UserId { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// 额外信息(可选)
        /// </summary>
        [StringLength(500)]
        public string? Extra { get; set; }

        /// <summary>
        /// 过期时间(分钟)，默认60分钟
        /// </summary>
        [Range(1, 43200)] // 最大30天
        public int? ExpirationMinutes { get; set; } = 60;
    }

    /// <summary>
    /// 生成Token响应
    /// </summary>
    public class GenerateTokenResponse
    {
        public bool Success { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int ExpiresIn { get; set; }
        public TokenInfoDto TokenInfo { get; set; } = new();
        public UsageInfo Usage { get; set; } = new();
    }

    /// <summary>
    /// Token使用说明
    /// </summary>
    public class UsageInfo
    {
        /// <summary>
        /// Authorization Header示例
        /// </summary>
        public string AuthorizationHeader { get; set; } = string.Empty;

        /// <summary>
        /// Cookie名称
        /// </summary>
        public string CookieName { get; set; } = string.Empty;

        /// <summary>
        /// Cookie值
        /// </summary>
        public string CookieValue { get; set; } = string.Empty;

        /// <summary>
        /// Query String示例
        /// </summary>
        public string QueryString { get; set; } = string.Empty;

        /// <summary>
        /// 自定义Header
        /// </summary>
        public Dictionary<string, string> CustomHeader { get; set; } = new();
    }

    public class TokenInfoDto
    {
        public long TenantId { get; set; }
        public long StoreId { get; set; }
        public long UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Extra { get; set; }
    }

    public class ValidateTokenRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }

    public class ValidateTokenResponse
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public TokenInfoDto? TokenInfo { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int? RemainingSeconds { get; set; }
    }

    public class BatchTokenRequest
    {
        [Required]
        [MinLength(1)]
        [MaxLength(100)]
        public List<GenerateTokenRequest> Users { get; set; } = new();
    }

    public class BatchTokenResponse
    {
        public int Total { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public List<TokenItem> Tokens { get; set; } = new();
    }

    public class TokenItem
    {
        public long UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? Error { get; set; }
    }

    public class ErrorResponse
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Detail { get; set; }
    }

    #endregion

    #region 测试用身份实现

    /// <summary>
    /// 测试用的身份实现
    /// </summary>
    internal class TestIdentity : ICurrentIdentity
    {
        public long? TenantId { get; set; }
        public long? StoreId { get; set; }
        public long? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Extra { get; set; }
        public string? SessionId { get; set; }
        public bool IsAuthenticated { get; set; }
    }

    #endregion
}

