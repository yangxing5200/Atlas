using Atlas.Exporting;
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

        Assert.DoesNotContain(
            typeof(ExportTenantRecordsRequest).GetProperties(),
            property => property.Name == "TenantId");
    }

    [Fact]
    public void Export_request_supports_selected_columns()
    {
        Assert.True(typeof(IExportColumnSelection).IsAssignableFrom(typeof(ExportTenantRecordsRequest)));
        Assert.True(typeof(IExportSearchRequest<TenantRecordExportCriteria>).IsAssignableFrom(typeof(ExportTenantRecordsRequest)));
        Assert.Contains(
            typeof(ExportTenantRecordsRequest).GetProperties(),
            property => property.Name == nameof(ExportTenantRecordsRequest.SelectedFields));
        Assert.Contains(
            typeof(ExportTenantRecordsRequest).GetProperties(),
            property => property.Name == nameof(ExportTenantRecordsRequest.Criteria));
    }
}
