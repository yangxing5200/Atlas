using Atlas.Models.DTOs;

namespace Atlas.Models.Responses
{
    /// <summary>
    /// 切换门店响应
    /// </summary>
    public class SwitchStoreResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// 切换后的当前门店
        /// </summary>
        public StoreInfoDto? CurrentStore { get; set; }
    }
}
