using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Atlas.Modules.BidOps.Documents;

public sealed partial class BidOpsTextExtractor : IBidOpsTextExtractor
{
    private const int MaxExtractedTextLength = 500_000;

    public async Task<string> ExtractAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
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

        if (extension == "pdf" ||
            normalizedContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Trim(ExtractPdfText(stream));
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
        var document = archive.GetEntry("word/document.xml");
        if (document == null)
            return string.Empty;

        using var entryStream = document.Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var xml = reader.ReadToEnd();
        xml = ParagraphCloseRegex().Replace(xml, "\n");
        xml = TagRegex().Replace(xml, " ");
        return NormalizeWhitespace(WebUtility.HtmlDecode(xml));
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
