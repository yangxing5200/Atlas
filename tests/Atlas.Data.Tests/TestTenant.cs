using Atlas.Core.Entities;

namespace Atlas.Data.Tests.TestEntities
{
    /// <summary>
    /// 测试用租户实体
    /// </summary>
    public class TestTenant: VersionedEntity
    {
        /// <summary>
        /// 租户名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 租户编码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 状态：0-禁用，1-启用
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string? Remark { get; set; }
    }

    /// <summary>
    /// 测试用用户实体（使用Snowflake ID）
    /// </summary>
    public class TestUser: TenantVersionedEntity
    {

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 邮箱
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// 手机号
        /// </summary>
        public string Phone { get; set; }
    }

    /// <summary>
    /// 测试用产品实体（无审计字段的简单实体）
    /// </summary>
    public class TestProduct
    {
        public long Id { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
    }
}