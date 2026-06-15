using Atlas.Modules.BidOps.Entities.Crawling;

namespace Atlas.Modules.BidOps.Crawling;

public sealed class StateGridEcpCrawlAdapter : IBidOpsCrawlAdapter
{
    private static readonly string[] AttachmentTypes =
    [
        "pdf",
        "doc",
        "docx",
        "xls",
        "xlsx",
        "xlsm",
        "zip",
        "rar",
        "txt",
        "html",
        "htm"
    ];

    public string SourceType => BidOpsCrawlSourceTypes.StateGridEcp;

    public string AdapterCode => "state-grid-ecp-wcm";

    public bool SupportsInlineHtmlTables => true;

    public bool SupportsAttachmentDiscovery => true;

    public IReadOnlyCollection<string> SupportedAttachmentTypes => AttachmentTypes;

    public bool CanHandle(CrawlSource source)
    {
        return string.Equals(source.SourceType, SourceType, StringComparison.OrdinalIgnoreCase) &&
               !source.NeedLogin &&
               Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var uri) &&
               uri.Host.EndsWith("sgcc.com.cn", StringComparison.OrdinalIgnoreCase);
    }

    public bool CanImportDetail(Uri detailUri)
    {
        return detailUri.Scheme is "http" or "https" &&
               detailUri.Host.EndsWith("sgcc.com.cn", StringComparison.OrdinalIgnoreCase) &&
               StateGridEcpWcmParser.TryParsePortalDetailUrl(detailUri.ToString(), out _, out _, out _);
    }
}
