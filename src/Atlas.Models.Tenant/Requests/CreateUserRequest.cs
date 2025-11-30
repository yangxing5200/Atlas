using Atlas.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Atlas.Models.Requests
{
    /// <summary>
    /// 登录请求
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// 租户域名/代码
        /// </summary>
        [Required(ErrorMessage = "租户域名不能为空")]
        [StringLength(100, ErrorMessage = "租户域名长度不能超过100")]
        public string Domain { get; set; } = string.Empty;

        [Required(ErrorMessage = "用户名不能为空")]
        [StringLength(50, ErrorMessage = "用户名长度不能超过50")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "密码不能为空")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度为6-100位")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 登录门店ID（可选，不传则使用默认门店）
        /// </summary>
        public long? StoreId { get; set; }

        /// <summary>
        /// 记住我（延长Token有效期）
        /// </summary>
        public bool RememberMe { get; set; }
    }

    /// <summary>
    /// 创建用户请求
    /// </summary>
    public class CreateUserRequest
    {
        [Required(ErrorMessage = "用户名不能为空")]
        [StringLength(50, ErrorMessage = "用户名长度不能超过50")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "密码不能为空")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度为6-100位")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "真实姓名不能为空")]
        [StringLength(100, ErrorMessage = "姓名长度不能超过100")]
        public string RealName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? NickName { get; set; }

        [Phone(ErrorMessage = "手机号格式不正确")]
        [StringLength(20)]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        [StringLength(100)]
        public string? Email { get; set; }

        public Gender Gender { get; set; } = Gender.Unknown;

        [Required]
        public UserType Type { get; set; } = UserType.Employee;

        public long? DefaultStoreId { get; set; }

        [StringLength(50)]
        public string? EmployeeNo { get; set; }

        public long? DepartmentId { get; set; }

        [StringLength(100)]
        public string? Position { get; set; }

        public DateTime? HireDate { get; set; }

        [StringLength(1000)]
        public string? Remark { get; set; }

        /// <summary>
        /// 是否首次登录需要修改密码
        /// </summary>
        public bool MustChangePassword { get; set; } = true;

        /// <summary>
        /// 关联的门店ID列表
        /// </summary>
        public List<long>? StoreIds { get; set; }
    }

    /// <summary>
    /// 更新用户请求
    /// </summary>
    public class UpdateUserRequest
    {
        [Required]
        public long Id { get; set; }

        [Required(ErrorMessage = "真实姓名不能为空")]
        [StringLength(100)]
        public string RealName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? NickName { get; set; }

        [Phone(ErrorMessage = "手机号格式不正确")]
        [StringLength(20)]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        [StringLength(100)]
        public string? Email { get; set; }

        public Gender Gender { get; set; }

        public UserType Type { get; set; }

        public UserStatus Status { get; set; }

        public long? DefaultStoreId { get; set; }

        [StringLength(50)]
        public string? EmployeeNo { get; set; }

        public long? DepartmentId { get; set; }

        [StringLength(100)]
        public string? Position { get; set; }

        public DateTime? HireDate { get; set; }

        [StringLength(1000)]
        public string? Remark { get; set; }
    }

    /// <summary>
    /// 修改密码请求
    /// </summary>
    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "旧密码不能为空")]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "新密码不能为空")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度为6-100位")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "确认密码不能为空")]
        [Compare(nameof(NewPassword), ErrorMessage = "两次密码输入不一致")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// 重置密码请求（管理员操作）
    /// </summary>
    public class ResetPasswordRequest
    {
        [Required]
        public long UserId { get; set; }

        [Required(ErrorMessage = "新密码不能为空")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度为6-100位")]
        public string NewPassword { get; set; } = string.Empty;

        /// <summary>
        /// 是否要求用户首次登录修改密码
        /// </summary>
        public bool MustChangePassword { get; set; } = true;
    }

    /// <summary>
    /// 分配门店请求
    /// </summary>
    public class AssignStoresRequest
    {
        [Required]
        public long UserId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "至少分配一个门店")]
        public List<AssignStoreItem> Stores { get; set; } = new();
    }

    public class AssignStoreItem
    {
        [Required]
        public long StoreId { get; set; }

        public bool IsPrimary { get; set; }

        [StringLength(50)]
        public string? Permission { get; set; }

        public DateTime? EffectiveFrom { get; set; }

        public DateTime? EffectiveTo { get; set; }
    }

    /// <summary>
    /// 用户查询请求
    /// </summary>
    public class UserQueryRequest
    {
        /// <summary>
        /// 关键字（用户名/姓名/手机号）
        /// </summary>
        public string? Keyword { get; set; }

        /// <summary>
        /// 用户类型
        /// </summary>
        public UserType? Type { get; set; }

        /// <summary>
        /// 用户状态
        /// </summary>
        public UserStatus? Status { get; set; }

        /// <summary>
        /// 门店ID
        /// </summary>
        public long? StoreId { get; set; }

        /// <summary>
        /// 部门ID
        /// </summary>
        public long? DepartmentId { get; set; }

        /// <summary>
        /// 是否激活
        /// </summary>
        public bool? IsActivated { get; set; }

        /// <summary>
        /// 页码
        /// </summary>
        [Range(1, int.MaxValue)]
        public int PageIndex { get; set; } = 1;

        /// <summary>
        /// 每页数量
        /// </summary>
        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// 登录日志查询请求
    /// </summary>
    public class LoginLogQueryRequest
    {
        public long? UserId { get; set; }

        public string? IpAddress { get; set; }

        public bool? IsSuccess { get; set; }

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        [Range(1, int.MaxValue)]
        public int PageIndex { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }
}