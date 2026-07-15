using System.Linq.Expressions;
using System.Reflection;
using Atlas.Core.Authorization;
using Atlas.Core.Exceptions;
using Atlas.Data.Abstractions;
using Atlas.Extensions.DependencyInjection;
using Atlas.Infrastructure.Security;
using Atlas.Modules.BidOps;
using Atlas.Modules.BidOps.Controllers;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Atlas.Services.Tests;

public sealed class BidOpsNoticeEditingTests
{
    [Fact]
    public void NoticeEditingContracts_DeclareManagePermissionRouteAndFields()
    {
        var module = new BidOpsModule();
        var authorizationBuilder = new AtlasAuthorizationCatalogBuilder("BidOpsNoticeEditingTests");
        module.ConfigureAuthorization(authorizationBuilder);
        var catalog = authorizationBuilder.Build();

        var services = new ServiceCollection();
        module.AddServices(new AtlasModuleContext(services, new ConfigurationBuilder().Build(), module));

        var updateMethod = typeof(NoticesController).GetMethod(nameof(NoticesController.UpdateAsync));
        var route = updateMethod?
            .GetCustomAttributes<HttpPutAttribute>()
            .SingleOrDefault()?
            .Template;
        var requestType = updateMethod?
            .GetParameters()
            .Single(x => x.GetCustomAttribute<FromBodyAttribute>() != null)
            .ParameterType;

        Assert.Equal("{id:long}", route);
        Assert.Equal(typeof(UpdateNoticeRequest), requestType);
        Assert.Contains(
            updateMethod!.GetCustomAttributes<AuthorizeAttribute>(),
            attribute => attribute.Policy == AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.BusinessManage);
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.BusinessManage));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IBidOpsNoticeService));
        Assert.NotNull(typeof(NoticeDto).GetProperty(nameof(NoticeDto.AgencyName)));
        Assert.NotNull(typeof(NoticeDto).GetProperty(nameof(NoticeDto.SignupDeadline)));
        Assert.NotNull(typeof(NoticeDto).GetProperty(nameof(NoticeDto.OpenBidTime)));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEditableFormalFieldsWithinNoticeDataScope()
    {
        var originalTime = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var notice = new Notice
        {
            Id = 101,
            TenantId = 300001,
            Title = "原公告",
            NoticeType = "TenderAnnouncement",
            ProjectName = "原项目",
            CreatedAt = originalTime
        };
        var query = new Mock<IQueryBuilder<Notice>>();
        query
            .Setup(x => x.Where(It.IsAny<Expression<Func<Notice, bool>>>()))
            .Returns(query.Object);
        query
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(notice);

        var repository = new Mock<IRepository<Notice>>();
        repository
            .Setup(x => x.QueryDataScopeTrackingAsync(
                BidOpsDataResources.Notice,
                AtlasDataScopeType.AllTenant,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(query.Object);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var service = new BidOpsNoticeService(repository.Object, unitOfWork.Object);
        var updateStartedAt = DateTime.UtcNow;

        await service.UpdateAsync(notice.Id, new UpdateNoticeRequest
        {
            Title = "  修正后的公告  ",
            NoticeType = "AwardAnnouncement",
            ProjectName = "修正后的项目",
            ProjectCode = "2026-001",
            BuyerName = "采购人甲",
            AgencyName = "代理机构乙",
            Region = "四川",
            BudgetAmount = 123456.78m,
            PublishTime = new DateTime(2026, 7, 1, 9, 0, 0),
            SignupDeadline = new DateTime(2026, 7, 5, 17, 0, 0),
            BidDeadline = new DateTime(2026, 7, 10, 10, 0, 0),
            OpenBidTime = new DateTime(2026, 7, 10, 10, 30, 0)
        });

        Assert.Equal("修正后的公告", notice.Title);
        Assert.Equal("AwardAnnouncement", notice.NoticeType);
        Assert.Equal("修正后的项目", notice.ProjectName);
        Assert.Equal("2026-001", notice.ProjectCode);
        Assert.Equal("采购人甲", notice.BuyerName);
        Assert.Equal("代理机构乙", notice.AgencyName);
        Assert.Equal("四川", notice.Region);
        Assert.Equal(123456.78m, notice.BudgetAmount);
        Assert.Equal(new DateTime(2026, 7, 5, 17, 0, 0), notice.SignupDeadline);
        Assert.Equal(new DateTime(2026, 7, 10, 10, 30, 0), notice.OpenBidTime);
        Assert.True(notice.UpdatedAt >= updateStartedAt);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_RejectsNegativeBudgetWithoutSaving()
    {
        var repository = new Mock<IRepository<Notice>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = new BidOpsNoticeService(repository.Object, unitOfWork.Object);

        var exception = await Assert.ThrowsAsync<AtlasException>(() => service.UpdateAsync(101, new UpdateNoticeRequest
        {
            Title = "公告",
            NoticeType = "TenderAnnouncement",
            ProjectName = "项目",
            BudgetAmount = -0.01m
        }));

        Assert.Contains("不能小于 0", exception.Message, StringComparison.Ordinal);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("", "TenderAnnouncement", "项目", "公告标题")]
    [InlineData("公告", "", "项目", "公告类型")]
    [InlineData("公告", "TenderAnnouncement", "", "项目名称")]
    public async Task UpdateAsync_RejectsMissingRequiredBusinessFields(
        string title,
        string noticeType,
        string projectName,
        string expectedField)
    {
        var notice = new Notice { Id = 101, TenantId = 300001 };
        var query = new Mock<IQueryBuilder<Notice>>();
        query
            .Setup(x => x.Where(It.IsAny<Expression<Func<Notice, bool>>>()))
            .Returns(query.Object);
        query
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(notice);
        var repository = new Mock<IRepository<Notice>>();
        repository
            .Setup(x => x.QueryDataScopeTrackingAsync(
                BidOpsDataResources.Notice,
                AtlasDataScopeType.AllTenant,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(query.Object);
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = new BidOpsNoticeService(repository.Object, unitOfWork.Object);

        var exception = await Assert.ThrowsAsync<AtlasException>(() => service.UpdateAsync(101, new UpdateNoticeRequest
        {
            Title = title,
            NoticeType = noticeType,
            ProjectName = projectName
        }));

        Assert.Contains(expectedField, exception.Message, StringComparison.Ordinal);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
