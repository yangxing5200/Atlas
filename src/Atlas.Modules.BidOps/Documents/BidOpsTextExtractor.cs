using System.IO.Compression;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ExcelDataReader;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Atlas.Modules.BidOps.Documents;

public sealed partial class BidOpsTextExtractor : IBidOpsTextExtractor
{
    private const int MaxExtractedTextLength = 500_000;
    private const int MaxArchiveDepth = 5;
    private const int MaxArchiveEntries = 100;
    private const int MaxArchiveEntryBytes = 20 * 1024 * 1024;
    private const int MinLegacyTextRunLength = 4;
    private const int MaxPdfTableRowsPerPage = 120;

    private static readonly XNamespace WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    public async Task<string> ExtractAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        return await ExtractInternalAsync(stream, fileName, contentType, depth: 0, cancellationToken);
    }

    private async Task<string> ExtractInternalAsync(
        Stream stream,
        string fileName,
        string contentType,
        int depth,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        var normalizedContentType = contentType.Trim().ToLowerInvariant();
        if (extension is "txt" or "csv" ||
            normalizedContentType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase))
        {
            return Trim(await ReadTextAsync(stream, cancellationToken));
        }

        if (extension is "html" or "htm" ||
            normalizedContentType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            var html = await ReadTextAsync(stream, cancellationToken);
            return Trim(ExtractHtmlText(html));
        }

        if (extension == "docx" ||
            normalizedContentType.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase))
        {
            return Trim(ExtractDocxText(stream));
        }

        if (extension is "xlsx" or "xlsm" or "xltx" or "xltm" ||
            normalizedContentType.Contains("spreadsheetml", StringComparison.OrdinalIgnoreCase))
        {
            return Trim(ExtractXlsxText(stream));
        }

        if (extension == "xls" ||
            normalizedContentType.Contains("vnd.ms-excel", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return Trim(ExtractLegacyExcelText(stream));
            }
            catch
            {
                return Trim(await ExtractLegacyBinaryTextAsync(stream, cancellationToken));
            }
        }

        if (extension == "zip" ||
            normalizedContentType.Contains("zip", StringComparison.OrdinalIgnoreCase))
        {
            return Trim(await ExtractZipTextAsync(stream, fileName, depth, cancellationToken));
        }

        if (extension == "pdf" ||
            normalizedContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Trim(ExtractPdfText(stream));
        }

        if (extension == "doc" ||
            normalizedContentType.Contains("msword", StringComparison.OrdinalIgnoreCase))
        {
            return Trim(await ExtractLegacyBinaryTextAsync(stream, cancellationToken));
        }

        return string.Empty;
    }

    private static async Task<string> ReadTextAsync(
        Stream stream,
        CancellationToken ct)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }

    private static string ExtractHtmlText(string html)
    {
        var withoutScripts = ScriptRegex().Replace(html, " ");
        withoutScripts = StyleRegex().Replace(withoutScripts, " ");
        var text = TagRegex().Replace(withoutScripts, " ");
        return NormalizeWhitespace(WebUtility.HtmlDecode(text));
    }

    private static string ExtractDocxText(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.GetEntry("word/document.xml");
        if (entry == null)
            return string.Empty;

        var document = LoadXml(entry);
        var body = document.Root?.Element(WordNamespace + "body");
        if (body == null)
            return string.Empty;

        var builder = new StringBuilder();
        var recentHeading = string.Empty;
        var tableIndex = 1;

        foreach (var child in body.Elements())
        {
            if (child.Name == WordNamespace + "p")
            {
                var paragraph = ReadWordText(child);
                if (string.IsNullOrWhiteSpace(paragraph))
                    continue;

                if (builder.Length > 0)
                    builder.AppendLine();

                builder.AppendLine(paragraph);
                recentHeading = paragraph;
                continue;
            }

            if (child.Name != WordNamespace + "tbl")
                continue;

            var rows = ReadWordTableRows(child);
            if (rows.Count == 0)
                continue;

            if (builder.Length > 0)
                builder.AppendLine();

            AppendMarkdownTable(builder, tableIndex, recentHeading, rows);
            tableIndex++;
        }

        return NormalizePdfWhitespace(builder.ToString());
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadWordTableRows(XElement table)
    {
        return table
            .Elements(WordNamespace + "tr")
            .Select(row => row
                .Elements(WordNamespace + "tc")
                .Select(ReadWordTableCellText)
                .ToList())
            .Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToList();
    }

    private static string ReadWordTableCellText(XElement cell)
    {
        var paragraphs = cell
            .Elements(WordNamespace + "p")
            .Select(ReadWordText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return paragraphs.Count > 0
            ? NormalizeWhitespace(string.Join("；", paragraphs))
            : ReadWordText(cell);
    }

    private static string ReadWordText(XElement element)
    {
        var builder = new StringBuilder();
        foreach (var child in element.Descendants())
        {
            if (child.Name == WordNamespace + "t")
            {
                builder.Append(child.Value);
            }
            else if (child.Name == WordNamespace + "tab" ||
                     child.Name == WordNamespace + "br" ||
                     child.Name == WordNamespace + "cr")
            {
                builder.Append(' ');
            }
        }

        return NormalizeWhitespace(builder.ToString());
    }

    private static void AppendMarkdownTable(
        StringBuilder builder,
        int tableIndex,
        string nearbyHeading,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var normalizedRows = rows
            .Select(row => row.Select(cell => NormalizeWhitespace(cell)).ToList())
            .Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToList();
        if (normalizedRows.Count == 0)
            return;

        var headerIndex = FindWordTableHeaderIndex(normalizedRows);
        var header = BuildWordTableHeader(normalizedRows, headerIndex);
        var columnCount = Math.Max(header.Count, normalizedRows.Max(row => row.Count));
        header = PadOrTrimRow(header, columnCount);

        builder.AppendLine(string.IsNullOrWhiteSpace(nearbyHeading)
            ? $"## 表格 {tableIndex}"
            : $"## 表格 {tableIndex}：{nearbyHeading}");
        AppendMarkdownRow(builder, header);
        AppendMarkdownRow(builder, Enumerable.Repeat("---", columnCount));

        foreach (var row in normalizedRows.Skip(headerIndex + 1))
        {
            var padded = PadOrTrimRow(row, columnCount);
            if (IsRepeatedHeaderRow(padded, header))
                continue;

            AppendMarkdownRow(builder, padded);
        }
    }

    private static IReadOnlyList<string> BuildWordTableHeader(
        IReadOnlyList<IReadOnlyList<string>> rows,
        int headerIndex)
    {
        var header = rows[headerIndex].ToList();
        for (var i = headerIndex - 1; i >= 0; i--)
        {
            var parent = rows[i];
            var columnCount = Math.Max(header.Count, parent.Count);
            header = PadOrTrimRow(header, columnCount);
            var paddedParent = PadOrTrimRow(parent, columnCount);
            for (var column = 0; column < columnCount; column++)
            {
                if (string.IsNullOrWhiteSpace(header[column]) &&
                    !string.IsNullOrWhiteSpace(paddedParent[column]))
                {
                    header[column] = paddedParent[column];
                }
            }
        }

        return header;
    }

    private static int FindWordTableHeaderIndex(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var bestIndex = 0;
        var bestScore = -1;
        var limit = Math.Min(rows.Count, 3);
        for (var i = 0; i < limit; i++)
        {
            var score = ScoreWordTableHeaderRow(rows[i]);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static int ScoreWordTableHeaderRow(IReadOnlyList<string> row)
    {
        var headerText = NormalizeHeaderText(string.Join("|", row));
        var score = 0;
        foreach (var token in new[]
                 {
                     "分标编号", "分标名称", "包号", "包名称", "采购范围", "服务期", "框架协议有效期", "实施地点",
                     "分标", "采购编号", "项目单位", "项目名称", "子项目名称", "项目概况", "最高限价", "子项最高限价",
                     "报价方式", "需求单位", "物资名称", "数量", "工期", "首批交货日期"
                 })
        {
            if (headerText.Contains(NormalizeHeaderText(token), StringComparison.OrdinalIgnoreCase))
                score++;
        }

        foreach (var token in new[] { "资质要求", "业绩要求", "人员要求" })
        {
            if (headerText.Contains(NormalizeHeaderText(token), StringComparison.OrdinalIgnoreCase))
                score += 2;
        }

        return score;
    }

    private static List<string> PadOrTrimRow(IReadOnlyList<string> row, int columnCount)
    {
        var result = row.Take(columnCount).ToList();
        while (result.Count < columnCount)
            result.Add(string.Empty);

        return result;
    }

    private static bool IsRepeatedHeaderRow(
        IReadOnlyList<string> row,
        IReadOnlyList<string> header)
    {
        if (row.Count != header.Count)
            return false;

        var matches = 0;
        for (var i = 0; i < row.Count; i++)
        {
            if (string.Equals(
                    NormalizeHeaderText(row[i]),
                    NormalizeHeaderText(header[i]),
                    StringComparison.OrdinalIgnoreCase))
            {
                matches++;
            }
        }

        return matches >= Math.Max(2, header.Count - 1);
    }

    private static void AppendMarkdownRow(
        StringBuilder builder,
        IEnumerable<string> cells)
    {
        builder.Append("| ");
        builder.AppendJoin(" | ", cells.Select(EscapeMarkdownTableCell));
        builder.AppendLine(" |");
    }

    private static string EscapeMarkdownTableCell(string value)
    {
        return NormalizeWhitespace(value)
            .Replace("|", "/", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }

    private static string ExtractXlsxText(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using var archive = OpenReadZipArchive(stream);
        var sharedStrings = ReadSharedStrings(archive);
        var sheetNamesByPath = ReadSheetNamesByPath(archive);
        var builder = new StringBuilder();
        var tableIndex = 1;

        foreach (var entry in archive.Entries
                     .Where(x => x.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) &&
                                 x.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase))
        {
            var normalizedPath = NormalizeArchivePath(entry.FullName);
            var sheetName = sheetNamesByPath.TryGetValue(normalizedPath, out var name)
                ? name
                : Path.GetFileNameWithoutExtension(entry.Name);
            var rows = ExtractWorksheetRows(entry, sharedStrings);
            if (rows.Count == 0)
                continue;

            if (builder.Length > 0)
                builder.AppendLine().AppendLine();

            AppendMarkdownTable(builder, tableIndex, $"Sheet: {sheetName}", rows);
            tableIndex++;

            if (builder.Length >= MaxExtractedTextLength)
                break;
        }

        return NormalizePdfWhitespace(builder.ToString());
    }

    private async Task<string> ExtractZipTextAsync(
        Stream stream,
        string archiveName,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth >= MaxArchiveDepth)
            return string.Empty;

        if (stream.CanSeek)
            stream.Position = 0;

        var builder = new StringBuilder();
        ZipArchive archive;
        try
        {
            archive = OpenReadZipArchive(stream);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendArchiveParseError(builder, archiveName, archiveName, ex.Message);
            return builder.ToString();
        }

        using (archive)
        {
            var entries = archive.Entries
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Take(MaxArchiveEntries)
                .ToList();

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Length > MaxArchiveEntryBytes || !IsSupportedArchiveEntry(entry.Name))
                    continue;

                var entryPath = NormalizeArchivePath(entry.FullName);
                if (!IsSafeArchiveEntryPath(entryPath))
                {
                    AppendArchiveParseError(builder, archiveName, entryPath, "Unsafe archive entry path was skipped.");
                    continue;
                }

                string entryText;
                try
                {
                    await using var entryStream = entry.Open();
                    await using var entryCopy = await CopyToMemoryWithLimitAsync(entryStream, MaxArchiveEntryBytes, cancellationToken);
                    entryCopy.Position = 0;

                    entryText = await ExtractInternalAsync(
                        entryCopy,
                        entry.Name,
                        GuessContentTypeFromFileName(entry.Name),
                        depth + 1,
                        cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    AppendArchiveParseError(builder, archiveName, entryPath, ex.Message);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entryText))
                    continue;

                if (builder.Length > 0)
                    builder.AppendLine().AppendLine();

                builder.AppendLine($"Archive: {archiveName}");
                builder.AppendLine($"File: {entryPath}");
                builder.Append(entryText);

                if (builder.Length >= MaxExtractedTextLength)
                    break;
            }
        }

        return builder.ToString();
    }

    private static void AppendArchiveParseError(
        StringBuilder builder,
        string archiveName,
        string entryPath,
        string message)
    {
        if (builder.Length > 0)
            builder.AppendLine().AppendLine();

        builder.AppendLine($"Archive: {archiveName}");
        builder.AppendLine($"File: {entryPath}");
        builder.AppendLine($"ParseError: {message}");
    }

    private static string ExtractPdfText(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        var builder = new StringBuilder();
        using var document = PdfDocument.Open(stream);

        foreach (var page in document.GetPages())
        {
            var pageText = ExtractPdfPageText(page);
            var tableMarkdown = ExtractPdfPageTableMarkdown(page);
            if (!LooksLikeHumanText(pageText))
            {
                var words = page.GetWords(NearestNeighbourWordExtractor.Instance)
                    .Select(x => x.Text)
                    .Where(LooksLikeHumanText);
                pageText = string.Join(" ", words);
            }

            if (!string.IsNullOrWhiteSpace(pageText))
            {
                if (builder.Length > 0)
                    builder.AppendLine().AppendLine();

                builder.Append(pageText);
            }

            if (!string.IsNullOrWhiteSpace(tableMarkdown))
            {
                if (builder.Length > 0)
                    builder.AppendLine().AppendLine();

                builder.Append(tableMarkdown);
            }

            if (builder.Length >= MaxExtractedTextLength)
                break;
        }

        return NormalizePdfWhitespace(builder.ToString());
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null)
            return [];

        var document = LoadXml(entry);
        if (document.Root == null)
            return [];

        return document.Root
            .Elements(SpreadsheetNamespace + "si")
            .Select(ReadSharedStringItem)
            .ToList();
    }

    private static string ReadSharedStringItem(XElement item)
    {
        var text = string.Concat(item
            .Descendants(SpreadsheetNamespace + "t")
            .Select(x => x.Value));
        return NormalizeWhitespace(text);
    }

    private static IReadOnlyDictionary<string, string> ReadSheetNamesByPath(ZipArchive archive)
    {
        var workbook = archive.GetEntry("xl/workbook.xml");
        var relationships = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbook == null || relationships == null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var relationshipTargets = LoadXml(relationships)
            .Root?
            .Elements(PackageRelationshipsNamespace + "Relationship")
            .Select(x => new
            {
                Id = (string?)x.Attribute("Id") ?? string.Empty,
                Target = NormalizeWorkbookRelationshipTarget((string?)x.Attribute("Target") ?? string.Empty)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Target))
            .ToDictionary(x => x.Id, x => x.Target, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var workbookDocument = LoadXml(workbook);
        var sheets = workbookDocument.Root?
            .Descendants(SpreadsheetNamespace + "sheet") ?? Enumerable.Empty<XElement>();
        foreach (var sheet in sheets)
        {
            var relationshipId = (string?)sheet.Attribute(OfficeRelationshipsNamespace + "id");
            var sheetName = NormalizeWhitespace((string?)sheet.Attribute("name") ?? string.Empty);
            if (string.IsNullOrWhiteSpace(relationshipId) ||
                string.IsNullOrWhiteSpace(sheetName) ||
                !relationshipTargets.TryGetValue(relationshipId, out var target))
            {
                continue;
            }

            result[target] = sheetName;
        }

        return result;
    }

    private static IReadOnlyList<IReadOnlyList<string>> ExtractWorksheetRows(
        ZipArchiveEntry entry,
        IReadOnlyList<string> sharedStrings)
    {
        var document = LoadXml(entry);
        var rows = document.Root?
            .Descendants(SpreadsheetNamespace + "row") ?? Enumerable.Empty<XElement>();
        var result = new List<IReadOnlyList<string>>();

        foreach (var row in rows)
        {
            var cellsByColumn = new SortedDictionary<int, string>();
            var fallbackColumn = 0;
            foreach (var cell in row.Elements(SpreadsheetNamespace + "c"))
            {
                var columnIndex = TryGetSpreadsheetColumnIndex((string?)cell.Attribute("r"));
                if (columnIndex < 0)
                    columnIndex = fallbackColumn;

                cellsByColumn[columnIndex] = ReadCellText(cell, sharedStrings);
                fallbackColumn = columnIndex + 1;
            }

            if (cellsByColumn.Count == 0 ||
                cellsByColumn.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var maxColumn = cellsByColumn.Keys.Max();
            var cells = Enumerable.Repeat(string.Empty, maxColumn + 1).ToList();
            foreach (var (column, value) in cellsByColumn)
                cells[column] = value;

            result.Add(TrimTrailingEmptyCells(cells));
        }

        return result;
    }

    private static string ReadCellText(
        XElement cell,
        IReadOnlyList<string> sharedStrings)
    {
        var type = ((string?)cell.Attribute("t") ?? string.Empty).Trim();
        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
        {
            var indexText = cell.Element(SpreadsheetNamespace + "v")?.Value;
            return int.TryParse(indexText, out var index) &&
                   index >= 0 &&
                   index < sharedStrings.Count
                ? sharedStrings[index]
                : string.Empty;
        }

        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeWhitespace(string.Concat(cell
                .Descendants(SpreadsheetNamespace + "t")
                .Select(x => x.Value)));
        }

        return NormalizeWhitespace(cell.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty);
    }

    private static string ExtractLegacyExcelText(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = ExcelReaderFactory.CreateReader(
            stream,
            new ExcelReaderConfiguration
            {
                FallbackEncoding = GetChineseEncoding(),
                LeaveOpen = true
            });

        var builder = new StringBuilder();
        var tableIndex = 1;
        do
        {
            var rows = new List<IReadOnlyList<string>>();
            while (reader.Read())
            {
                var cells = new List<string>(reader.FieldCount);
                for (var i = 0; i < reader.FieldCount; i++)
                    cells.Add(FormatExcelCell(reader.GetValue(i)));

                if (cells.Any(x => !string.IsNullOrWhiteSpace(x)))
                    rows.Add(TrimTrailingEmptyCells(cells));
            }

            if (rows.Count == 0)
                continue;

            if (builder.Length > 0)
                builder.AppendLine().AppendLine();

            AppendMarkdownTable(builder, tableIndex, $"Sheet: {reader.Name}", rows);
            tableIndex++;

            if (builder.Length >= MaxExtractedTextLength)
                break;
        } while (reader.NextResult());

        return NormalizePdfWhitespace(builder.ToString());
    }

    private static string FormatExcelCell(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime date => date.TimeOfDay == TimeSpan.Zero
                ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            IFormattable formattable => NormalizeWhitespace(formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty),
            _ => NormalizeWhitespace(value.ToString() ?? string.Empty)
        };
    }

    private static List<string> TrimTrailingEmptyCells(List<string> cells)
    {
        var last = cells.Count - 1;
        while (last >= 0 && string.IsNullOrWhiteSpace(cells[last]))
            last--;

        return last < 0 ? [] : cells.Take(last + 1).ToList();
    }

    private static int TryGetSpreadsheetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
            return -1;

        var index = 0;
        var hasColumn = false;
        foreach (var ch in cellReference.Trim())
        {
            if (!char.IsLetter(ch))
                break;

            index = index * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
            hasColumn = true;
        }

        return hasColumn ? index - 1 : -1;
    }

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        using var entryStream = entry.Open();
        using var reader = XmlReader.Create(entryStream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static async Task<string> ExtractLegacyBinaryTextAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        await using var copy = await CopyToMemoryWithLimitAsync(stream, MaxArchiveEntryBytes, cancellationToken);
        var bytes = copy.ToArray();
        var builder = new StringBuilder();
        AppendAsciiTextRuns(bytes, builder);
        AppendUtf16LeTextRuns(bytes, builder);
        return NormalizePdfWhitespace(builder.ToString());
    }

    private static void AppendAsciiTextRuns(
        byte[] bytes,
        StringBuilder builder)
    {
        var run = new StringBuilder();
        foreach (var value in bytes)
        {
            if (IsReadableAscii(value))
            {
                run.Append((char)value);
                continue;
            }

            FlushTextRun(run, builder);
        }

        FlushTextRun(run, builder);
    }

    private static void AppendUtf16LeTextRuns(
        byte[] bytes,
        StringBuilder builder)
    {
        var run = new StringBuilder();
        for (var i = 0; i + 1 < bytes.Length; i += 2)
        {
            var value = bytes[i] | (bytes[i + 1] << 8);
            var ch = (char)value;
            if (IsReadableUnicode(ch))
            {
                run.Append(ch);
                continue;
            }

            FlushTextRun(run, builder);
        }

        FlushTextRun(run, builder);
    }

    private static bool IsReadableAscii(byte value)
    {
        return value is >= 0x20 and <= 0x7E or 0x09 or 0x0A or 0x0D;
    }

    private static bool IsReadableUnicode(char value)
    {
        return value == '\t' ||
               value == '\n' ||
               value == '\r' ||
               value >= 0x20 && !char.IsControl(value) && !char.IsSurrogate(value);
    }

    private static void FlushTextRun(
        StringBuilder run,
        StringBuilder builder)
    {
        if (run.Length >= MinLegacyTextRunLength)
        {
            var text = NormalizeWhitespace(run.ToString());
            if (!string.IsNullOrWhiteSpace(text) && LooksLikeHumanText(text))
            {
                if (builder.Length > 0)
                    builder.AppendLine();

                builder.Append(text);
            }
        }

        run.Clear();
    }

    private static string ExtractPdfPageText(Page page)
    {
        var options = new ContentOrderTextExtractor.Options
        {
            SeparateParagraphsWithDoubleNewline = false,
            ReplaceWhitespaceWithSpace = false,
            NegativeGapAsWhitespace = true
        };

        return NormalizePdfWhitespace(ContentOrderTextExtractor.GetText(page, options));
    }

    private static string ExtractPdfPageTableMarkdown(Page page)
    {
        var words = page.GetWords(NearestNeighbourWordExtractor.Instance)
            .Select(PdfWordBox.FromWord)
            .Where(x => LooksLikePdfTableWord(x.Text))
            .ToList();
        if (words.Count < 4)
            return string.Empty;

        var tableRows = BuildPdfTableRows(BuildPdfRows(words))
            .Take(MaxPdfTableRowsPerPage)
            .ToList();
        if (tableRows.Count < 2)
            return string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine($"PDF 表格结构 Page {page.Number}");

        var header = tableRows.FirstOrDefault(x => LooksLikePdfTableHeader(x));
        var columnCount = Math.Max(3, tableRows.Max(x => x.Cells.Count));
        var headers = header?.Cells.Count >= 3
            ? NormalizeHeaderCells(header.Cells, columnCount)
            : Enumerable.Range(1, columnCount).Select(x => $"列{x.ToString(CultureInfo.InvariantCulture)}").ToList();

        AppendMarkdownRow(builder, headers);
        AppendMarkdownRow(builder, headers.Select(_ => "---"));

        foreach (var row in tableRows.Where(x => !ReferenceEquals(x, header)))
        {
            AppendMarkdownRow(builder, PadCells(row.Cells, columnCount));
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<PdfTableRow> BuildPdfTableRows(IReadOnlyList<IReadOnlyList<PdfWordBox>> wordRows)
    {
        var physicalRows = wordRows
            .Select(BuildPdfTableRow)
            .Where(x => x.Cells.Count > 0)
            .ToList();
        if (physicalRows.Count == 0)
            return [];

        var headerIndex = physicalRows.FindIndex(LooksLikePdfTableHeader);
        if (headerIndex < 0)
        {
            return physicalRows
                .Where(x => x.Cells.Count >= 3 && LooksLikePdfTableRow(x))
                .ToList();
        }

        var header = physicalRows[headerIndex];
        var columnCount = header.CellBoxes.Count;
        var normalizedHeaders = NormalizeHeaderCells(header.Cells, columnCount);
        var logicalRows = new List<string[]> { normalizedHeaders.ToArray() };
        string[]? current = null;

        foreach (var row in physicalRows.Skip(headerIndex + 1))
        {
            var assigned = AssignToPdfColumns(row, header.CellBoxes);
            if (assigned.All(string.IsNullOrWhiteSpace))
                continue;

            if (IsRepeatedHeaderRow(assigned, normalizedHeaders))
                continue;

            var startsNewRecord = StartsNewPdfTableRecord(assigned, normalizedHeaders, current == null);
            if (startsNewRecord || current == null)
            {
                current = assigned.ToArray();
                logicalRows.Add(current);
                continue;
            }

            for (var i = 0; i < current.Length; i++)
            {
                current[i] = MergePdfContinuationCell(current[i], assigned[i]);
            }
        }

        return logicalRows
            .Select(x => CreatePdfTableRow(x, header.CellBoxes))
            .Where((x, index) => index == 0 || LooksLikePdfTableRow(x))
            .ToList();
    }

    private static IReadOnlyList<IReadOnlyList<PdfWordBox>> BuildPdfRows(IReadOnlyList<PdfWordBox> words)
    {
        var medianHeight = Median(words.Select(x => x.Height).Where(x => x > 0));
        var rowTolerance = Math.Max(3d, medianHeight * 0.55d);
        var rows = new List<List<PdfWordBox>>();

        foreach (var word in words.OrderByDescending(x => x.CenterY).ThenBy(x => x.Left))
        {
            var row = rows.FirstOrDefault(x => Math.Abs(x.Average(item => item.CenterY) - word.CenterY) <= rowTolerance);
            if (row == null)
            {
                row = [];
                rows.Add(row);
            }

            row.Add(word);
        }

        return rows
            .Select(x => (IReadOnlyList<PdfWordBox>)x.OrderBy(item => item.Left).ToList())
            .ToList();
    }

    private static PdfTableRow BuildPdfTableRow(IReadOnlyList<PdfWordBox> row)
    {
        var medianHeight = Median(row.Select(x => x.Height).Where(x => x > 0));
        var gapThreshold = Math.Max(10d, medianHeight * 0.85d);
        var cells = new List<PdfTableCell>();
        var current = new StringBuilder();
        PdfWordBox? previous = null;
        double currentLeft = 0;
        double currentRight = 0;

        foreach (var word in row)
        {
            if (previous != null && word.Left - previous.Right >= gapThreshold)
            {
                AddCell(cells, current, currentLeft, currentRight);
            }

            if (current.Length == 0)
                currentLeft = word.Left;

            if (current.Length > 0)
                current.Append(' ');
            current.Append(word.Text);
            currentRight = word.Right;
            previous = word;
        }

        AddCell(cells, current, currentLeft, currentRight);
        return new PdfTableRow(cells.Select(x => x.Text).ToList(), cells);
    }

    private static void AddCell(
        List<PdfTableCell> cells,
        StringBuilder current,
        double left,
        double right)
    {
        var value = NormalizePdfTableCell(current.ToString());
        if (!string.IsNullOrWhiteSpace(value))
            cells.Add(new PdfTableCell(value, left, right));
        current.Clear();
    }

    private static string[] AssignToPdfColumns(PdfTableRow row, IReadOnlyList<PdfTableCell> columns)
    {
        var assigned = Enumerable.Repeat(string.Empty, columns.Count).ToArray();
        foreach (var cell in row.CellBoxes)
        {
            var index = FindPdfColumnIndex(cell.Center, columns);
            assigned[index] = MergePdfContinuationCell(assigned[index], cell.Text);
        }

        return assigned.Select(NormalizePdfTableCell).ToArray();
    }

    private static int FindPdfColumnIndex(double center, IReadOnlyList<PdfTableCell> columns)
    {
        for (var i = 0; i < columns.Count - 1; i++)
        {
            var boundary = (columns[i].Center + columns[i + 1].Center) / 2d;
            if (center < boundary)
                return i;
        }

        return columns.Count - 1;
    }

    private static bool StartsNewPdfTableRecord(
        IReadOnlyList<string> cells,
        IReadOnlyList<string> headers,
        bool isFirstDataRow)
    {
        var nonEmptyCount = cells.Count(x => !string.IsNullOrWhiteSpace(x));
        if (isFirstDataRow)
            return nonEmptyCount >= 2;

        var sequenceIndex = FindPdfHeaderIndex(headers, "序号", "no", "row");
        if (sequenceIndex >= 0 &&
            Regex.IsMatch(cells[sequenceIndex], @"^\d{1,4}$", RegexOptions.CultureInvariant))
        {
            return true;
        }

        var row = CreatePdfTableRow(cells);
        return nonEmptyCount >= 3 && LooksLikePdfTableRow(row);
    }

    private static int FindPdfHeaderIndex(IReadOnlyList<string> headers, params string[] tokens)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            if (ContainsAny(headers[i], tokens))
                return i;
        }

        return -1;
    }

    private static PdfTableRow CreatePdfTableRow(IReadOnlyList<string> cells)
    {
        var boxes = cells
            .Select((x, index) => new PdfTableCell(NormalizePdfTableCell(x), index, index + 1d))
            .ToList();
        return new PdfTableRow(boxes.Select(x => x.Text).ToList(), boxes);
    }

    private static PdfTableRow CreatePdfTableRow(
        IReadOnlyList<string> cells,
        IReadOnlyList<PdfTableCell> columns)
    {
        var boxes = cells
            .Select((x, index) =>
            {
                var column = index < columns.Count
                    ? columns[index]
                    : new PdfTableCell(string.Empty, index, index + 1d);
                return new PdfTableCell(NormalizePdfTableCell(x), column.Left, column.Right);
            })
            .ToList();
        return new PdfTableRow(boxes.Select(x => x.Text).ToList(), boxes);
    }

    private static string MergePdfContinuationCell(string existing, string addition)
    {
        existing = NormalizePdfTableCell(existing);
        addition = NormalizePdfTableCell(addition);
        if (string.IsNullOrWhiteSpace(existing))
            return addition;
        if (string.IsNullOrWhiteSpace(addition))
            return existing;

        return ShouldJoinPdfContinuationWithoutSpace(existing, addition)
            ? existing + addition
            : $"{existing} {addition}";
    }

    private static bool ShouldJoinPdfContinuationWithoutSpace(string left, string right)
    {
        if (ContainsCjk(left[^1]) || ContainsCjk(right[0]))
            return true;

        return LooksLikePdfCodeFragment(left) || LooksLikePdfCodeFragment(right);
    }

    private static bool LooksLikePdfCodeFragment(string value)
    {
        var text = value.Trim();
        if (text.Contains(' '))
            return false;

        var hasDigit = text.Any(char.IsDigit);
        if (!hasDigit)
            return false;

        return text.Contains('-') ||
               text.Contains('_') ||
               text.Any(char.IsLetter) && text.Any(char.IsDigit);
    }

    private static bool ContainsCjk(char value)
    {
        return value >= '\u4e00' && value <= '\u9fff';
    }

    private static bool LooksLikePdfTableWord(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 0 &&
               (trimmed.Any(char.IsLetterOrDigit) ||
                trimmed.Any(ch => ch >= '\u4e00' && ch <= '\u9fff'));
    }

    private static bool LooksLikePdfTableRow(PdfTableRow row)
    {
        var text = string.Join(' ', row.Cells);
        if (LooksLikePdfTableHeader(row))
            return true;

        return ContainsAny(text, "包", "分标", "标段", "中标", "成交", "供应商", "中标人", "成交人", "投标人", "lot", "package", "supplier", "winner") ||
               Regex.IsMatch(text, @"[A-Za-z0-9]{3,}(?:[-_/][A-Za-z0-9]{2,}){1,}", RegexOptions.CultureInvariant);
    }

    private static bool LooksLikePdfTableHeader(PdfTableRow row)
    {
        var text = string.Join(' ', row.Cells);
        return ContainsAny(text, "分标编号", "标段编号", "分标名称", "标段名称", "lot no", "lot name", "lotno", "lotname") &&
               ContainsAny(text, "包号", "包件", "分包", "标包", "package no", "packageno", "package") &&
               ContainsAny(text, "中标人", "成交人", "供应商", "投标人", "supplier", "winner", "bidder");
    }

    private static IReadOnlyList<string> NormalizeHeaderCells(IReadOnlyList<string> cells, int columnCount)
    {
        return PadCells(cells.Select(x => x switch
        {
            var value when ContainsAny(value, "分标编号", "标段编号", "lot no", "lotno") => "分标编号",
            var value when ContainsAny(value, "分标名称", "标段名称", "lot name", "lotname") => "分标名称",
            var value when ContainsAny(value, "包号", "包件", "分包", "标包", "package no", "packageno", "package") => "包号",
            var value when ContainsAny(value, "中标人", "成交人", "供应商", "投标人", "supplier", "winner", "bidder") => "成交供应商",
            _ => x
        }).ToList(), columnCount);
    }

    private static IReadOnlyList<string> PadCells(IEnumerable<string> cells, int columnCount)
    {
        var result = cells.Select(NormalizePdfTableCell).ToList();
        while (result.Count < columnCount)
            result.Add(string.Empty);
        return result.Count <= columnCount ? result : result.Take(columnCount).ToList();
    }

    private static string NormalizePdfTableCell(string value)
    {
        return NormalizeWhitespace(value).Trim();
    }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.Where(x => !double.IsNaN(x) && !double.IsInfinity(x) && x > 0).OrderBy(x => x).ToArray();
        if (ordered.Length == 0)
            return 10d;

        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2d;
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeHumanText(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length < 2)
            return false;

        return trimmed.Any(char.IsLetterOrDigit) ||
               trimmed.Any(ch => ch >= '\u4e00' && ch <= '\u9fff');
    }

    private static string NormalizeWhitespace(string value)
    {
        return WhitespaceRegex().Replace(value, " ").Trim();
    }

    private static string NormalizePdfWhitespace(string value)
    {
        var normalized = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace('\u00a0', ' ');

        normalized = PdfHorizontalControlWhitespaceRegex().Replace(normalized, " ");
        normalized = PdfSpacesBeforeLineBreakRegex().Replace(normalized, "\n");
        normalized = PdfBlankLinesRegex().Replace(normalized, "\n\n");

        return normalized.Trim();
    }

    private static string Trim(string value)
    {
        return value.Length <= MaxExtractedTextLength ? value : value[..MaxExtractedTextLength];
    }

    private static string NormalizeWorkbookRelationshipTarget(string target)
    {
        var normalized = NormalizeArchivePath(target);
        if (normalized.StartsWith('/'))
            normalized = normalized.TrimStart('/');
        if (!normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            normalized = $"xl/{normalized}";

        return normalized;
    }

    private static string NormalizeArchivePath(string value)
    {
        return value.Replace('\\', '/').Trim();
    }

    private static bool IsSafeArchiveEntryPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.StartsWith("/", StringComparison.Ordinal) ||
            value.Contains(':'))
        {
            return false;
        }

        return value
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(part => part != "." && part != "..");
    }

    private static ZipArchive OpenReadZipArchive(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        if (!ArchiveEntryNamesLookGarbled(archive))
            return archive;

        archive.Dispose();
        if (stream.CanSeek)
            stream.Position = 0;

        return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true, entryNameEncoding: GetChineseEncoding());
    }

    private static bool ArchiveEntryNamesLookGarbled(ZipArchive archive)
    {
        return archive.Entries
            .Take(20)
            .Select(x => x.FullName)
            .Any(LooksLikeMojibakeArchiveName);
    }

    private static bool LooksLikeMojibakeArchiveName(string value)
    {
        if (value.Contains('\uFFFD', StringComparison.Ordinal))
            return true;

        var suspicious = 0;
        foreach (var ch in value)
        {
            if (ch is >= '\u2500' and <= '\u257F' or 'Ã' or 'Â' or 'Ä' or 'Å' or 'Ð' or 'Ñ' or 'Ö' or '×' or 'Ê' or 'Ë' or '¾' or '¼')
                suspicious++;
        }

        return suspicious >= 2;
    }

    private static Encoding GetChineseEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding("GB18030");
    }

    private static string NormalizeHeaderText(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch) ||
                ch is '/' or '／' or '|' or ':' or '：' or '-' or '_' or '（' or '）' or '(' or ')')
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }

    private static bool IsSupportedArchiveEntry(string fileName)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        return extension is "txt" or "csv" or "html" or "htm" or "docx" or "doc" or "xlsx" or "xlsm" or "xltx" or "xltm" or "xls" or "pdf" or "zip";
    }

    private static string GuessContentTypeFromFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        return extension switch
        {
            "pdf" => "application/pdf",
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "doc" => "application/msword",
            "xlsx" or "xlsm" or "xltx" or "xltm" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "xls" => "application/vnd.ms-excel",
            "zip" => "application/zip",
            "html" or "htm" => "text/html",
            "txt" or "csv" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private static async Task<MemoryStream> CopyToMemoryWithLimitAsync(
        Stream source,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        var target = new MemoryStream();
        var buffer = new byte[81920];
        var total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
                break;

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        target.Position = 0;
        return target;
    }

    private sealed record PdfTableRow(
        IReadOnlyList<string> Cells,
        IReadOnlyList<PdfTableCell> CellBoxes);

    private sealed record PdfTableCell(
        string Text,
        double Left,
        double Right)
    {
        public double Center => (Left + Right) / 2d;
    }

    private sealed record PdfWordBox(
        string Text,
        double Left,
        double Right,
        double CenterY,
        double Height)
    {
        public static PdfWordBox FromWord(Word word)
        {
            var box = word.BoundingBox;
            return new PdfWordBox(
                word.Text,
                box.Left,
                box.Right,
                (box.Bottom + box.Top) / 2d,
                Math.Abs(box.Top - box.Bottom));
        }
    }

    [GeneratedRegex("<script\\b[^>]*>.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptRegex();

    [GeneratedRegex("<style\\b[^>]*>.*?</style>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex("</w:p>", RegexOptions.IgnoreCase)]
    private static partial Regex ParagraphCloseRegex();

    [GeneratedRegex("\\s+", RegexOptions.Singleline)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("[\\t\\f\\v]+")]
    private static partial Regex PdfHorizontalControlWhitespaceRegex();

    [GeneratedRegex(" +\\n")]
    private static partial Regex PdfSpacesBeforeLineBreakRegex();

    [GeneratedRegex("\\n{3,}")]
    private static partial Regex PdfBlankLinesRegex();
}
