using Atlas.ModuleTemplate.Models;
using Xunit;

namespace Atlas.ModuleTemplate.Tests;

public sealed class ModuleTemplateTests
{
    [Fact]
    public void Request_models_do_not_accept_tenant_context()
    {
        Assert.DoesNotContain(
            typeof(CreateTenantRecordRequest).GetProperties(),
            property => property.Name == "TenantId");

        Assert.DoesNotContain(
            typeof(UpdateTenantRecordRequest).GetProperties(),
            property => property.Name == "TenantId");
    }
}
