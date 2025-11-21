using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Data.Tenant.Context;

namespace Atlas.Data.Tenant.Providers
{
    /// <summary>
    /// 用于存储当前请求的写/读 DbContext，支持异步上下文隔离
    /// </summary>
    public class TenantDbContextAccessor : IAsyncDisposable
    {
        private static readonly AsyncLocal<AtlasTenantDbContext> _writeContext = new();
        private static readonly AsyncLocal<AtlasTenantDbContext> _readContext = new();

        /// <summary>
        /// 写 DbContext
        /// </summary>
        public AtlasTenantDbContext WriteDbContext
        {
            get
            {
                if (_writeContext.Value == null)
                    throw new InvalidOperationException("当前上下文中没有写 DbContext");
                return _writeContext.Value;
            }
            set => _writeContext.Value = value;
        }

        /// <summary>
        /// 读 DbContext，默认 fallback 到写 DbContext
        /// </summary>
        public AtlasTenantDbContext ReadDbContext
        {
            get
            {
                var context = _readContext.Value ?? _writeContext.Value;
                if (context == null)
                    throw new InvalidOperationException("当前上下文中没有可用的读/写 DbContext");
                return context;
            }
            set => _readContext.Value = value;
        }

        /// <summary>
        /// 异步释放 DbContext，保证每个 DbContext 都被 Dispose
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_readContext.Value != null)
            {
                try
                {
                    await _readContext.Value.DisposeAsync();
                }
                catch
                {
                    // 可以 log 异常
                }
                _readContext.Value = null;
            }

            if (_writeContext.Value != null)
            {
                try
                {
                    await _writeContext.Value.DisposeAsync();
                }
                catch
                {
                    // 可以 log 异常
                }
                _writeContext.Value = null;
            }
        }

        /// <summary>
        /// 同步清理 DbContext（不 Dispose，只清空引用），适合某些特殊场景
        /// </summary>
        public void Clear()
        {
            _readContext.Value = null;
            _writeContext.Value = null;
        }
    }
}
