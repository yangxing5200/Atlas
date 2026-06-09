using Atlas.Core.Context;

namespace Atlas.Core.Services
{
    /// <summary>
    /// 当前用户服务接口
    /// </summary>
    public interface ICurrentIdentity : ITenantExecutionContext
    {
        /// <summary>
        /// 当前用户名
        /// </summary>
        string UserName { get; }
    }
}
