# Atlas Admin Frontend

## 技术栈

Vue 3 + Vite + TypeScript + Element Plus + Pinia + Vue Router + Axios

## 启动

```bash
npm install
npm run dev
```

## 构建

```bash
npm run build
```

## 类型检查

```bash
npm run typecheck
```

## 环境变量

复制 `.env.example` 为 `.env.local`。

```env
VITE_API_BASE_URL=/api
```

## 后端代理

开发环境默认把 `/api` 代理到 `http://localhost:5260`，与 `src/Atlas.WebApi` 的本地 HTTP launch profile 对齐。

可用 `VITE_DEV_API_PROXY_TARGET` 覆盖代理目标，例如：

```bash
$env:VITE_DEV_API_PROXY_TARGET='https://localhost:7282'
npm run dev
```

## BidOps 首版范围

- 采集来源
- 采集栏目
- 原始公告
- 手动公开 URL 导入
- 审核任务
- 正式公告库
- 商机包件
- 包件要求项

## 权限说明

前端实现了路由级和按钮级权限控制，权限码与后端 BidOps 常量保持一致。前端权限只改善用户体验，不能替代后端鉴权。
