using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v023bidopsmvp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bidops_crawl_channel",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NoticeType = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ListUrl = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Region = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Industry = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ListItemSelector = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TitleSelector = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UrlSelector = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PublishTimeSelector = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DetailContentSelector = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AttachmentSelector = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastScanTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastSuccessTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastError = table.Column<string>(type: "varchar(256)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_crawl_channel", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_crawl_run_log",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: true),
                    ChannelId = table.Column<long>(type: "bigint", nullable: true),
                    BackgroundJobId = table.Column<long>(type: "bigint", nullable: true),
                    Operation = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Message = table.Column<string>(type: "varchar(256)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_crawl_run_log", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_crawl_source",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceType = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BaseUrl = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    RateLimitPerMinute = table.Column<int>(type: "int", nullable: false),
                    CrawlIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxRetryCount = table.Column<int>(type: "int", nullable: false),
                    NeedJsRender = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    NeedLogin = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RespectRobots = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UserAgent = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RobotsPolicyNote = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PauseReason = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PausedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Remark = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_crawl_source", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_notice",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    RawNoticeId = table.Column<long>(type: "bigint", nullable: false),
                    NoticeStagingId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NoticeType = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectName = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectCode = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BuyerName = table.Column<string>(type: "varchar(256)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgencyName = table.Column<string>(type: "varchar(256)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Region = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BudgetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PublishTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SignupDeadline = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    BidDeadline = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    OpenBidTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_notice", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_notice_staging",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    RawNoticeId = table.Column<long>(type: "bigint", nullable: false),
                    NoticeType = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectName = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectCode = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BuyerName = table.Column<string>(type: "varchar(256)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgencyName = table.Column<string>(type: "varchar(256)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Region = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BudgetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PublishTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SignupDeadline = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    BidDeadline = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    OpenBidTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AiConfidence = table.Column<decimal>(type: "decimal(5,4)", precision: 18, scale: 2, nullable: false),
                    ReviewStatus = table.Column<int>(type: "int", nullable: false),
                    ReviewerId = table.Column<long>(type: "bigint", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RawAiOutputStorageKey = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_notice_staging", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_package_staging",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    NoticeStagingId = table.Column<long>(type: "bigint", nullable: false),
                    LotNo = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LotName = table.Column<string>(type: "varchar(256)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PackageNo = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PackageName = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 2, nullable: true),
                    Unit = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BudgetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DeliveryPlace = table.Column<string>(type: "varchar(256)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeliveryPeriod = table.Column<string>(type: "varchar(256)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AiConfidence = table.Column<decimal>(type: "decimal(5,4)", precision: 18, scale: 2, nullable: false),
                    ReviewStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_package_staging", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_raw_attachment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    RawNoticeId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileUrl = table.Column<string>(type: "varchar(256)", maxLength: 1500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileType = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    FileHash = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StorageProvider = table.Column<string>(type: "varchar(256)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StorageKey = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DownloadStatus = table.Column<int>(type: "int", nullable: false),
                    TextExtractStatus = table.Column<int>(type: "int", nullable: false),
                    TextContentStorageKey = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_raw_attachment", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_raw_notice",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: true),
                    SourceNoticeId = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DetailUrl = table.Column<string>(type: "varchar(256)", maxLength: 1500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DetailUrlHash = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NoticeType = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PublishTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FetchTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ContentHash = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StorageProvider = table.Column<string>(type: "varchar(256)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HtmlSnapshotStorageKey = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TextContentStorageKey = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TextPreview = table.Column<string>(type: "varchar(256)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "varchar(256)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_raw_notice", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_requirement_item",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PackageId = table.Column<long>(type: "bigint", nullable: false),
                    RequirementStagingId = table.Column<long>(type: "bigint", nullable: true),
                    RequirementType = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalText = table.Column<string>(type: "varchar(256)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceFileId = table.Column<long>(type: "bigint", nullable: true),
                    SourcePage = table.Column<int>(type: "int", nullable: true),
                    IsMandatory = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsRejectRisk = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RequiredEvidenceType = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RiskLevel = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AiExplanation = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ManualRemark = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_requirement_item", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_requirement_staging",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PackageStagingId = table.Column<long>(type: "bigint", nullable: false),
                    RequirementType = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalText = table.Column<string>(type: "varchar(256)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceFileId = table.Column<long>(type: "bigint", nullable: true),
                    SourcePage = table.Column<int>(type: "int", nullable: true),
                    IsMandatory = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsRejectRisk = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RequiredEvidenceType = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RiskLevel = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AiExplanation = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AiConfidence = table.Column<decimal>(type: "decimal(5,4)", precision: 18, scale: 2, nullable: false),
                    ReviewStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_requirement_staging", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_review_task",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    BizType = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BizId = table.Column<long>(type: "bigint", nullable: false),
                    RawNoticeId = table.Column<long>(type: "bigint", nullable: true),
                    TaskTitle = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedTo = table.Column<long>(type: "bigint", nullable: true),
                    ReviewerId = table.Column<long>(type: "bigint", nullable: true),
                    Decision = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Remark = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_review_task", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_tender_package",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    NoticeId = table.Column<long>(type: "bigint", nullable: false),
                    PackageStagingId = table.Column<long>(type: "bigint", nullable: true),
                    LotNo = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LotName = table.Column<string>(type: "varchar(256)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PackageNo = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PackageName = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 2, nullable: true),
                    Unit = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BudgetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DeliveryPlace = table.Column<string>(type: "varchar(256)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeliveryPeriod = table.Column<string>(type: "varchar(256)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_tender_package", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_channel_SourceId",
                table: "bidops_crawl_channel",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_channel_TenantId",
                table: "bidops_crawl_channel",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_channel_TenantId_CreatedAt",
                table: "bidops_crawl_channel",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_channel_TenantId_Enabled_LastScanTime",
                table: "bidops_crawl_channel",
                columns: new[] { "TenantId", "Enabled", "LastScanTime" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_channel_TenantId_SourceId_Code",
                table: "bidops_crawl_channel",
                columns: new[] { "TenantId", "SourceId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_run_log_BackgroundJobId",
                table: "bidops_crawl_run_log",
                column: "BackgroundJobId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_run_log_ChannelId",
                table: "bidops_crawl_run_log",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_run_log_SourceId",
                table: "bidops_crawl_run_log",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_run_log_TenantId",
                table: "bidops_crawl_run_log",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_run_log_TenantId_BackgroundJobId",
                table: "bidops_crawl_run_log",
                columns: new[] { "TenantId", "BackgroundJobId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_run_log_TenantId_CreatedAt",
                table: "bidops_crawl_run_log",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_run_log_TenantId_SourceId_CreatedAt",
                table: "bidops_crawl_run_log",
                columns: new[] { "TenantId", "SourceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_source_TenantId",
                table: "bidops_crawl_source",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_source_TenantId_Code",
                table: "bidops_crawl_source",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_source_TenantId_CreatedAt",
                table: "bidops_crawl_source",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_crawl_source_TenantId_Enabled_Priority",
                table: "bidops_crawl_source",
                columns: new[] { "TenantId", "Enabled", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_notice_NoticeStagingId",
                table: "bidops_notice",
                column: "NoticeStagingId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_notice_RawNoticeId",
                table: "bidops_notice",
                column: "RawNoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_notice_TenantId",
                table: "bidops_notice",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_notice_TenantId_CreatedAt",
                table: "bidops_notice",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_notice_TenantId_ProjectCode",
                table: "bidops_notice",
                columns: new[] { "TenantId", "ProjectCode" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_notice_TenantId_PublishTime",
                table: "bidops_notice",
                columns: new[] { "TenantId", "PublishTime" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_notice_TenantId_RawNoticeId",
                table: "bidops_notice",
                columns: new[] { "TenantId", "RawNoticeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_notice_staging_RawNoticeId",
                table: "bidops_notice_staging",
                column: "RawNoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_notice_staging_ReviewerId",
                table: "bidops_notice_staging",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_notice_staging_TenantId",
                table: "bidops_notice_staging",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_notice_staging_TenantId_CreatedAt",
                table: "bidops_notice_staging",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_notice_staging_TenantId_RawNoticeId",
                table: "bidops_notice_staging",
                columns: new[] { "TenantId", "RawNoticeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_notice_staging_TenantId_ReviewStatus_CreatedAt",
                table: "bidops_notice_staging",
                columns: new[] { "TenantId", "ReviewStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_package_staging_NoticeStagingId",
                table: "bidops_package_staging",
                column: "NoticeStagingId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_package_staging_TenantId",
                table: "bidops_package_staging",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_package_staging_TenantId_CreatedAt",
                table: "bidops_package_staging",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_package_staging_TenantId_NoticeStagingId",
                table: "bidops_package_staging",
                columns: new[] { "TenantId", "NoticeStagingId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_raw_attachment_RawNoticeId",
                table: "bidops_raw_attachment",
                column: "RawNoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_raw_attachment_TenantId",
                table: "bidops_raw_attachment",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_raw_attachment_TenantId_CreatedAt",
                table: "bidops_raw_attachment",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_raw_attachment_TenantId_RawNoticeId_FileHash",
                table: "bidops_raw_attachment",
                columns: new[] { "TenantId", "RawNoticeId", "FileHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_raw_notice_ChannelId",
                table: "bidops_raw_notice",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_raw_notice_SourceId",
                table: "bidops_raw_notice",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_raw_notice_SourceNoticeId",
                table: "bidops_raw_notice",
                column: "SourceNoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_raw_notice_TenantId",
                table: "bidops_raw_notice",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_raw_notice_TenantId_ContentHash",
                table: "bidops_raw_notice",
                columns: new[] { "TenantId", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_raw_notice_TenantId_CreatedAt",
                table: "bidops_raw_notice",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_raw_notice_TenantId_SourceId_DetailUrlHash",
                table: "bidops_raw_notice",
                columns: new[] { "TenantId", "SourceId", "DetailUrlHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_raw_notice_TenantId_Status_FetchTime",
                table: "bidops_raw_notice",
                columns: new[] { "TenantId", "Status", "FetchTime" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_requirement_item_PackageId",
                table: "bidops_requirement_item",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_requirement_item_RequirementStagingId",
                table: "bidops_requirement_item",
                column: "RequirementStagingId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_requirement_item_SourceFileId",
                table: "bidops_requirement_item",
                column: "SourceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_requirement_item_TenantId",
                table: "bidops_requirement_item",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_requirement_item_TenantId_CreatedAt",
                table: "bidops_requirement_item",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_requirement_item_TenantId_PackageId",
                table: "bidops_requirement_item",
                columns: new[] { "TenantId", "PackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_requirement_item_TenantId_RiskLevel",
                table: "bidops_requirement_item",
                columns: new[] { "TenantId", "RiskLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_requirement_staging_PackageStagingId",
                table: "bidops_requirement_staging",
                column: "PackageStagingId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_requirement_staging_SourceFileId",
                table: "bidops_requirement_staging",
                column: "SourceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_requirement_staging_TenantId",
                table: "bidops_requirement_staging",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_requirement_staging_TenantId_CreatedAt",
                table: "bidops_requirement_staging",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_requirement_staging_TenantId_PackageStagingId",
                table: "bidops_requirement_staging",
                columns: new[] { "TenantId", "PackageStagingId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_requirement_staging_TenantId_RiskLevel",
                table: "bidops_requirement_staging",
                columns: new[] { "TenantId", "RiskLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_review_task_BizId",
                table: "bidops_review_task",
                column: "BizId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_review_task_RawNoticeId",
                table: "bidops_review_task",
                column: "RawNoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_review_task_ReviewerId",
                table: "bidops_review_task",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_review_task_TenantId",
                table: "bidops_review_task",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_review_task_TenantId_BizType_BizId",
                table: "bidops_review_task",
                columns: new[] { "TenantId", "BizType", "BizId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_review_task_TenantId_CreatedAt",
                table: "bidops_review_task",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_review_task_TenantId_Status_Priority_CreatedAt",
                table: "bidops_review_task",
                columns: new[] { "TenantId", "Status", "Priority", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_tender_package_NoticeId",
                table: "bidops_tender_package",
                column: "NoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_tender_package_PackageStagingId",
                table: "bidops_tender_package",
                column: "PackageStagingId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_tender_package_TenantId",
                table: "bidops_tender_package",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_tender_package_TenantId_CreatedAt",
                table: "bidops_tender_package",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_tender_package_TenantId_NoticeId",
                table: "bidops_tender_package",
                columns: new[] { "TenantId", "NoticeId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_tender_package_TenantId_NoticeId_PackageNo",
                table: "bidops_tender_package",
                columns: new[] { "TenantId", "NoticeId", "PackageNo" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_tender_package_TenantId_Status_CreatedAt",
                table: "bidops_tender_package",
                columns: new[] { "TenantId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bidops_crawl_channel");

            migrationBuilder.DropTable(
                name: "bidops_crawl_run_log");

            migrationBuilder.DropTable(
                name: "bidops_crawl_source");

            migrationBuilder.DropTable(
                name: "bidops_notice");

            migrationBuilder.DropTable(
                name: "bidops_notice_staging");

            migrationBuilder.DropTable(
                name: "bidops_package_staging");

            migrationBuilder.DropTable(
                name: "bidops_raw_attachment");

            migrationBuilder.DropTable(
                name: "bidops_raw_notice");

            migrationBuilder.DropTable(
                name: "bidops_requirement_item");

            migrationBuilder.DropTable(
                name: "bidops_requirement_staging");

            migrationBuilder.DropTable(
                name: "bidops_review_task");

            migrationBuilder.DropTable(
                name: "bidops_tender_package");
        }
    }
}
