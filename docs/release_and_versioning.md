# Atlas Release And Versioning

Atlas 使用 SemVer。框架库版本来自 `Directory.Build.props` 的 `VersionPrefix`，tag 必须使用同一版本号，例如 `VersionPrefix=0.2.0` 对应 tag `v0.2.0`。

## 版本规则

| 类型 | 规则 | 示例 |
| --- | --- | --- |
| Major | 删除或改变公开 API、配置键、数据库兼容性、Analyzer 规则导致已有业务代码必须修改 | `1.0.0` 到 `2.0.0` |
| Minor | 新增向后兼容能力、可选配置、非破坏性 Analyzer | `0.2.0` 到 `0.3.0` |
| Patch | Bug fix、文档、内部实现调整 | `0.2.0` 到 `0.2.1` |

## Release Workflow

流水线位于 `.github/workflows/release.yml`。

手动 dry-run：

```powershell
gh workflow run release.yml -f version=0.2.0 -f dryRun=true
```

tag 发布：

```powershell
git tag v0.2.0
git push origin v0.2.0
```

workflow 会执行：

1. 校验 `VersionPrefix` 与输入版本或 tag 版本一致。
2. 校验换行和中央包版本。
3. Restore、build、核心测试。
4. `dotnet pack Atlas.sln` 生成 NuGet 包。
5. 构建 WebApi、Worker、MigrationJob Docker 镜像。
6. 生成 release notes。
7. 非 dry-run 时推送 NuGet、GHCR 镜像并创建 GitHub release。

dry-run 不推送真实包、镜像或 GitHub release，只上传 NuGet 包和 release notes artifact。

## Release Notes 要求

每次发布必须包含：

- Summary：说明发布目标。
- Breaking Changes：没有也必须明确写 `None declared`。
- Migration Notes：说明是否需要运行 MigrationJob，以及是否需要手工数据修复。
- Operational Notes：说明配置、部署或观测变更。
- Artifacts：列出 NuGet 包和 Docker 镜像。

模板位于 `.github/release-notes-template.md`。

## Migration Note 模板

```markdown
## Migration Notes

- Required: Yes/No
- Command: `Atlas.MigrationJob plan` then `Atlas.MigrationJob apply`
- Rollback: restore database backup and redeploy previous WebApi/Worker images
- Verification: `/health/ready`, tenant migration state, smoke test endpoint
```

## Breaking Change 标记

提交或 PR 描述中使用：

```text
BREAKING CHANGE: <what changed>
Migration: <required command or manual step>
Compatibility: <affected versions>
```

任何涉及租户边界、仓储访问、认证授权、数据库 schema 的 breaking change，都必须在 release notes 中给出升级步骤和验证标准。
