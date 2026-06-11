请在公开后的 Atlas 仓库根目录中执行本任务。先阅读：

1. `AGENTS.md`
2. `CODEX_ATLAS_ADAPTATION_PROMPT.md`
3. `BIDOPS_CODEX_EXECUTION_SPEC.md`
4. `docs/BIDOPS/BIDOPS_ATLAS_DATABASE_INTEGRATION_NOTES.md`
5. Atlas README、docs、solution、module template、Tenant DbContext、MigrationJob、Worker/WebApi 启动代码

执行目标：将 BidOps 作为 Atlas 的业务模块接入，先完成“公开标讯采集 → Raw → Staging → 人工审核 → 正式入库”的 MVP 闭环。

硬性要求：

1. 不要创建新 solution，不要重构 Atlas 底座。
2. 不要升级 Atlas 的 .NET 版本或包版本策略。
3. BidOps 目录优先使用 `src/Atlas.Modules.BidOps`，遵守 Atlas 模块模板。
4. BidOps 数据模型逻辑上归模块所有，但物理存储放 Atlas Tenant DB。
5. 不要创建独立 `bidops_db`、`BidOpsDbContext` 或独立 migration pipeline。
6. 所有 BidOps 表使用 `bidops_` 前缀，租户业务实体必须遵守 Atlas 租户隔离规则。
7. 如果 AtlasTenantDbContext 暂不支持扫描模块程序集里的 `IEntityTypeConfiguration<T>`，先实现通用模块 EF 配置扫描机制。
8. Web/API 不得直接注入 AtlasTenantDbContext，不得直接调用 DbContext.Set<T>()；使用 Repository、QueryService 或领域服务。
9. 爬虫、附件下载、文本抽取、AI 解析、去重、变更监控、中标回填等长任务必须放到 Atlas.Worker / 后台任务，不跑在 WebAPI 请求线程。
10. 附件二进制、HTML 快照、大段文本不进 MySQL；定义 `IBidOpsFileStore`，MVP 可用本地文件存储。
11. AI 解析只写 Staging，不直接写正式业务表；人工审核通过后才入正式业务库。
12. 不实现登录抓取、验证码绕过、反爬绕过、高频抓取、非公开信息采集、串标或灰色交易相关功能。

执行顺序：

- Phase 0：只读评估 Atlas，输出 `docs/ATLAS_BIDOPS_FIT_REPORT.md`。
- Phase 0.5：确认数据库归属、模块 EF 扫描、Tenant migration、文件存储策略，写入 `docs/DECISIONS.md`。
- Phase 1：创建 BidOps 模块骨架、权限、菜单、空 API。
- Phase 2：建立 Raw/Staging/Review/Formal 最小数据模型和统一 Tenant migration。
- Phase 3：实现人工 URL 导入或 MockCrawler，跑通 Raw 写入。
- Phase 4：实现 AI/规则 Mock 预解析到 Staging。
- Phase 5：实现待审核池，通过审核后入正式 Notice / Package / Requirement。

除非涉及凭据、安全、生产数据、破坏性操作、付费服务或法律合规风险，否则不要中途询问用户。遇到不确定项，采用保守默认方案，记录到 `docs/DECISIONS.md`，然后继续。
