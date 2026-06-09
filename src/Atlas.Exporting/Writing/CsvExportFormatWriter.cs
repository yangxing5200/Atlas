using System.Globalization;
using System.Text;

namespace Atlas.Exporting.Writing;

public sealed class CsvExportFormatWriter : IExportFormatWriter
{
    public string Format => "csv";
    public string ContentType => "text/csv; charset=utf-8";
    public string FileExtension => ".csv";

    public async Task<ExportWriteResult> WriteAsync(
        ExportWriteContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var columns = NormalizeColumns(
            await context.Provider.ResolveColumnsAsync(context.TaskContext, ct));

        if (columns.Count == 0)
            throw new InvalidOperationException("Export provider must declare at least one column.");

        var culture = ResolveCulture(context.Culture);
        var timeZone = ResolveTimeZone(context.TimeZone);
        var processedRows = 0L;
        long? totalRows = null;
        var pageIndex = 1;
        var pageSize = Math.Max(1, context.PageSize);

        await using var writer = new StreamWriter(
            context.Output,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            bufferSize: 64 * 1024,
            leaveOpen: true);

        await WriteRowAsync(
            writer,
            columns.Select(x => x.Title),
            ct);

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var page = await context.Provider.ReadPageAsync(
                context.TaskContext,
                pageIndex,
                pageSize,
                ct);

            totalRows ??= page.TotalRows;
            if (page.Rows.Count == 0)
                break;

            foreach (var row in page.Rows)
            {
                if (context.MaxRows.HasValue && processedRows >= context.MaxRows.Value)
                    return new ExportWriteResult(processedRows, totalRows);

                await WriteRowAsync(
                    writer,
                    columns.Select(column =>
                        FormatValue(ResolveValue(row, column.Field), column, culture, timeZone)),
                    ct);

                processedRows++;
            }

            if (totalRows.HasValue && processedRows >= totalRows.Value)
                break;

            pageIndex++;
        }

        await writer.FlushAsync(ct);
        return new ExportWriteResult(processedRows, totalRows);
    }

    private static IReadOnlyList<ExportColumn> NormalizeColumns(
        IReadOnlyList<ExportColumn> columns)
    {
        var visibleColumns = columns
            .Select((column, index) => new { Column = column, Index = index })
            .Where(x => !x.Column.Hidden && !string.IsNullOrWhiteSpace(x.Column.Title))
            .OrderBy(x => x.Column.Order)
            .ThenBy(x => x.Index)
            .Select(x => x.Column)
            .ToArray();

        foreach (var column in visibleColumns)
        {
            if (string.IsNullOrWhiteSpace(column.Field))
                throw new InvalidOperationException("Export column field is required for visible columns.");
        }

        var duplicateField = visibleColumns
            .GroupBy(x => x.Field.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicateField != null)
            throw new InvalidOperationException($"Export column field '{duplicateField.Key}' is declared more than once.");

        return visibleColumns;
    }

    private static ExportCellValue ResolveValue(
        IReadOnlyDictionary<string, ExportCellValue> row,
        string field)
    {
        if (row.TryGetValue(field, out var value))
            return value;

        var match = row.FirstOrDefault(x => string.Equals(x.Key, field, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(match.Key) ? ExportCellValue.Empty : match.Value;
    }

    private static string FormatValue(
        ExportCellValue cellValue,
        ExportColumn column,
        CultureInfo culture,
        TimeZoneInfo timeZone)
    {
        var value = cellValue.Value;
        var valueKind = cellValue.ValueKind ?? column.ValueKind;
        var format = cellValue.Format ?? column.Format;

        if (value == null)
            return string.Empty;

        if (valueKind != ExportValueKind.Auto)
            return FormatTypedValue(value, valueKind, format, culture, timeZone);

        return value switch
        {
            DateOnly dateOnly => dateOnly.ToString(format ?? "d", culture),
            DateTime dateTime => FormatDateTime(dateTime, culture, timeZone, format),
            DateTimeOffset dateTimeOffset => FormatDateTimeOffset(dateTimeOffset, culture, timeZone, format),
            IFormattable formattable => formattable.ToString(format, culture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string FormatTypedValue(
        object value,
        ExportValueKind valueKind,
        string? format,
        CultureInfo culture,
        TimeZoneInfo timeZone)
    {
        return valueKind switch
        {
            ExportValueKind.String => value.ToString() ?? string.Empty,
            ExportValueKind.Boolean => FormatBoolean(value),
            ExportValueKind.Number => value is IFormattable formattable
                ? formattable.ToString(format, culture) ?? string.Empty
                : value.ToString() ?? string.Empty,
            ExportValueKind.Date => FormatDate(value, format, culture, timeZone),
            ExportValueKind.DateTime => FormatDateTimeValue(value, format, culture, timeZone),
            ExportValueKind.DateTimeOffset => FormatDateTimeOffsetValue(value, format, culture, timeZone),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string FormatBoolean(object value)
    {
        if (value is bool boolValue)
            return boolValue ? "true" : "false";

        return value.ToString() ?? string.Empty;
    }

    private static string FormatDate(
        object value,
        string? format,
        CultureInfo culture,
        TimeZoneInfo timeZone)
    {
        return value switch
        {
            DateOnly dateOnly => dateOnly.ToString(format ?? "d", culture),
            DateTime dateTime => FormatDateTime(dateTime, culture, timeZone, format ?? "d"),
            DateTimeOffset dateTimeOffset => FormatDateTimeOffset(dateTimeOffset, culture, timeZone, format ?? "d"),
            IFormattable formattable => formattable.ToString(format, culture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string FormatDateTimeValue(
        object value,
        string? format,
        CultureInfo culture,
        TimeZoneInfo timeZone)
    {
        return value switch
        {
            DateTime dateTime => FormatDateTime(dateTime, culture, timeZone, format),
            DateTimeOffset dateTimeOffset => FormatDateTimeOffset(dateTimeOffset, culture, timeZone, format),
            IFormattable formattable => formattable.ToString(format, culture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string FormatDateTimeOffsetValue(
        object value,
        string? format,
        CultureInfo culture,
        TimeZoneInfo timeZone)
    {
        return value switch
        {
            DateTimeOffset dateTimeOffset => FormatDateTimeOffset(dateTimeOffset, culture, timeZone, format),
            DateTime dateTime => FormatDateTime(dateTime, culture, timeZone, format),
            IFormattable formattable => formattable.ToString(format, culture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string FormatDateTime(
        DateTime value,
        CultureInfo culture,
        TimeZoneInfo timeZone,
        string? format)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone).ToString(format ?? "G", culture);
    }

    private static string FormatDateTimeOffset(
        DateTimeOffset value,
        CultureInfo culture,
        TimeZoneInfo timeZone,
        string? format)
    {
        return TimeZoneInfo.ConvertTime(value, timeZone).ToString(format ?? "G", culture);
    }

    private static async Task WriteRowAsync(
        TextWriter writer,
        IEnumerable<string> values,
        CancellationToken ct)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first)
                await writer.WriteAsync(',');

            await writer.WriteAsync(Escape(value.AsMemory()).AsMemory(), ct);
            first = false;
        }

        await writer.WriteLineAsync();
    }

    private static string Escape(ReadOnlyMemory<char> value)
    {
        var span = value.Span;
        var mustQuote = span.Contains(',') ||
                        span.Contains('"') ||
                        span.Contains('\r') ||
                        span.Contains('\n');

        if (!mustQuote)
            return value.ToString();

        return $"\"{value.ToString().Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static CultureInfo ResolveCulture(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return CultureInfo.InvariantCulture;

        try
        {
            return CultureInfo.GetCultureInfo(culture);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
