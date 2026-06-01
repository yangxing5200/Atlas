using Atlas.Core.DataMasking;
using Atlas.Core.Enums;

namespace Atlas.Models.DTOs
{
    /// <summary>
    /// 用户DTO
    /// </summary>
    public class UserDto
    {
        public long Id { get; set; }
        public long TenantId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string RealName { get; set; } = string.Empty;
        public string? NickName { get; set; }
        [SensitiveData(MaskKind.Phone)]
        public string? Phone { get; set; }

        [SensitiveData(MaskKind.Email)]
        public string? Email { get; set; }
        public string? Avatar { get; set; }
        public Gender Gender { get; set; }
        public UserType Type { get; set; }
        public UserStatus Status { get; set; }
        public bool IsActivated { get; set; }
        public long? DefaultStoreId { get; set; }
        public string? DefaultStoreName { get; set; }
        public string? EmployeeNo { get; set; }
        public string? Position { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// 用户简要信息DTO
    /// </summary>
    public class UserSimpleDto
    {
        public long Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string RealName { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public UserType Type { get; set; }
    }

    /// <summary>
    /// 用户详细信息DTO
    /// </summary>
    public class UserDetailDto : UserDto
    {
        public long? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public DateTime? HireDate { get; set; }
        public string? Remark { get; set; }
        public List<UserStoreDto> UserStores { get; set; } = new();
        public List<string> Roles { get; set; } = new();
        public int TokenVersion { get; set; }
        public int LoginFailedCount { get; set; }
        public DateTime? LockoutEndAt { get; set; }
        public bool MustChangePassword { get; set; }
    }

    /// <summary>
    /// 用户门店关联DTO
    /// </summary>
    public class UserStoreDto
    {
        public long StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public string? Permission { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }

    /// <summary>
    /// 登录日志DTO
    /// </summary>
    public class UserLoginLogDto
    {
        public long Id { get; set; }
        [SensitiveData(MaskKind.Token)]
        public string? SessionId { get; set; }

        [SensitiveData(MaskKind.IpAddress)]
        public string IpAddress { get; set; } = string.Empty;
        public string? DeviceType { get; set; }
        public string? Browser { get; set; }
        public string? OperatingSystem { get; set; }
        public string LoginMethod { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? FailureReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LogoutAt { get; set; }
        public string? LogoutType { get; set; }
    }
}