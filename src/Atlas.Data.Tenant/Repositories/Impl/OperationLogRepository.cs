using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;

namespace Atlas.Data.Tenant.Repositories.Impl
{
    public class OperationLogRepository : RepositoryBase<OperationLog>, IOperationLogRepository
    {
        public OperationLogRepository(ITenantDbContextFactory dbContextFactory, IDataScope dataScope)
            : base(dbContextFactory, dataScope)
        {
        }
    }
}
