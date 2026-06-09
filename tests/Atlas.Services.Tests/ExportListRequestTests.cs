using Atlas.Exporting;

namespace Atlas.Services.Tests;

public sealed class ExportListRequestTests
{
    [Fact]
    public void ExportListRequest_SeparatesExportOptionsFromSearchCriteria()
    {
        var request = new SampleExportRequest
        {
            Format = "csv",
            SelectedFields = ["name", "id"],
            Criteria = new SampleCriteria
            {
                Keyword = "demo",
                IsActive = true
            }
        };

        Assert.IsAssignableFrom<IExportRequestOptions>(request);
        Assert.IsAssignableFrom<IExportSearchRequest<SampleCriteria>>(request);
        Assert.Equal("csv", request.Format);
        Assert.Equal(new[] { "name", "id" }, request.SelectedFields);
        Assert.Equal("demo", request.Criteria.Keyword);
        Assert.True(request.Criteria.IsActive);
    }

    [Fact]
    public void GetCriteria_ReturnsDefaultCriteriaWhenCriteriaIsNull()
    {
        var request = new NullCriteriaExportRequest();

        var criteria = ExportSearchRequest.GetCriteria(request);

        Assert.NotNull(criteria);
        Assert.Null(criteria.Keyword);
        Assert.Null(criteria.IsActive);
    }

    private sealed class SampleExportRequest : ExportListRequest<SampleCriteria>
    {
    }

    private sealed class NullCriteriaExportRequest : IExportSearchRequest<SampleCriteria>
    {
        public string? Format => null;

        public IReadOnlyList<string>? SelectedFields => null;

        public SampleCriteria Criteria => null!;
    }

    private sealed class SampleCriteria
    {
        public string? Keyword { get; init; }

        public bool? IsActive { get; init; }
    }
}
