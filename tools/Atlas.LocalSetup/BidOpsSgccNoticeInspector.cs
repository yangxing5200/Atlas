using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.Crawling;
using Atlas.Modules.BidOps.Documents;
using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

internal static class BidOpsSgccNoticeInspector
{
    private static readonly string[] DefaultUrls =
    [
        "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2606028335014767_2018032900295987",
        "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2605288211018925_2018032900295987",
        "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2605268192348756_2018032900295987"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static async Task RunAsync(string[] args)
    {
        var urls = GetOptionValues(args, "--url")
            .Concat(args.Skip(1).Where(x => x.StartsWith("http", StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (urls.Count == 0)
            urls.AddRange(DefaultUrls);

        var packageTake = TryGetIntOption(args, "--package-take") ?? 20;
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 Atlas.LocalSetup BidOps inspector");
        httpClient.DefaultRequestHeaders.Referrer = new Uri("https://ecp.sgcc.com.cn/ecp2.0/portal/");

        var reports = new List<SgccNoticeInspectionReport>();
        foreach (var url in urls)
            reports.Add(await InspectAsync(httpClient, url, packageTake));

        Console.WriteLine(JsonSerializer.Serialize(reports, JsonOptions));
    }

    private static async Task<SgccNoticeInspectionReport> InspectAsync(
        HttpClient httpClient,
        string detailUrl,
        int packageTake)
    {
        if (!StateGridEcpWcmParser.TryParsePortalDetailUrl(detailUrl, out var doctype, out var noticeId, out var menuId))
            throw new InvalidOperationException($"Invalid SGCC ECP detail URL: {detailUrl}");

        var notice = new StateGridEcpApiNotice(
            string.Empty,
            detailUrl,
            doctype,
            menuId,
            noticeId,
            null,
            null,
            string.Empty,
            string.Empty);

        var detailJson = await PostJsonAsync(
            httpClient,
            BuildApiUri(StateGridEcpWcmParser.GetDetailApiPath(doctype)),
            JsonSerializer.Serialize(noticeId, JsonOptions));
        var document = StateGridEcpWcmParser.ParseNoticeDetail(detailJson, notice);

        var extractor = new BidOpsTextExtractor();
        var attachmentReports = new List<SgccAttachmentInspectionReport>();
        var extractedTexts = new List<string>();
        foreach (var attachment in document.Attachments)
        {
            var bytes = await DownloadBytesAsync(httpClient, attachment.FileUrl);
            await using var content = new MemoryStream(bytes);
            var text = await extractor.ExtractAsync(
                content,
                attachment.FileName,
                GuessContentType(attachment.FileName, attachment.FileType));

            attachmentReports.Add(new SgccAttachmentInspectionReport(
                attachment.FileName,
                attachment.FileType,
                attachment.FileSize,
                bytes.LongLength,
                text.Length,
                ListArchiveEntries(bytes, attachment.FileName)));
            extractedTexts.Add(text);
        }

        var attachmentText = string.Join(Environment.NewLine + Environment.NewLine, extractedTexts);
        var sourceText = string.IsNullOrWhiteSpace(attachmentText)
            ? document.Text
            : $"{document.Text}{Environment.NewLine}{Environment.NewLine}{attachmentText}";

        var extract = BidOpsDeterministicNoticeParser.Extract(document.Title, sourceText);
        var packages = extract.Packages
            .Select(x => new SgccPackageInspectionReport(
                x.LotNo,
                x.LotName,
                x.PackageNo,
                x.PackageName,
                x.Category,
                x.Quantity,
                x.Unit,
                x.BudgetAmount,
                x.MaxPrice,
                x.DeliveryPlace,
                x.DeliveryPeriod,
                x.Requirements.Count))
            .ToList();

        return new SgccNoticeInspectionReport(
            detailUrl,
            noticeId,
            doctype,
            document.Title,
            extract.ProjectCode,
            extract.BuyerName,
            extract.AgencyName,
            extract.Region,
            document.PublishTime ?? extract.PublishTime,
            document.Attachments.Count,
            attachmentReports,
            packages.Count,
            packages.Count(x => x.BudgetAmount.HasValue || x.MaxPrice.HasValue),
            packages.Sum(x => x.RequirementCount),
            packages.Take(Math.Max(1, packageTake)).ToList());
    }

    private static async Task<string> PostJsonAsync(
        HttpClient httpClient,
        Uri uri,
        string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<byte[]> DownloadBytesAsync(
        HttpClient httpClient,
        string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static IReadOnlyList<SgccArchiveEntryInspectionReport> ListArchiveEntries(
        byte[] bytes,
        string archiveName)
    {
        if (!string.Equals(Path.GetExtension(archiveName), ".zip", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<SgccArchiveEntryInspectionReport>();

        var entries = new List<SgccArchiveEntryInspectionReport>();
        ListArchiveEntries(bytes, archiveName, depth: 0, entries);
        return entries;
    }

    private static void ListArchiveEntries(
        byte[] bytes,
        string archiveName,
        int depth,
        List<SgccArchiveEntryInspectionReport> entries)
    {
        if (depth >= 3)
            return;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false, entryNameEncoding: Encoding.GetEncoding("GB18030"));
        foreach (var entry in archive.Entries.Where(x => !string.IsNullOrWhiteSpace(x.Name)).Take(100))
        {
            entries.Add(new SgccArchiveEntryInspectionReport(depth, NormalizeArchivePath(entry.FullName), entry.Length));
            if (!string.Equals(Path.GetExtension(entry.Name), ".zip", StringComparison.OrdinalIgnoreCase) ||
                entry.Length > 20 * 1024 * 1024)
            {
                continue;
            }

            using var entryStream = entry.Open();
            using var copy = new MemoryStream();
            entryStream.CopyTo(copy);
            ListArchiveEntries(copy.ToArray(), entry.Name, depth + 1, entries);
        }
    }

    private static Uri BuildApiUri(string apiPath)
    {
        return new Uri($"https://ecp.sgcc.com.cn/ecp2.0/ecpwcmcore/{apiPath.TrimStart('/')}");
    }

    private static string GuessContentType(string fileName, string fileType)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
            extension = fileType.Trim().ToLowerInvariant();

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

    private static IReadOnlyList<string> GetOptionValues(string[] args, string name)
    {
        var values = new List<string>();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                values.Add(args[i + 1]);
        }

        return values;
    }

    private static int? TryGetIntOption(string[] args, string name)
    {
        var value = GetOptionValues(args, name).LastOrDefault();
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static string NormalizeArchivePath(string value)
    {
        return value.Replace('\\', '/').Trim();
    }

    private sealed record SgccNoticeInspectionReport(
        string Url,
        long NoticeId,
        string Doctype,
        string Title,
        string ProjectCode,
        string BuyerName,
        string AgencyName,
        string Region,
        DateTime? PublishTime,
        int AttachmentCount,
        IReadOnlyList<SgccAttachmentInspectionReport> Attachments,
        int PackageCount,
        int PackagesWithAmountReference,
        int RequirementCount,
        IReadOnlyList<SgccPackageInspectionReport> PackageSamples);

    private sealed record SgccAttachmentInspectionReport(
        string FileName,
        string FileType,
        long? DeclaredSize,
        long DownloadedBytes,
        int ExtractedTextLength,
        IReadOnlyList<SgccArchiveEntryInspectionReport> ArchiveEntries);

    private sealed record SgccArchiveEntryInspectionReport(
        int Depth,
        string Path,
        long Size);

    private sealed record SgccPackageInspectionReport(
        string LotNo,
        string LotName,
        string PackageNo,
        string PackageName,
        string Category,
        decimal? Quantity,
        string Unit,
        decimal? BudgetAmount,
        decimal? MaxPrice,
        string DeliveryPlace,
        string DeliveryPeriod,
        int RequirementCount);
}
