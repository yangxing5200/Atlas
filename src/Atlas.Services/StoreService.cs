using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Atlas.Models.DTOs;
using Atlas.Services.Abstractions;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Services
{
    public class StoreService : ServiceBase<Store, StoreDto>, IStoreService
    {
        public StoreService(IRepository<Store> repository, IUnitOfWork unitOfWork, IMapper mapper) : base(repository, unitOfWork, mapper)
        {
        }
    }
}
