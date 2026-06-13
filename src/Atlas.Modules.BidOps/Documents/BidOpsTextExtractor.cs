using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Atlas.Modules.BidOps.Documents;

public sealed partial class BidOpsTextExtractor : IBidOpsTextExtractor
{
    private const int MaxExtractedTextLength = 500_000;
    private const int MaxArchiveDepth = 3;
    private const int MaxArchiveEntries = 100;
    private const int MaxArchiveEntryBytes = 20 * 1024 * 1024;
    private const int MinLegacyTextRunLength = 4;

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

        if (extension is "doc" or "xls" ||
            normalizedContentType.Contains("msword", StringComparison.OrdinalIgnoreCase) ||
            normalizedContentType.Contains("vnd.ms-excel", StringComparison.OrdinalIgnoreCase))
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
                     "分标"
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

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var sharedStrings = ReadSharedStrings(archive);
        var sheetNamesByPath = ReadSheetNamesByPath(archive);
        var builder = new StringBuilder();

        foreach (var entry in archive.Entries
                     .Where(x => x.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) &&
                                 x.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase))
        {
            var sheetText = ExtractWorksheetText(entry, sharedStrings);
            if (string.IsNullOrWhiteSpace(sheetText))
                continue;

            if (builder.Length > 0)
                builder.AppendLine().AppendLine();

            var normalizedPath = NormalizeArchivePath(entry.FullName);
            var sheetName = sheetNamesByPath.TryGetValue(normalizedPath, out var name)
                ? name
                : Path.GetFileNameWithoutExtension(entry.Name);
            builder.AppendLine($"Sheet: {sheetName}");
            builder.Append(sheetText);

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

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var builder = new StringBuilder();
        var entries = archive.Entries
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
            .Take(MaxArchiveEntries)
            .ToList();

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Length > MaxArchiveEntryBytes || !IsSupportedArchiveEntry(entry.Name))
                continue;

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
                continue;
            }

            if (string.IsNullOrWhiteSpace(entryText))
                continue;

            if (builder.Length > 0)
                builder.AppendLine().AppendLine();

            builder.AppendLine($"Archive: {archiveName}");
            builder.AppendLine($"File: {NormalizeArchivePath(entry.FullName)}");
            builder.Append(entryText);

            if (builder.Length >= MaxExtractedTextLength)
                break;
        }

        return builder.ToString();
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

    private static string ExtractWorksheetText(
        ZipArchiveEntry entry,
        IReadOnlyList<string> sharedStrings)
    {
        var document = LoadXml(entry);
        var rows = document.Root?
            .Descendants(SpreadsheetNamespace + "row") ?? Enumerable.Empty<XElement>();
        var builder = new StringBuilder();

        foreach (var row in rows)
        {
            var cells = row
                .Elements(SpreadsheetNamespace + "c")
                .Select(x => ReadCellText(x, sharedStrings))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (cells.Count == 0)
                continue;

            if (builder.Length > 0)
                builder.AppendLine();

            builder.AppendJoin('\t', cells);
            if (builder.Length >= MaxExtractedTextLength)
                break;
        }

        return builder.ToString();
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
