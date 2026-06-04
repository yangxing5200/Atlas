using System.Globalization;
using System.Text;

namespace Atlas.Exporting;

public sealed class CsvExportFormatWriter : IExportFormatWriter
{
    public string Format => "csv";
    public string ContentType => "text/csv; charset=utf-8";
    public string FileExtension => ".csv";

    public async Task<ExportWriteResult> WriteAsync(ExportWriteContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var columns = context.Provider.Columns;
        if (columns.Count == 0)
            throw new InvalidOperationException($"Export task '{context.Provider.ExportTaskType}' has no columns.");

        await using var writer = new StreamWriter(context.Output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 81920, leaveOpen: true);
        await WriteRowAsync(writer, columns.Select(x => x.Header), ct);

        var pageIndex = 0;
        long processed = 0;
        long? totalRows = null;

        while (!ct.IsCancellationRequested)
        {
            var page = await context.Provider.ReadPageAsync(new ExportTaskContext
            {
                ExportJobId = context.Payload.ExportJobId,
                TenantId = context.Payload.TenantId,
                StoreId = context.Payload.StoreId,
                UserId = context.Payload.UserId,
                ExportTaskType = context.Payload.ExportTaskType,
                ResourceCode = context.Payload.ResourceCode,
                Query = context.Query
            }, pageIndex, context.Payload.PageSize, ct);

            totalRows ??= page.TotalRows;
            foreach (var row in page.Rows)
            {
                if (context.Payload.MaxRows.HasValue && processed >= context.Payload.MaxRows.Value)
                    throw new InvalidOperationException($"Export row limit {context.Payload.MaxRows.Value} exceeded.");

                await WriteRowAsync(writer, columns.Select(column => FormatCell(column.ValueAccessor(row))), ct);
                processed++;
            }

            var progress = totalRows is > 0
                ? Math.Min(99, (int)Math.Floor(processed * 100D / totalRows.Value))
                : page.HasMore ? 50 : 99;
            await context.ReportProgressAsync(processed, totalRows, progress, ct);

            if (!page.HasMore)
                break;

            pageIndex++;
        }

        await writer.FlushAsync(ct);
        return new ExportWriteResult(processed);
    }

    private static Task WriteRowAsync(TextWriter writer, IEnumerable<string?> cells, CancellationToken ct)
    {
        var line = string.Join(',', cells.Select(Escape));
        return writer.WriteLineAsync(line.AsMemory(), ct).AsTask();
    }

    private static string? FormatCell(object? value)
    {
        return value switch
        {
            null => null,
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var mustQuote = value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n');
        var escaped = value.Replace("\"", "\"\"");
        return mustQuote ? $"\"{escaped}\"" : escaped;
    }
}
