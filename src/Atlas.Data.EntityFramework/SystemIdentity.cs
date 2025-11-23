using Atlas.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Common
{


    /// <summary>
    /// 系统身份标识
    /// 用于非用户请求场景（如：后台任务、数据迁移、系统初始化等）
    /// </summary>
    public sealed class SystemIdentity : ICurrentIdentity
    {
        #region 常量定义

        /// <summary>
        /// 系统用户ID
        /// </summary>
        private const long SYSTEM_USER_ID = -1;

        /// <summary>
        /// 数据迁移用户ID
        /// </summary>
        private const long MIGRATION_USER_ID = -2;

        /// <summary>
        /// 系统初始化用户ID
        /// </summary>
        private const long SEED_USER_ID = -3;

        /// <summary>
        /// 后台任务用户ID
        /// </summary>
        private const long BACKGROUND_JOB_USER_ID = -4;

        /// <summary>
        /// 定时任务用户ID
        /// </summary>
        private const long SCHEDULED_TASK_USER_ID = -5;

        /// <summary>
        /// 消息队列用户ID
        /// </summary>
        private const long MESSAGE_QUEUE_USER_ID = -6;

        #endregion

        #region 属性
        public string? SessionId { get; set; }
        /// <summary>
        /// 用户ID
        /// </summary>
        public long? UserId { get; private set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; private set; }

        /// <summary>
        /// 门店ID
        /// </summary>
        public long? StoreId { get; private set; }

        /// <summary>
        /// 租户ID
        /// </summary>
        public long? TenantId { get; private set; }

        /// <summary>
        /// 是否已认证（系统身份默认为已认证）
        /// </summary>
        public bool IsAuthenticated { get; private set; }

        /// <summary>
        /// 身份类型描述
        /// </summary>
        public string IdentityType { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 私有构造函数，防止外部直接实例化
        /// </summary>
        private SystemIdentity(
            long userId,
            string userName,
            string identityType,
            long? tenantId = null,
            long? storeId = null,
            bool isAuthenticated = true)
        {
            UserId = userId;
            UserName = userName;
            IdentityType = identityType;
            TenantId = tenantId;
            StoreId = storeId;
            IsAuthenticated = isAuthenticated;
        }

        #endregion

        #region 预定义系统身份

        /// <summary>
        /// 系统默认身份
        /// 用于：系统级操作、内部调用
        /// </summary>
        public static SystemIdentity System { get; } = new SystemIdentity(
            userId: SYSTEM_USER_ID,
            userName: "SYSTEM",
            identityType: "System");

        /// <summary>
        /// 数据迁移身份
        /// 用于：EF Core 数据迁移、数据库结构变更
        /// </summary>
        public static SystemIdentity Migration { get; } = new SystemIdentity(
            userId: MIGRATION_USER_ID,
            userName: "MIGRATION",
            identityType: "Migration");

        /// <summary>
        /// 种子数据身份
        /// 用于：初始化数据、种子数据插入
        /// </summary>
        public static SystemIdentity Seed { get; } = new SystemIdentity(
            userId: SEED_USER_ID,
            userName: "SEED",
            identityType: "Seed");

        /// <summary>
        /// 后台任务身份
        /// 用于：Hangfire、Quartz 等后台任务
        /// </summary>
        public static SystemIdentity BackgroundJob { get; } = new SystemIdentity(
            userId: BACKGROUND_JOB_USER_ID,
            userName: "BACKGROUND_JOB",
            identityType: "BackgroundJob");

        /// <summary>
        /// 定时任务身份
        /// 用于：定时任务、计划任务
        /// </summary>
        public static SystemIdentity ScheduledTask { get; } = new SystemIdentity(
            userId: SCHEDULED_TASK_USER_ID,
            userName: "SCHEDULED_TASK",
            identityType: "ScheduledTask");

        /// <summary>
        /// 消息队列身份
        /// 用于：RabbitMQ、Kafka 等消息消费
        /// </summary>
        public static SystemIdentity MessageQueue { get; } = new SystemIdentity(
            userId: MESSAGE_QUEUE_USER_ID,
            userName: "MESSAGE_QUEUE",
            identityType: "MessageQueue");

        #endregion

        #region 工厂方法

        /// <summary>
        /// 创建指定租户的系统身份
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="storeId">门店ID（可选）</param>
        /// <returns></returns>
        public static SystemIdentity ForTenant(long tenantId, long? storeId = null)
        {
            return new SystemIdentity(
                userId: SYSTEM_USER_ID,
                userName: $"SYSTEM@T{tenantId}",
                identityType: "TenantSystem",
                tenantId: tenantId,
                storeId: storeId);
        }

        /// <summary>
        /// 创建指定租户的迁移身份
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <returns></returns>
        public static SystemIdentity ForTenantMigration(long tenantId)
        {
            return new SystemIdentity(
                userId: MIGRATION_USER_ID,
                userName: $"MIGRATION@T{tenantId}",
                identityType: "TenantMigration",
                tenantId: tenantId);
        }

        /// <summary>
        /// 创建指定租户的种子数据身份
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="storeId">门店ID（可选）</param>
        /// <returns></returns>
        public static SystemIdentity ForTenantSeed(long tenantId, long? storeId = null)
        {
            return new SystemIdentity(
                userId: SEED_USER_ID,
                userName: $"SEED@T{tenantId}",
                identityType: "TenantSeed",
                tenantId: tenantId,
                storeId: storeId);
        }

        /// <summary>
        /// 创建自定义系统身份
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="userName">用户名</param>
        /// <param name="identityType">身份类型</param>
        /// <param name="tenantId">租户ID</param>
        /// <param name="storeId">门店ID</param>
        /// <returns></returns>
        public static SystemIdentity Custom(
            long userId,
            string userName,
            string identityType,
            long? tenantId = null,
            long? storeId = null)
        {
            return new SystemIdentity(
                userId: userId,
                userName: userName,
                identityType: identityType,
                tenantId: tenantId,
                storeId: storeId);
        }

        #endregion

        #region ICurrentIdentity 实现

        /// <summary>
        /// 获取可访问的门店ID列表
        /// 系统身份默认返回空列表（无门店限制）
        /// </summary>
        public Task<List<long>> GetShareStoreIdsAsync(CancellationToken ct = default)
        {
            // 如果指定了门店ID，返回该门店
            if (StoreId.HasValue)
            {
                return Task.FromResult(new List<long> { StoreId.Value });
            }

            // 否则返回空列表（表示无门店限制，可访问所有门店）
            return Task.FromResult(new List<long>());
        }

        #endregion

        #region 克隆方法

        /// <summary>
        /// 克隆并设置租户ID
        /// </summary>
        public SystemIdentity WithTenant(long tenantId)
        {
            return new SystemIdentity(
                userId: UserId!.Value,
                userName: UserName,
                identityType: IdentityType,
                tenantId: tenantId,
                storeId: StoreId,
                isAuthenticated: IsAuthenticated);
        }

        /// <summary>
        /// 克隆并设置门店ID
        /// </summary>
        public SystemIdentity WithStore(long storeId)
        {
            return new SystemIdentity(
                userId: UserId!.Value,
                userName: UserName,
                identityType: IdentityType,
                tenantId: TenantId,
                storeId: storeId,
                isAuthenticated: IsAuthenticated);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 判断是否为系统身份
        /// </summary>
        public static bool IsSystemIdentity(ICurrentIdentity identity)
        {
            return identity is SystemIdentity;
        }

        /// <summary>
        /// 判断是否为特定类型的系统身份
        /// </summary>
        public bool IsIdentityType(string type)
        {
            return string.Equals(IdentityType, type, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取身份描述
        /// </summary>
        public override string ToString()
        {
            var parts = new List<string>
            {
                $"Type={IdentityType}",
                $"UserId={UserId}",
                $"UserName={UserName}"
            };

            if (TenantId.HasValue)
            {
                parts.Add($"TenantId={TenantId}");
            }

            if (StoreId.HasValue)
            {
                parts.Add($"StoreId={StoreId}");
            }

            return $"SystemIdentity({string.Join(", ", parts)})";
        }

        #endregion
    }

}
