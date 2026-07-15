using Atlas.Core.Authorization;
using Atlas.Core.Exceptions;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsNoticeService : IBidOpsNoticeService
{
    private readonly IRepository<Notice> _notices;
    private readonly IUnitOfWork _unitOfWork;

    public BidOpsNoticeService(IRepository<Notice> notices, IUnitOfWork unitOfWork)
    {
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task UpdateAsync(long id, UpdateNoticeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.BudgetAmount < 0)
            throw new AtlasException("公告预算金额不能小于 0。");

        var builder = await _notices.QueryDataScopeTrackingAsync(
            BidOpsDataResources.Notice,
            AtlasDataScopeType.AllTenant,
            ct);
        var notice = await builder.Where(x => x.Id == id).FirstOrDefaultAsync(ct)
            ?? throw new AtlasException($"BidOps notice does not exist: {id}");

        // 正式公告允许人工修正，但不回写 Raw/Staging，避免破坏原始证据和审核轨迹。
        notice.Title = CleanRequired(request.Title, "公告标题", 500);
        notice.NoticeType = CleanRequired(request.NoticeType, "公告类型", 64);
        notice.ProjectName = CleanRequired(request.ProjectName, "项目名称", 500);
        notice.ProjectCode = CleanOptional(request.ProjectCode, 128);
        notice.BuyerName = CleanOptional(request.BuyerName, 300);
        notice.AgencyName = CleanOptional(request.AgencyName, 300);
        notice.Region = CleanOptional(request.Region, 128);
        notice.BudgetAmount = request.BudgetAmount;
        notice.PublishTime = request.PublishTime;
        notice.SignupDeadline = request.SignupDeadline;
        notice.BidDeadline = request.BidDeadline;
        notice.OpenBidTime = request.OpenBidTime;
        notice.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static string CleanRequired(string? value, string fieldName, int maxLength)
    {
        var cleaned = CleanOptional(value, maxLength);
        if (string.IsNullOrWhiteSpace(cleaned))
            throw new AtlasException($"{fieldName}不能为空或全为乱码占位符。");

        return cleaned;
    }

    private static string CleanOptional(string? value, int maxLength)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
