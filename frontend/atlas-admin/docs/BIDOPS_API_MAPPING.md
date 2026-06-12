# BidOps API Mapping

| 页面 | API | 权限 | 状态 |
|---|---|---|---|
| 采集来源 | GET/POST/PUT/enable/disable `/api/bidops/crawl-sources` | `bidops.crawl.read` / `bidops.crawl.manage` | 已接入 |
| 采集栏目 | GET/POST/PUT/scan-now `/api/bidops/crawl-channels` | `bidops.crawl.read` / `bidops.crawl.manage` / `bidops.crawl.import` | 已接入 |
| 原始公告 | GET/GET by id/GET attachments/GET attachment text/POST import-url `/api/bidops/raw-notices` | `bidops.crawl.read` / `bidops.crawl.import` | 已接入 |
| 审核任务 | GET/GET by id/approve/ignore `/api/bidops/review-tasks` | `bidops.review.read` / `bidops.review.approve` | 已接入 |
| 正式公告 | GET `/api/bidops/notices` | `bidops.business.read` | 已接入 |
| 商机包件 | GET `/api/bidops/packages`, GET `/api/bidops/packages/{id}`, GET `/api/bidops/packages/{id}/timeline`, GET `/api/bidops/packages/{id}/requirements` | `bidops.business.read` | 已接入 |

## Notes

- Axios `baseURL` 为 `/api`，BidOps API 封装不重复拼接 `/api`。
- 正式公告详情接口未实现，前端只做列表与包件跳转，不发起不存在的请求。
- 附件打开使用后端返回的公开来源 `FileUrl`，前端不拼接本地 `StorageKey`。
- 附件提取文本通过 `GET /api/bidops/raw-notices/{id}/attachments/{attachmentId}/text` 读取，由后端从 `TextContentStorageKey` 对应的文件仓库对象中加载。
