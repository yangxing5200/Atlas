using Atlas.Core.Entities.Base;
using Atlas.Core.Entities.Interfaces;
using Atlas.Core.Enums;

namespace Atlas.Core.Entities.Tenant
{
    /// <summary>
    /// 用户实体
    /// 说明：
    /// 1. 使用 VersionedEntity 支持乐观锁（修改密码/权限时防并发）
    /// 2. 实现 ITenantEntity 支持多租户隔离
    /// 3. 实现 ISnowflakeId 使用分布式ID（用户可能需要跨系统同步）
    /// 4. 支持一个用户绑定多个门店（通过 UserStore 关联表）
    /// </summary>
    public class User : VersionedEntity, ITenantEntity, ISnowflakeId
    {
        #region 基础信息

        /// <summary>
        /// 租户ID
        /// </summary>
        public long TenantId { get; set; }

        /// <summary>
        /// 用户名（登录账号，租户内唯一）
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// 密码哈希（使用BCrypt/PBKDF2）
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// 真实姓名
        /// </summary>
        public string RealName { get; set; } = string.Empty;

        /// <summary>
        /// 昵称
        /// </summary>
        public string? NickName { get; set; }

        /// <summary>
        /// 手机号（可用于登录）
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// 邮箱（可用于登录）
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// 头像URL
        /// </summary>
        public string? Avatar { get; set; }

        /// <summary>
        /// 性别
        /// </summary>
        public Gender Gender { get; set; } = Gender.Unknown;

        #endregion

        #region 安全相关

        /// <summary>
        /// Token版本号（用于强制注销）
        /// 修改密码/强制登出时递增，使旧token失效
        /// </summary>
        public int TokenVersion { get; set; } = 1;

        /// <summary>
        /// 最后登录时间
        /// </summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// 最后登录IP
        /// </summary>
        public string? LastLoginIp { get; set; }

        /// <summary>
        /// 登录失败次数
        /// </summary>
        public int LoginFailedCount { get; set; } = 0;

        /// <summary>
        /// 账号锁定到期时间
        /// </summary>
        public DateTime? LockoutEndAt { get; set; }

        /// <summary>
        /// 密码过期时间（可选的密码策略）
        /// </summary>
        public DateTime? PasswordExpiresAt { get; set; }

        /// <summary>
        /// 是否需要修改密码（首次登录或管理员重置后）
        /// </summary>
        public bool MustChangePassword { get; set; } = false;

        #endregion

        #region 状态与权限

        /// <summary>
        /// 用户类型
        /// </summary>
        public UserType Type { get; set; } = UserType.Employee;

        /// <summary>
        /// 用户状态
        /// </summary>
        public UserStatus Status { get; set; } = UserStatus.Active;

        /// <summary>
        /// 是否激活（邮箱/手机验证）
        /// </summary>
        public bool IsActivated { get; set; } = true;

        /// <summary>
        /// 角色ID列表（逗号分隔，用于快速权限检查）
        /// 或者使用 UserRole 关联表（推荐）
        /// </summary>
        public string? RoleIds { get; set; }

        /// <summary>
        /// 默认门店ID（用户最常使用的门店）
        /// </summary>
        public long? DefaultStoreId { get; set; }

        #endregion

        #region 业务信息

        /// <summary>
        /// 员工编号（可选）
        /// </summary>
        public string? EmployeeNo { get; set; }

        /// <summary>
        /// 部门ID（可选）
        /// </summary>
        public long? DepartmentId { get; set; }

        /// <summary>
        /// 职位
        /// </summary>
        public string? Position { get; set; }

        /// <summary>
        /// 入职日期
        /// </summary>
        public DateTime? HireDate { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string? Remark { get; set; }

        /// <summary>
        /// 扩展字段（JSON格式，存储自定义信息）
        /// </summary>
        public string? ExtendedData { get; set; }

        #endregion

        #region 导航属性

        /// <summary>
        /// 默认门店
        /// </summary>
        public virtual Store? DefaultStore { get; set; }

        /// <summary>
        /// 用户-门店关联（一个用户可以管理多个门店）
        /// </summary>
        public virtual ICollection<UserStore> UserStores { get; set; } = new List<UserStore>();

        /// <summary>
        /// 用户-角色关联
        /// </summary>
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        /// <summary>
        /// 登录日志
        /// </summary>
        public virtual ICollection<UserLoginLog> LoginLogs { get; set; } = new List<UserLoginLog>();

        #endregion

        #region 辅助方法

        /// <summary>
        /// 是否被锁定
        /// </summary>
        public bool IsLockedOut()
        {
            return LockoutEndAt.HasValue && LockoutEndAt.Value > DateTime.UtcNow;
        }

        /// <summary>
        /// 密码是否过期
        /// </summary>
        public bool IsPasswordExpired()
        {
            return PasswordExpiresAt.HasValue && PasswordExpiresAt.Value < DateTime.UtcNow;
        }

        /// <summary>
        /// 是否可以登录
        /// </summary>
        public bool CanLogin()
        {
            return Status == UserStatus.Active
                   && IsActivated
                   && !IsDeleted
                   && !IsLockedOut();
        }

        /// <summary>
        /// 递增Token版本（使所有旧token失效）
        /// </summary>
        public void InvalidateAllTokens()
        {
            TokenVersion++;
        }

        /// <summary>
        /// 重置登录失败计数
        /// </summary>
        public void ResetLoginFailedCount()
        {
            LoginFailedCount = 0;
            LockoutEndAt = null;
        }

        /// <summary>
        /// 记录登录失败
        /// </summary>
        /// <param name="maxFailedAttempts">最大失败次数</param>
        /// <param name="lockoutMinutes">锁定时长（分钟）</param>
        public void RecordLoginFailure(int maxFailedAttempts = 5, int lockoutMinutes = 15)
        {
            LoginFailedCount++;

            if (LoginFailedCount >= maxFailedAttempts)
            {
                LockoutEndAt = DateTime.UtcNow.AddMinutes(lockoutMinutes);
            }
        }

        #endregion
    }

    #region 关联表

    /// <summary>
    /// 用户-门店关联表（多对多）
    /// 一个用户可以管理多个门店，一个门店可以有多个用户
    /// </summary>
    public class UserStore : BaseEntity, ITenantEntity
    {
        public long TenantId { get; set; }
        public long UserId { get; set; }
        public long StoreId { get; set; }

        /// <summary>
        /// 是否为主门店
        /// </summary>
        public bool IsPrimary { get; set; } = false;

        /// <summary>
        /// 权限范围（可选：Read, Write, Admin）
        /// </summary>
        public string? Permission { get; set; }

        /// <summary>
        /// 生效时间
        /// </summary>
        public DateTime? EffectiveFrom { get; set; }

        /// <summary>
        /// 失效时间
        /// </summary>
        public DateTime? EffectiveTo { get; set; }

        // 导航属性
        public virtual User User { get; set; } = null!;
        public virtual Store Store { get; set; } = null!;
    }

    /// <summary>
    /// 用户-角色关联表（多对多）
    /// </summary>
    public class UserRole : BaseEntity, ITenantEntity
    {
        public long TenantId { get; set; }
        public long UserId { get; set; }
        public long RoleId { get; set; }
        public long StoreId { get; set; }

        /// <summary>
        /// 授权时间
        /// </summary>
        public DateTime GrantedAt { get; set; }

        /// <summary>
        /// 授权人
        /// </summary>
        public long GrantedBy { get; set; }

        // 导航属性
        public virtual User User { get; set; } = null!;
        public virtual Role Role { get; set; } = null!;
    }

    /// <summary>
    /// 用户登录日志
    /// </summary>
    public class UserLoginLog : BaseEntity, ITenantEntity
    {
        public long TenantId { get; set; }
        public long UserId { get; set; }

        /// <summary>
        /// 会话ID（与Token中的SessionId对应）
        /// 登录成功时生成，失败时为NULL
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Token版本号
        /// </summary>
        public int TokenVersion { get; set; }

        /// <summary>
        /// 登录IP
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// User Agent
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// 设备类型（Mobile/Desktop/Tablet）
        /// </summary>
        public string? DeviceType { get; set; }

        /// <summary>
        /// 浏览器信息
        /// </summary>
        public string? Browser { get; set; }

        /// <summary>
        /// 操作系统
        /// </summary>
        public string? OperatingSystem { get; set; }

        /// <summary>
        /// 登录门店ID
        /// </summary>
        public long? StoreId { get; set; }

        /// <summary>
        /// 登录方式（Password/QRCode/SSO/SMS）
        /// </summary>
        public string LoginMethod { get; set; } = "Password";

        /// <summary>
        /// 是否登录成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 失败原因
        /// </summary>
        public string? FailureReason { get; set; }

        /// <summary>
        /// Token过期时间（登录成功时记录）
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// 登出时间（用户主动登出或Token过期）
        /// </summary>
        public DateTime? LogoutAt { get; set; }

        /// <summary>
        /// 登出方式（Manual手动/Expired过期/ForceLogout强制）
        /// </summary>
        public string? LogoutType { get; set; }

        // 导航属性
        public virtual User User { get; set; } = null!;
    }

    #endregion
}
