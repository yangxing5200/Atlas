using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Atlas.Models.DTOs;
using Atlas.Services.Abstractions;
using Atlas.Services.Abstractions.Base;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Services
{
    public class UserLoginLogService : ServiceBase<UserLoginLog, UserLoginLogDto>, IUserLoginLogService
    {
        public UserLoginLogService(IRepository<UserLoginLog> repository, IUnitOfWork unitOfWork, IMapper mapper) : base(repository, unitOfWork, mapper)
        {
        }
    }
}
