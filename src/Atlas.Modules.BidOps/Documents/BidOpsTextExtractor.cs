using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

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
            return Trim(await ExtractPdfTextAsync(stream, cancellationToken));
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

    private static async Task<string> ExtractPdfTextAsync(
        Stream stream,
        CancellationToken ct)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var latin = Encoding.Latin1.GetString(bytes);
        var builder = new StringBuilder();

        foreach (Match match in PdfLiteralStringRegex().Matches(latin))
        {
            var text = DecodePdfLiteral(match.Groups["text"].Value);
            if (LooksLikeHumanText(text))
                builder.AppendLine(text);

            if (builder.Length >= MaxExtractedTextLength)
                break;
        }

        if (builder.Length == 0)
        {
            foreach (Match match in PdfUtf16HexRegex().Matches(latin))
            {
                var text = DecodePdfUtf16Hex(match.Groups["hex"].Value);
                if (LooksLikeHumanText(text))
                    builder.AppendLine(text);

                if (builder.Length >= MaxExtractedTextLength)
                    break;
            }
        }

        return NormalizeWhitespace(builder.ToString());
    }

    private static string DecodePdfLiteral(string value)
    {
        return value
            .Replace("\\(", "(", StringComparison.Ordinal)
            .Replace("\\)", ")", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\n", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static string DecodePdfUtf16Hex(string hex)
    {
        try
        {
            if (hex.Length % 2 != 0)
                return string.Empty;

            var bytes = Convert.FromHexString(hex);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
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

    [GeneratedRegex("\\((?<text>(?:\\\\.|[^\\\\)]){2,})\\)")]
    private static partial Regex PdfLiteralStringRegex();

    [GeneratedRegex("<(?<hex>FEFF[0-9A-Fa-f]{4,})>")]
    private static partial Regex PdfUtf16HexRegex();

    [GeneratedRegex("\\s+", RegexOptions.Singleline)]
    private static partial Regex WhitespaceRegex();
}
