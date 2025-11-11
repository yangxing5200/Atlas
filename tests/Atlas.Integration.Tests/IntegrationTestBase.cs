// Infrastructure/IntegrationTestBase.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Integration.Tests.Infrastructure
{
    public abstract class IntegrationTestBase : IAsyncLifetime
    {
        protected IServiceProvider ServiceProvider { get; private set; } = null!;
        protected IServiceScope Scope { get; private set; } = null!;

        public virtual async Task InitializeAsync()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
            Scope = ServiceProvider.CreateScope();

            await OnInitializeAsync();
        }

        public virtual async Task DisposeAsync()
        {
            await OnDisposeAsync();
            Scope?.Dispose();

            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        protected abstract void ConfigureServices(IServiceCollection services);

        protected virtual Task OnInitializeAsync() => Task.CompletedTask;

        protected virtual Task OnDisposeAsync() => Task.CompletedTask;

        protected T GetService<T>() where T : notnull
        {
            return Scope.ServiceProvider.GetRequiredService<T>();
        }
    }
}