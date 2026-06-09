using System.Text;
using Atlas.Exporting;
using Atlas.Exporting.Writing;

namespace Atlas.Services.Tests;

public sealed class CsvExportFormatWriterTests
{
    [Fact]
    public async Task WriteAsync_UsesColumnMetadataForOrderVisibilityAndFormatting()
    {
        var provider = new SampleExportProvider(
        [
            new("name", "Name") { Order = 30 },
            new("secret", "Secret") { Hidden = true, IsSensitive = true, Order = 40 },
            new("amount", "Amount") { ValueKind = ExportValueKind.Number, Format = "0.00", Order = 10 },
            new("createdAt", "Created") { ValueKind = ExportValueKind.DateTime, Format = "yyyy-MM-dd HH:mm", Order = 20 }
        ],
        [
            new Dictionary<string, ExportCellValue>
            {
                ["name"] = "Ada",
                ["secret"] = "not exported",
                ["amount"] = 12.345m,
                ["createdAt"] = new DateTime(2026, 6, 4, 1, 2, 0, DateTimeKind.Utc)
            }
        ]);

        var output = new MemoryStream();

        await new CsvExportFormatWriter().WriteAsync(
            CreateContext(provider, output));

        output.Position = 0;
        using var reader = new StreamReader(output, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var csv = (await reader.ReadToEndAsync()).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Equal("Amount,Created,Name\n12.35,2026-06-04 01:02,Ada\n", csv);
    }

    [Fact]
    public async Task WriteAsync_RejectsDuplicateVisibleFields()
    {
        var provider = new SampleExportProvider(
        [
            new("id", "Id"),
            new("ID", "Identifier")
        ],
        []);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new CsvExportFormatWriter().WriteAsync(CreateContext(provider, new MemoryStream())));

        Assert.Contains("declared more than once", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WriteAsync_UsesSelectedFieldsWhenQueryImplementsColumnSelection()
    {
        var provider = new SelectableExportProvider(
        [
            new("id", "Id") { ValueKind = ExportValueKind.Number },
            new("name", "Name"),
            new("secret", "Secret") { Hidden = true }
        ],
        [
            new Dictionary<string, ExportCellValue>
            {
                ["id"] = 1,
                ["name"] = "Ada",
                ["secret"] = "hidden"
            }
        ]);
        var output = new MemoryStream();

        await new CsvExportFormatWriter().WriteAsync(
            CreateContext(
                provider,
                output,
                new SelectedFieldsQuery(["name", "id"])));

        output.Position = 0;
        using var reader = new StreamReader(output, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var csv = (await reader.ReadToEndAsync()).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Equal("Name,Id\nAda,1\n", csv);
    }

    [Fact]
    public async Task WriteAsync_RejectsUnknownSelectedFields()
    {
        var provider = new SelectableExportProvider(
        [
            new("id", "Id")
        ],
        []);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new CsvExportFormatWriter().WriteAsync(
                CreateContext(
                    provider,
                    new MemoryStream(),
                    new SelectedFieldsQuery(["missing"]))));

        Assert.Contains("not available", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WriteAsync_ContinuesWhenProviderReturnsLessThanRequestedPageSize()
    {
        var provider = new CappedPageExportProvider(
        [
            new("id", "Id") { ValueKind = ExportValueKind.Number }
        ]);
        var output = new MemoryStream();

        await new CsvExportFormatWriter().WriteAsync(
            CreateContext(provider, output, pageSize: 5));

        output.Position = 0;
        using var reader = new StreamReader(output, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var csv = (await reader.ReadToEndAsync()).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Equal("Id\n1\n2\n3\n4\n5\n", csv);
    }

    private static ExportWriteContext CreateContext(
        IExportTaskProvider provider,
        Stream output,
        object? query = null,
        int pageSize = 500)
    {
        var payload = new ExportJobPayload(
            1,
            10,
            null,
            20,
            provider.ExportTaskType,
            provider.ResourceCode,
            provider.PermissionCode,
            "csv",
            "{}",
            "hash",
            "en-US",
            "UTC",
            pageSize,
            null,
            "v1");

        return new ExportWriteContext
        {
            Payload = payload,
            Provider = provider,
            TaskContext = new ExportTaskContext
            {
                ExportJobId = payload.ExportJobId,
                TenantId = payload.TenantId,
                StoreId = payload.StoreId,
                UserId = payload.UserId,
                ExportTaskType = payload.ExportTaskType,
                ResourceCode = payload.ResourceCode,
                Query = query ?? new object()
            },
            Output = output,
            PageSize = payload.PageSize,
            MaxRows = payload.MaxRows,
            Culture = payload.Culture,
            TimeZone = payload.TimeZone
        };
    }

    private sealed record SelectedFieldsQuery(
        IReadOnlyList<string>? SelectedFields) : IExportColumnSelection;

    private sealed class SampleExportProvider : ExportTaskProvider<object>
    {
        private readonly IReadOnlyList<IReadOnlyDictionary<string, ExportCellValue>> _rows;

        public SampleExportProvider(
            IReadOnlyList<ExportColumn> columns,
            IReadOnlyList<IReadOnlyDictionary<string, ExportCellValue>> rows)
        {
            Columns = columns;
            _rows = rows;
        }

        public override string ExportTaskType => "test.sample";
        public override string ResourceCode => "test.resource";
        public override string PermissionCode => "test.export";
        public override IReadOnlyList<ExportColumn> Columns { get; }

        public override Task<ExportPage> ReadPageAsync(
            ExportTaskContext<object> context,
            int pageIndex,
            int pageSize,
            CancellationToken ct = default)
        {
            return Task.FromResult(pageIndex == 1
                ? new ExportPage(_rows, _rows.Count)
                : new ExportPage([]));
        }
    }

    private sealed class SelectableExportProvider : ExportTaskProvider<SelectedFieldsQuery>
    {
        private readonly IReadOnlyList<IReadOnlyDictionary<string, ExportCellValue>> _rows;

        public SelectableExportProvider(
            IReadOnlyList<ExportColumn> columns,
            IReadOnlyList<IReadOnlyDictionary<string, ExportCellValue>> rows)
        {
            Columns = columns;
            _rows = rows;
        }

        public override string ExportTaskType => "test.selectable";
        public override string ResourceCode => "test.resource";
        public override string PermissionCode => "test.export";
        public override IReadOnlyList<ExportColumn> Columns { get; }

        public override Task<ExportPage> ReadPageAsync(
            ExportTaskContext<SelectedFieldsQuery> context,
            int pageIndex,
            int pageSize,
            CancellationToken ct = default)
        {
            return Task.FromResult(pageIndex == 1
                ? new ExportPage(_rows, _rows.Count)
                : new ExportPage([]));
        }
    }

    private sealed class CappedPageExportProvider : ExportTaskProvider<object>
    {
        private static readonly IReadOnlyList<IReadOnlyDictionary<string, ExportCellValue>> AllRows =
        [
            new Dictionary<string, ExportCellValue> { ["id"] = 1 },
            new Dictionary<string, ExportCellValue> { ["id"] = 2 },
            new Dictionary<string, ExportCellValue> { ["id"] = 3 },
            new Dictionary<string, ExportCellValue> { ["id"] = 4 },
            new Dictionary<string, ExportCellValue> { ["id"] = 5 }
        ];

        public CappedPageExportProvider(IReadOnlyList<ExportColumn> columns)
        {
            Columns = columns;
        }

        public override string ExportTaskType => "test.capped";
        public override string ResourceCode => "test.resource";
        public override string PermissionCode => "test.export";
        public override IReadOnlyList<ExportColumn> Columns { get; }

        public override Task<ExportPage> ReadPageAsync(
            ExportTaskContext<object> context,
            int pageIndex,
            int pageSize,
            CancellationToken ct = default)
        {
            const int actualPageSize = 2;
            var rows = AllRows
                .Skip((pageIndex - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToArray();

            return Task.FromResult(new ExportPage(rows, AllRows.Count));
        }
    }
}
