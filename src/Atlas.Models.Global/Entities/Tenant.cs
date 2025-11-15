using Atlas.Core.Entities;
using Atlas.Core.Enums;

namespace Atlas.Models.Global.Entities;

public class Tenant : AuditableEntity
{
    /// <summary>
    /// 公司名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 品牌名称
    /// </summary>
    public string? BrandName { get; set; }

    /// <summary>
    /// 地址
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// 电话
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// 联系人姓名
    /// </summary>
    public string ContactName { get; set; }

    /// <summary>
    /// 联系人手机号
    /// </summary>
    public string ContactPhoneNumber { get; set; }

    /// <summary>
    /// 联系人邮箱
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// 公司代码
    /// </summary>
    public string Domain { get; set; }

    /// <summary>
    /// 租户类型
    /// </summary>
    public TenantType TenantType { get; set; }

    /// <summary>
    /// 省份
    /// </summary>
    public string? Province { get; set; }

    /// <summary>
    /// 城市
    /// </summary>
    public string City { get; set; }

    /// <summary>
    /// 类别
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 租户状态
    /// </summary>
    public TenantStatus Status { get; set; }

    /// <summary>
    /// 连锁类型
    /// </summary>
    public BusinessType BusinessType { get; set; }

    /// <summary>
    /// 关联的数据库实例Id
    /// </summary>
    public int DatabaseInstanceId { get; set; }

    /// <summary>
    /// 诊所数量
    /// </summary>
    public int OfficeCount { get; set; }

    /// <summary>
    /// 关联的数据库实例
    /// </summary>
    public DatabaseInstance DatabaseInstance { get; set; }

    /// <summary>
    /// 租户类别字典
    /// </summary>
    public static class TenantCategory
    {
        public static readonly string Trial = "试用";
        public static readonly string Mobile = "Mobile";
    }

    /// <summary>
    /// 个人版租户代码前缀
    /// </summary>
    public static readonly string MobileTenantPrefix = "#M";
}
