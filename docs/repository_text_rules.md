# Atlas 文本和换行规范

Atlas 仓库使用 `.gitattributes` 和 `.editorconfig` 共同约束文本文件。

## 规则

1. `.gitattributes` 使用 `* text eol=lf`，所有文本文件统一以 LF 入库和检出。
2. YAML 和 shell 脚本同样固定使用 LF，避免 CI、Linux 容器和脚本执行差异。
3. 图片、Excel、压缩包、二进制产物必须按 binary 处理。
4. 不允许把业务修改和整仓换行归一化混在同一个 PR。
5. 不允许出现 mixed line endings。

## 检查命令

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-line-endings.ps1
git diff --check
```

`verify-line-endings.ps1` 会扫描 Git 已跟踪和未忽略的新文件，忽略常见二进制扩展，检查 mixed line endings，并强制所有文本文件使用 LF。
