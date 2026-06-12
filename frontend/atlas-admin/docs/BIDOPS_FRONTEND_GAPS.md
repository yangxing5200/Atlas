# BidOps Frontend Gaps

以下接口当前后端未提供，前端首版只做占位或文档记录，不臆造调用路径。

```text
GET /api/bidops/dashboard/summary
GET /api/bidops/raw-notices/{id}/versions
POST /api/bidops/raw-notices/{id}/reparse
GET /api/bidops/notices/{id}
GET /api/bidops/notices/{id}/packages
```

第二阶段再考虑：

```text
/api/bidops/suppliers
/api/bidops/matching
/api/bidops/pursuits
/api/bidops/compliance
```

## 页面占位

- `/bidops/crawl/raw-notices/:id` 展示公告原文和公开附件，版本历史接口待补充。
- `/bidops/notices` 仅提供跳转包件列表，不调用公告详情。
