using Atlas.Modules.BidOps.Entities.Crawling;

namespace Atlas.Modules.BidOps.Crawling;

public interface IBidOpsCrawlAdapter
{
    string SourceType { get; }

    string AdapterCode { get; }

    bool SupportsInlineHtmlTables { get; }

    bool SupportsAttachmentDiscovery { get; }

    IReadOnlyCollection<string> SupportedAttachmentTypes { get; }

    bool CanHandle(CrawlSource source);

    bool CanImportDetail(Uri detailUri);
}
