using System.ComponentModel.DataAnnotations;

namespace Atlas.Models.Requests
{
    /// <summary>
    /// 切换门店请求
    /// </summary>
    public class SwitchStoreRequest
    {
        [Required(ErrorMessage = "门店ID不能为空")]
        public long StoreId { get; set; }
    }
}