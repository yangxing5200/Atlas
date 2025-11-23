using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Caching.Abstractions
{
    public interface ICacheInvalidationBus
    {
        Task PublishInvalidationAsync(string key);
        void Subscribe(Action<string> onInvalidate);
    }
}
