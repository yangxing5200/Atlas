# 后台导出设计执行任务拆分

本文把 `docs/background_export_design.md` 拆分为可验证的工程任务，并记录本次已执行的最小可落地切片。

## 已执行切片

1. **导出领域项目**
   - 验证点：solution 中包含 `Atlas.Exporting`，并可被核心 DI 项目引用。
   - 产出：`src/Atlas.Exporting/Atlas.Exporting.csproj`。

2. **核心抽象与 DTO**
   - 验证点：存在 `IExportJobService`、`IExportTaskProvider`、`IExportFormatWriter`、`IExportFileStore`、`ExportJobPayload`。
   - 产出：`src/Atlas.Exporting/ExportingContracts.cs`。

3. **导出业务状态表**
   - 验证点：Global DbContext 暴露 `ExportJobs`，Global migration 可创建 `ExportJobs` 表和设计索引。
   - 产出：`ExportJob` 实体、`ExportJobStatus` 枚举、EF configuration、migration。

4. **提交、查询、下载服务**
   - 验证点：`ExportJobService` 使用当前身份、provider 元数据、权限校验、查询快照、`export.generate` 后台任务和下载二次校验。
   - 产出：`src/Atlas.Exporting/ExportJobService.cs`。

5. **Worker handler**
   - 验证点：`ExportJobHandler` 注册为 `export.generate` handler，执行时设置后台身份、复验权限、分页读取 provider、写临时文件、commit 后更新 `Ready`。
   - 产出：`src/Atlas.Exporting/ExportJobHandler.cs`。

6. **CSV writer 与本地存储**
   - 验证点：CSV writer 支持 UTF-8 BOM、列头、分页写入、转义；本地存储支持 create/commit/open/delete 与 sha256。
   - 产出：`CsvExportFormatWriter`、`LocalExportFileStore`。

7. **清理任务与配置校验**
   - 验证点：存在 `ExportArtifactCleanupTask`；`Exporting` 配置校验 page size、retention、format、storage。
   - 产出：`ExportArtifactCleanupTask`、`ExportingOptions`、DI 扩展。

8. **运行时接入**
   - 验证点：`AddAtlasCore` 注册导出能力；Worker 默认监听 `export` 队列；WebApi 仍关闭 one-time worker。
   - 产出：`CoreServiceExtensions.cs`、Worker/WebApi appsettings 更新。

9. **后台身份上下文**
   - 验证点：`CurrentIdentity` 优先读取 `IExecutionIdentityAccessor.Current`，Worker 可以复用依赖 `ICurrentIdentity` 的查询/权限能力。
   - 产出：`ExecutionIdentityAccessor`、`CurrentIdentity` 调整。

## 后续未执行切片

1. 模块模板导出 provider/controller 示例。
2. 模板 verify 脚本增加导出 provider 禁用 `DbContext`/`FromSql`/`IgnoreQueryFilters` 的检查。
3. Excel/object storage/审计后台管理 API/定时报表/zip 分片。
4. 完整自动化测试与迁移快照刷新（当前环境缺少 `dotnet` CLI，无法本地生成 EF designer/snapshot 或运行 build）。
