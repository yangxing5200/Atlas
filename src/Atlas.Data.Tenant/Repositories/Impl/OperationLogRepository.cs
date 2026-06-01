using Atlas.Core.Authorization;
using Atlas.Core.Entities.Tenant;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;

namespace Atlas.Data.Tenant.Repositories.Impl
{
    public class OperationLogRepository : RepositoryBase<OperationLog>, IOperationLogRepository
    {
        public OperationLogRepository(
            ITenantDbContextFactory dbContextFactory,
            IDataScope dataScope,
            IAtlasDataScopePredicateBuilder dataScopePredicateBuilder,
            ICurrentIdentity currentIdentity)
            : base(dbContextFactory, dataScope, dataScopePredicateBuilder, currentIdentity)
        {
        }
    }
}
