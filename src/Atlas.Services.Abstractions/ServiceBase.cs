using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.Entities;
using Atlas.Data.Abstractions;

namespace Atlas.Services.Abstractions
{
    public class ServiceBase
    {
        private readonly IUnitOfWork _unitOfWork;
        public ServiceBase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IRepository<T> ResolveRepository<T>() where T : class, IBaseEntity<long>
        {
            return _unitOfWork.GetRepository<T>();
        }
    }
}
