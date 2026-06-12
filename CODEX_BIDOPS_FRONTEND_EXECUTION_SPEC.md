# Atlas BidOps 前端项目 Codex 执行文档

> 文件用途：把本文档放入 Atlas 仓库根目录，交给 Codex 执行。
> 推荐文件名：`CODEX_BIDOPS_FRONTEND_EXECUTION_SPEC.md`
> 目标：在当前 Atlas 仓库中新增一个可独立运行、可构建、可接入后端 BidOps API 的管理端前端项目。
> 生成日期：2026-06-11
> 语言：中文
> 执行优先级：先完成 P0/P1；P2 以后只预留结构，不硬接未实现接口。

---

## 0. 给 Codex 的总指令

你正在处理仓库：

```text
yangxing5200/Atlas
```

这是一个 .NET 8 多租户模块化后台框架，当前仓库已有后端 BidOps 模块，但还没有前端项目。请在仓库根目录新增前端项目：

```text
frontend/atlas-admin
```

使用技术栈：

```text
Vue 3
Vite
TypeScript
Element Plus
Pinia
Vue Router
Axios
```

本次任务只做前端，不重构 Atlas 后端，不创建新的 .NET solution，不移动现有后端目录，不改 BidOps 数据库模型。除非为了修复明确的前端代理、CORS 或文档问题，否则不要修改后端代码。

首版目标是把当前后端已经暴露的 BidOps API 做成可用后台页面：

```text
/api/bidops/crawl-sources
/api/bidops/crawl-channels
/api/bidops/raw-notices
/api/bidops/review-tasks
/api/bidops/notices
/api/bidops/packages
```

如果发现后端接口缺失，不要臆造调用路径；在前端中保留 TODO 或禁用按钮，并在 `frontend/atlas-admin/docs/BIDOPS_FRONTEND_GAPS.md` 记录需要后端补充的接口。

---

## 1. 已核对的后端现状

### 1.1 Atlas 后端技术背景

Atlas 是基于 .NET 8 的企业级多租户框架，包含多租户、模块化、Token/Cookie 认证、EF Core、MySQL、Redis、Swagger/OpenAPI、RabbitMQ/MassTransit 等基础设施。

前端需要按“管理后台 SPA”设计，不要把 BidOps 做成孤立站点。BidOps 应是 Atlas 管理端中的一个业务模块。

### 1.2 当前 BidOps 模块目录

后端 BidOps 模块位于：

```text
src/Atlas.Modules.BidOps
```

当前模块目录包含：

```text
src/Atlas.Modules.BidOps/
  Ai/
  BackgroundJobs/
  Controllers/
  Crawling/
  Documents/
  Entities/
  EntityConfigurations/
  Models/
  Queries/
  Services/
  Atlas.Modules.BidOps.csproj
  BidOpsConstants.cs
  BidOpsModule.cs
```

### 1.3 当前可直接接入的控制器

当前 `src/Atlas.Modules.BidOps/Controllers` 下有：

```text
CrawlSourcesController.cs
CrawlChannelsController.cs
RawNoticesController.cs
ReviewTasksController.cs
NoticesController.cs
PackagesController.cs
```

前端首版只围绕这些控制器实现。

### 1.4 当前权限码

当前后端常量中已有权限码：

```ts
export const BIDOPS_PERMISSIONS = {
  CRAWL_READ: 'bidops.crawl.read',
  CRAWL_MANAGE: 'bidops.crawl.manage',
  CRAWL_IMPORT: 'bidops.crawl.import',
  REVIEW_READ: 'bidops.review.read',
  REVIEW_APPROVE: 'bidops.review.approve',
  BUSINESS_READ: 'bidops.business.read',
} as const
```

前端必须同时做：

```text
1. 路由级权限控制
2. 按钮级权限控制
```

但注意：前端权限控制只是用户体验，不能替代后端鉴权。

---

## 2. 禁止事项

Codex 执行时必须遵守：

1. 不要创建新的 `.sln`。
2. 不要升级 Atlas 后端目标框架。
3. 不要重构 `src/Atlas.Modules.BidOps`。
4. 不要创建独立 `BidOpsDbContext`。
5. 不要改 BidOps migration 流程。
6. 不要把前端项目放进 `src/` 后端项目目录。
7. 不要实现登录态抓取、验证码破解、绕过反爬、自动投标、自动报价、多厂家同包件协同投标等功能。
8. 不要让用户在业务表单里手工输入 `TenantId`。
9. 不要前端拼接对象存储 `StorageKey` 作为下载地址；附件下载必须等后端提供安全下载 API。
10. 不要直接用 `v-html` 展示原始公告 HTML；默认纯文本展示，必须预览 HTML 时先做 sanitize。

---

## 3. 前端项目落点

在仓库根目录新增：

```text
frontend/
  atlas-admin/
```

不要使用：

```text
src/Atlas.Web/
src/Atlas.Modules.BidOps/Web/
src/BidOps.Web/
```

原因：当前 Atlas 后端是 .NET 项目结构，前端应独立构建和部署。

---

## 4. 推荐目录结构

请创建如下目录：

```text
frontend/atlas-admin/
  package.json
  pnpm-lock.yaml              # 若使用 pnpm 安装后自然生成；无则不强制
  index.html
  vite.config.ts
  tsconfig.json
  tsconfig.node.json
  env.d.ts
  .env.example
  README.md
  docs/
    BIDOPS_FRONTEND_GAPS.md
    BIDOPS_API_MAPPING.md

  src/
    main.ts
    App.vue

    app/
      bootstrap.ts

    router/
      index.ts
      guards.ts
      routes.ts

    layouts/
      BasicLayout.vue
      BlankLayout.vue

    stores/
      app.store.ts
      auth.store.ts
      permission.store.ts

    api/
      http.ts
      types.ts
      bidops/
        crawlSources.api.ts
        crawlChannels.api.ts
        rawNotices.api.ts
        reviewTasks.api.ts
        notices.api.ts
        packages.api.ts

    modules/
      bidops/
        constants.ts
        routes.ts
        types.ts

        pages/
          BidOpsHomePage.vue

          crawl/
            CrawlSourceListPage.vue
            CrawlChannelListPage.vue
            RawNoticeListPage.vue
            RawNoticeDetailPage.vue

          review/
            ReviewTaskListPage.vue
            ReviewTaskDetailPage.vue

          business/
            NoticeListPage.vue
            PackageListPage.vue
            PackageDetailPage.vue

        components/
          BidOpsStatusTag.vue
          DeadlineCountdown.vue
          ManualUrlImportDialog.vue
          PermissionButton.vue
          RawNoticePreview.vue
          RequirementTable.vue
          ReviewDecisionPanel.vue
          RiskLevelTag.vue

    shared/
      components/
        DataTable.vue
        EmptyState.vue
        FormDrawer.vue
        PageContainer.vue
        SearchForm.vue

      composables/
        usePagination.ts
        usePermission.ts
        useRequest.ts
        useTableQuery.ts

      utils/
        date.ts
        enum.ts
        money.ts
        sanitize.ts

    styles/
      global.css
```

允许根据 Vite/Vue 生成的默认文件做微调，但不能把业务代码散落在全局目录。

---

## 5. 初始化方式

### 5.1 优先方式：使用 Vite 模板

在仓库根目录执行：

```bash
mkdir -p frontend
cd frontend
pnpm create vite atlas-admin --template vue-ts
cd atlas-admin
pnpm install
pnpm add element-plus @element-plus/icons-vue pinia vue-router axios dayjs dompurify
pnpm add -D @types/dompurify eslint prettier
```

如果环境没有 `pnpm`，使用：

```bash
npm create vite@latest atlas-admin -- --template vue-ts
cd atlas-admin
npm install
npm install element-plus @element-plus/icons-vue pinia vue-router axios dayjs dompurify
npm install -D @types/dompurify eslint prettier
```

### 5.2 package.json 脚本

确保 `package.json` 至少包含：

```json
{
  "scripts": {
    "dev": "vite --host 0.0.0.0",
    "build": "vue-tsc -b && vite build",
    "preview": "vite preview --host 0.0.0.0",
    "typecheck": "vue-tsc -b"
  }
}
```

如果模板没有 `vue-tsc`，请安装：

```bash
pnpm add -D vue-tsc
```

---

## 6. 环境变量

创建：

```text
frontend/atlas-admin/.env.example
```

内容：

```env
VITE_APP_TITLE=Atlas Admin
VITE_API_BASE_URL=/api
VITE_AUTH_MODE=bearer
VITE_ENABLE_BIDOPS=true
```

开发代理放到 `vite.config.ts`。

---

## 7. Vite 配置

`vite.config.ts` 应支持：

1. Vue 插件
2. `@` 指向 `src`
3. `/api` 代理到后端
4. 端口默认 `5173`

示例：

```ts
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_DEV_API_PROXY_TARGET || 'https://localhost:5001',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
```

---

## 8. API 响应结构

后端 `PagedResult<T>` 当前结构：

```ts
export interface PagedResult<T> {
  total: number
  items: T[]
  pageIndex: number
  pageSize: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}
```

注意 C# 中 `Id` 是 `long`，前端首版统一用 `number`。如果后续出现超过 JS 安全整数范围，再调整为 `string`。

---

## 9. BidOps TypeScript 类型

创建：

```text
src/modules/bidops/types.ts
```

内容至少包含：

```ts
export interface BidOpsPagedQuery {
  keyword?: string
  pageIndex?: number
  pageSize?: number
}

export interface RawNoticeSearchQuery extends BidOpsPagedQuery {
  status?: RawNoticeStatus
}

export interface ReviewTaskSearchQuery extends BidOpsPagedQuery {
  status?: ReviewTaskStatus
}

export interface PackageSearchQuery extends BidOpsPagedQuery {
  noticeId?: number
}

export interface PagedResult<T> {
  total: number
  items: T[]
  pageIndex: number
  pageSize: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}

export type RawNoticeStatus = number | string
export type ReviewTaskStatus = number | string
export type ReviewStatus = number | string

export interface CrawlSourceDto {
  id: number
  code: string
  name: string
  sourceType: string
  baseUrl: string
  enabled: boolean
  rateLimitPerMinute: number
  crawlIntervalMinutes: number
  maxRetryCount: number
  needLogin: boolean
  respectRobots: boolean
  robotsPolicyNote: string
  pauseReason: string
}

export interface CrawlChannelDto {
  id: number
  sourceId: number
  code: string
  name: string
  noticeType: string
  listUrl: string
  region: string
  industry: string
  enabled: boolean
  lastScanTime?: string | null
  lastSuccessTime?: string | null
  lastError: string
}

export interface RawNoticeDto {
  id: number
  sourceId: number
  channelId?: number | null
  title: string
  detailUrl: string
  noticeType: string
  publishTime?: string | null
  fetchTime: string
  contentHash: string
  textPreview: string
  status: RawNoticeStatus
  lastError: string
}

export interface ReviewTaskDto {
  id: number
  bizType: string
  bizId: number
  rawNoticeId?: number | null
  taskTitle: string
  priority: number
  status: ReviewTaskStatus
  decision: string
  remark: string
  createdAt: string
  reviewedAt?: string | null
}

export interface NoticeStagingDto {
  id: number
  rawNoticeId: number
  noticeType: string
  projectName: string
  projectCode: string
  buyerName: string
  agencyName: string
  region: string
  budgetAmount?: number | null
  publishTime?: string | null
  signupDeadline?: string | null
  bidDeadline?: string | null
  openBidTime?: string | null
  aiConfidence: number
  reviewStatus: ReviewStatus
}

export interface RequirementStagingDto {
  id: number
  packageStagingId: number
  requirementType: string
  originalText: string
  isMandatory: boolean
  isRejectRisk: boolean
  requiredEvidenceType: string
  riskLevel: string
  aiExplanation: string
  aiConfidence: number
}

export interface PackageStagingDto {
  id: number
  noticeStagingId: number
  lotNo: string
  lotName: string
  packageNo: string
  packageName: string
  category: string
  budgetAmount?: number | null
  aiConfidence: number
  reviewStatus: ReviewStatus
  requirements: RequirementStagingDto[]
}

export interface ReviewTaskDetailDto {
  task: ReviewTaskDto
  rawNotice?: RawNoticeDto | null
  notice?: NoticeStagingDto | null
  packages: PackageStagingDto[]
}

export interface NoticeDto {
  id: number
  rawNoticeId: number
  title: string
  noticeType: string
  projectName: string
  projectCode: string
  buyerName: string
  region: string
  budgetAmount?: number | null
  publishTime?: string | null
  bidDeadline?: string | null
  status: string
}

export interface TenderPackageDto {
  id: number
  noticeId: number
  packageNo: string
  packageName: string
  category: string
  budgetAmount?: number | null
  maxPrice?: number | null
  deliveryPlace: string
  deliveryPeriod: string
  status: string
}

export interface RequirementItemDto {
  id: number
  packageId: number
  requirementType: string
  originalText: string
  isMandatory: boolean
  isRejectRisk: boolean
  requiredEvidenceType: string
  riskLevel: string
}

export interface EnqueueJobDto {
  jobId: number
  jobType: string
  queue: string
  alreadyExists: boolean
}

export interface CreateCrawlSourceRequest {
  code: string
  name: string
  sourceType: string
  baseUrl: string
  enabled: boolean
  rateLimitPerMinute: number
  crawlIntervalMinutes: number
  maxRetryCount: number
  needJsRender: boolean
  needLogin: boolean
  respectRobots: boolean
  robotsPolicyNote: string
  remark: string
}

export type UpdateCrawlSourceRequest = CreateCrawlSourceRequest

export interface CreateCrawlChannelRequest {
  sourceId: number
  code: string
  name: string
  noticeType: string
  listUrl: string
  region: string
  industry: string
  enabled: boolean
}

export type UpdateCrawlChannelRequest = CreateCrawlChannelRequest

export interface ImportPublicUrlRequest {
  sourceId?: number | null
  channelId?: number | null
  detailUrl: string
  title?: string | null
  noticeType?: string | null
  textContent?: string | null
}

export interface ReviewDecisionRequest {
  remark?: string | null
}
```

---

## 10. HTTP 客户端

创建：

```text
src/api/http.ts
```

要求：

1. `baseURL` 默认读取 `VITE_API_BASE_URL`，默认 `/api`。
2. 支持 Bearer Token。
3. 支持 Cookie 模式，因此 `withCredentials: true`。
4. 支持 `X-Tenant-Id`、`X-Store-Id` 请求头，但不要在业务页面让用户输入。
5. 处理 401，跳转登录或清空登录态。
6. 响应直接返回 `response.data`。

示例：

```ts
import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '@/stores/auth.store'

export const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 30_000,
  withCredentials: true,
})

http.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const auth = useAuthStore()

  if (auth.token) {
    config.headers.Authorization = `Bearer ${auth.token}`
  }

  if (auth.tenantId) {
    config.headers['X-Tenant-Id'] = auth.tenantId
  }

  if (auth.storeId) {
    config.headers['X-Store-Id'] = auth.storeId
  }

  return config
})

http.interceptors.response.use(
  (response) => response.data,
  (error: AxiosError<any>) => {
    const status = error.response?.status
    const message = error.response?.data?.message || error.message || '请求失败'

    if (status === 401) {
      useAuthStore().logout()
      ElMessage.error('登录已过期，请重新登录')
    } else if (status === 403) {
      ElMessage.error('没有权限执行该操作')
    } else {
      ElMessage.error(message)
    }

    return Promise.reject(error)
  },
)
```

如果 TypeScript 对 Axios 泛型返回值报错，可以在 `src/api/types.ts` 做统一包装，或局部用 `as Promise<T>`。

---

## 11. BidOps API 封装

所有 BidOps API 的 baseURL 不要包含 `/api`，因为 Axios 已经设置了 `/api`。

### 11.1 采集来源 API

`src/api/bidops/crawlSources.api.ts`

```ts
import { http } from '@/api/http'
import type {
  BidOpsPagedQuery,
  CrawlSourceDto,
  CreateCrawlSourceRequest,
  PagedResult,
  ReviewDecisionRequest,
  UpdateCrawlSourceRequest,
} from '@/modules/bidops/types'

const base = '/bidops/crawl-sources'

export const crawlSourcesApi = {
  search(params: BidOpsPagedQuery) {
    return http.get<PagedResult<CrawlSourceDto>>(base, { params })
  },

  create(data: CreateCrawlSourceRequest) {
    return http.post<CrawlSourceDto>(base, data)
  },

  update(id: number, data: UpdateCrawlSourceRequest) {
    return http.put<void>(`${base}/${id}`, data)
  },

  enable(id: number) {
    return http.post<void>(`${base}/${id}/enable`)
  },

  disable(id: number, data?: ReviewDecisionRequest) {
    return http.post<void>(`${base}/${id}/disable`, data || {})
  },
}
```

### 11.2 采集栏目 API

`src/api/bidops/crawlChannels.api.ts`

```ts
import { http } from '@/api/http'
import type {
  BidOpsPagedQuery,
  CrawlChannelDto,
  CreateCrawlChannelRequest,
  EnqueueJobDto,
  PagedResult,
  UpdateCrawlChannelRequest,
} from '@/modules/bidops/types'

const base = '/bidops/crawl-channels'

export const crawlChannelsApi = {
  search(params: BidOpsPagedQuery) {
    return http.get<PagedResult<CrawlChannelDto>>(base, { params })
  },

  create(data: CreateCrawlChannelRequest) {
    return http.post<CrawlChannelDto>(base, data)
  },

  update(id: number, data: UpdateCrawlChannelRequest) {
    return http.put<void>(`${base}/${id}`, data)
  },

  scanNow(id: number) {
    return http.post<EnqueueJobDto>(`${base}/${id}/scan-now`)
  },
}
```

### 11.3 原始公告 API

`src/api/bidops/rawNotices.api.ts`

```ts
import { http } from '@/api/http'
import type {
  EnqueueJobDto,
  ImportPublicUrlRequest,
  PagedResult,
  RawNoticeDto,
  RawNoticeSearchQuery,
} from '@/modules/bidops/types'

const base = '/bidops/raw-notices'

export const rawNoticesApi = {
  search(params: RawNoticeSearchQuery) {
    return http.get<PagedResult<RawNoticeDto>>(base, { params })
  },

  get(id: number) {
    return http.get<RawNoticeDto>(`${base}/${id}`)
  },

  importUrl(data: ImportPublicUrlRequest) {
    return http.post<EnqueueJobDto>(`${base}/import-url`, data)
  },
}
```

### 11.4 审核任务 API

`src/api/bidops/reviewTasks.api.ts`

```ts
import { http } from '@/api/http'
import type {
  NoticeDto,
  PagedResult,
  ReviewDecisionRequest,
  ReviewTaskDetailDto,
  ReviewTaskDto,
  ReviewTaskSearchQuery,
} from '@/modules/bidops/types'

const base = '/bidops/review-tasks'

export const reviewTasksApi = {
  search(params: ReviewTaskSearchQuery) {
    return http.get<PagedResult<ReviewTaskDto>>(base, { params })
  },

  get(id: number) {
    return http.get<ReviewTaskDetailDto>(`${base}/${id}`)
  },

  approve(id: number, data: ReviewDecisionRequest) {
    return http.post<NoticeDto>(`${base}/${id}/approve`, data)
  },

  ignore(id: number, data: ReviewDecisionRequest) {
    return http.post<void>(`${base}/${id}/ignore`, data)
  },
}
```

### 11.5 正式公告 API

`src/api/bidops/notices.api.ts`

```ts
import { http } from '@/api/http'
import type { BidOpsPagedQuery, NoticeDto, PagedResult } from '@/modules/bidops/types'

const base = '/bidops/notices'

export const noticesApi = {
  search(params: BidOpsPagedQuery) {
    return http.get<PagedResult<NoticeDto>>(base, { params })
  },
}
```

注意：当前后端没有正式公告详情接口，不要实现 `get(id)` 调用。详情页可暂时展示“后端接口待补充”。

### 11.6 商机包件 API

`src/api/bidops/packages.api.ts`

```ts
import { http } from '@/api/http'
import type {
  PackageSearchQuery,
  PagedResult,
  RequirementItemDto,
  TenderPackageDto,
} from '@/modules/bidops/types'

const base = '/bidops/packages'

export const packagesApi = {
  search(params: PackageSearchQuery) {
    return http.get<PagedResult<TenderPackageDto>>(base, { params })
  },

  requirements(id: number) {
    return http.get<RequirementItemDto[]>(`${base}/${id}/requirements`)
  },
}
```

注意：当前后端没有包件详情接口，详情页需要通过列表结果传参或只展示要求项。

---

## 12. 路由设计

创建：

```text
src/modules/bidops/routes.ts
```

路由：

```ts
import type { RouteRecordRaw } from 'vue-router'
import { BIDOPS_PERMISSIONS } from './constants'

export const bidOpsRoutes: RouteRecordRaw[] = [
  {
    path: '/bidops',
    name: 'BidOpsHome',
    component: () => import('./pages/BidOpsHomePage.vue'),
    meta: {
      title: '招投标作业',
      permissionsAny: [
        BIDOPS_PERMISSIONS.CRAWL_READ,
        BIDOPS_PERMISSIONS.REVIEW_READ,
        BIDOPS_PERMISSIONS.BUSINESS_READ,
      ],
    },
  },
  {
    path: '/bidops/crawl/sources',
    name: 'BidOpsCrawlSources',
    component: () => import('./pages/crawl/CrawlSourceListPage.vue'),
    meta: {
      title: '采集来源',
      permissions: [BIDOPS_PERMISSIONS.CRAWL_READ],
    },
  },
  {
    path: '/bidops/crawl/channels',
    name: 'BidOpsCrawlChannels',
    component: () => import('./pages/crawl/CrawlChannelListPage.vue'),
    meta: {
      title: '采集栏目',
      permissions: [BIDOPS_PERMISSIONS.CRAWL_READ],
    },
  },
  {
    path: '/bidops/crawl/raw-notices',
    name: 'BidOpsRawNotices',
    component: () => import('./pages/crawl/RawNoticeListPage.vue'),
    meta: {
      title: '原始公告',
      permissions: [BIDOPS_PERMISSIONS.CRAWL_READ],
    },
  },
  {
    path: '/bidops/crawl/raw-notices/:id',
    name: 'BidOpsRawNoticeDetail',
    component: () => import('./pages/crawl/RawNoticeDetailPage.vue'),
    meta: {
      title: '原始公告详情',
      permissions: [BIDOPS_PERMISSIONS.CRAWL_READ],
    },
  },
  {
    path: '/bidops/review/tasks',
    name: 'BidOpsReviewTasks',
    component: () => import('./pages/review/ReviewTaskListPage.vue'),
    meta: {
      title: '待审核池',
      permissions: [BIDOPS_PERMISSIONS.REVIEW_READ],
    },
  },
  {
    path: '/bidops/review/tasks/:id',
    name: 'BidOpsReviewTaskDetail',
    component: () => import('./pages/review/ReviewTaskDetailPage.vue'),
    meta: {
      title: '审核详情',
      permissions: [BIDOPS_PERMISSIONS.REVIEW_READ],
    },
  },
  {
    path: '/bidops/notices',
    name: 'BidOpsNotices',
    component: () => import('./pages/business/NoticeListPage.vue'),
    meta: {
      title: '正式公告库',
      permissions: [BIDOPS_PERMISSIONS.BUSINESS_READ],
    },
  },
  {
    path: '/bidops/packages',
    name: 'BidOpsPackages',
    component: () => import('./pages/business/PackageListPage.vue'),
    meta: {
      title: '商机包件',
      permissions: [BIDOPS_PERMISSIONS.BUSINESS_READ],
    },
  },
  {
    path: '/bidops/packages/:id',
    name: 'BidOpsPackageDetail',
    component: () => import('./pages/business/PackageDetailPage.vue'),
    meta: {
      title: '包件详情',
      permissions: [BIDOPS_PERMISSIONS.BUSINESS_READ],
    },
  },
]
```

创建全局 `src/router/index.ts` 并导入 `bidOpsRoutes`。

---

## 13. 权限 Store 与路由守卫

### 13.1 Auth Store

`src/stores/auth.store.ts`：

首版可以实现本地开发友好的最小登录态：

```ts
import { defineStore } from 'pinia'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('ATLAS_TOKEN') || '',
    tenantId: localStorage.getItem('ATLAS_TENANT_ID') || '',
    storeId: localStorage.getItem('ATLAS_STORE_ID') || '',
    permissions: JSON.parse(localStorage.getItem('ATLAS_PERMISSIONS') || '[]') as string[],
  }),

  actions: {
    setToken(token: string) {
      this.token = token
      localStorage.setItem('ATLAS_TOKEN', token)
    },

    setContext(context: { tenantId?: string; storeId?: string; permissions?: string[] }) {
      if (context.tenantId !== undefined) {
        this.tenantId = context.tenantId
        localStorage.setItem('ATLAS_TENANT_ID', context.tenantId)
      }

      if (context.storeId !== undefined) {
        this.storeId = context.storeId
        localStorage.setItem('ATLAS_STORE_ID', context.storeId)
      }

      if (context.permissions !== undefined) {
        this.permissions = context.permissions
        localStorage.setItem('ATLAS_PERMISSIONS', JSON.stringify(context.permissions))
      }
    },

    hasPermission(permission: string) {
      return this.permissions.includes(permission)
    },

    hasAllPermissions(permissions: string[] = []) {
      return permissions.every((permission) => this.hasPermission(permission))
    },

    hasAnyPermission(permissions: string[] = []) {
      return permissions.length === 0 || permissions.some((permission) => this.hasPermission(permission))
    },

    logout() {
      this.token = ''
      localStorage.removeItem('ATLAS_TOKEN')
    },
  },
})
```

后续等 Atlas 认证接口确定后，再补登录页和用户信息 API。

### 13.2 路由守卫

`src/router/guards.ts`：

```ts
import type { Router } from 'vue-router'
import { useAuthStore } from '@/stores/auth.store'

export function setupRouterGuards(router: Router) {
  router.beforeEach((to) => {
    const auth = useAuthStore()

    const requiredAll = (to.meta.permissions || []) as string[]
    const requiredAny = (to.meta.permissionsAny || []) as string[]

    if (requiredAll.length > 0 && !auth.hasAllPermissions(requiredAll)) {
      return '/403'
    }

    if (requiredAny.length > 0 && !auth.hasAnyPermission(requiredAny)) {
      return '/403'
    }

    return true
  })
}
```

TypeScript 中需要扩展 `RouteMeta`：

```text
src/env.d.ts
```

```ts
/// <reference types="vite/client" />

import 'vue-router'

declare module 'vue-router' {
  interface RouteMeta {
    title?: string
    permissions?: string[]
    permissionsAny?: string[]
  }
}
```

---

## 14. 基础布局

实现 `BasicLayout.vue`：

要求：

1. 左侧菜单。
2. 顶部栏显示系统名、当前租户/门店占位、用户区域占位。
3. 内容区使用 `<router-view />`。
4. 菜单至少包含：

```text
招投标作业
  今日作业台 /bidops
  标讯采集
    采集来源 /bidops/crawl/sources
    采集栏目 /bidops/crawl/channels
    原始公告 /bidops/crawl/raw-notices
  待审核池 /bidops/review/tasks
  正式公告库 /bidops/notices
  商机包件 /bidops/packages
```

菜单项要按权限隐藏。

---

## 15. 通用组件要求

### 15.1 PageContainer

提供统一标题、描述和右侧操作区：

```vue
<template>
  <section class="page-container">
    <header class="page-header">
      <div>
        <h1>{{ title }}</h1>
        <p v-if="description">{{ description }}</p>
      </div>
      <div class="page-actions">
        <slot name="actions" />
      </div>
    </header>
    <main>
      <slot />
    </main>
  </section>
</template>
```

### 15.2 PermissionButton

按权限显示按钮：

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { useAuthStore } from '@/stores/auth.store'

const props = defineProps<{
  permission?: string
  permissionsAny?: string[]
}>()

const auth = useAuthStore()

const visible = computed(() => {
  if (props.permission) return auth.hasPermission(props.permission)
  if (props.permissionsAny?.length) return auth.hasAnyPermission(props.permissionsAny)
  return true
})
</script>

<template>
  <el-button v-if="visible">
    <slot />
  </el-button>
</template>
```

实际实现时要透传 `type`、`loading`、`disabled`、`@click` 等属性。可用 `v-bind="$attrs"`。

### 15.3 RiskLevelTag

风险等级：

```text
High / 高：danger
Medium / 中：warning
Low / 低：success
空：info
```

### 15.4 RawNoticePreview

默认使用纯文本：

```vue
<pre class="raw-notice-preview">{{ text }}</pre>
```

不要直接 `v-html`。如果实现 HTML 模式，必须使用 `dompurify`。

---

## 16. 页面实现要求

### 16.1 BidOps 首页 `/bidops`

当前后端没有 dashboard 汇总接口，所以首页只做入口卡片和轻量说明，不强行聚合统计。

页面卡片：

```text
采集来源
采集栏目
原始公告
待审核池
正式公告库
商机包件
```

每个卡片显示：

```text
名称
说明
需要权限
进入按钮
```

### 16.2 采集来源列表 `/bidops/crawl/sources`

必须实现：

1. 关键词搜索。
2. 分页表格。
3. 新增采集来源。
4. 编辑采集来源。
5. 启用。
6. 停用。
7. 停用时允许输入原因，传给 `ReviewDecisionRequest.remark`。
8. `NeedLogin = true` 时显示风险提示。
9. `RespectRobots = false` 时显示合规风险提示。

表格列：

```text
Code
Name
SourceType
BaseUrl
Enabled
RateLimitPerMinute
CrawlIntervalMinutes
MaxRetryCount
NeedLogin
RespectRobots
PauseReason
Actions
```

新增/编辑表单字段：

```text
code
name
sourceType
baseUrl
enabled
rateLimitPerMinute
crawlIntervalMinutes
maxRetryCount
needJsRender
needLogin
respectRobots
robotsPolicyNote
remark
```

默认值：

```ts
const defaultSourceForm = {
  code: '',
  name: '',
  sourceType: 'Mock',
  baseUrl: '',
  enabled: true,
  rateLimitPerMinute: 10,
  crawlIntervalMinutes: 60,
  maxRetryCount: 3,
  needJsRender: false,
  needLogin: false,
  respectRobots: true,
  robotsPolicyNote: '',
  remark: '',
}
```

权限：

```text
查看：bidops.crawl.read
新增/编辑/启停：bidops.crawl.manage
```

### 16.3 采集栏目列表 `/bidops/crawl/channels`

必须实现：

1. 关键词搜索。
2. 分页表格。
3. 新增栏目。
4. 编辑栏目。
5. 立即扫描。
6. 扫描返回 `EnqueueJobDto` 后显示任务信息。

表格列：

```text
SourceId
Code
Name
NoticeType
ListUrl
Region
Industry
Enabled
LastScanTime
LastSuccessTime
LastError
Actions
```

表单字段：

```text
sourceId
code
name
noticeType
listUrl
region
industry
enabled
```

默认值：

```ts
const defaultChannelForm = {
  sourceId: 0,
  code: '',
  name: '',
  noticeType: 'TenderAnnouncement',
  listUrl: '',
  region: '',
  industry: '',
  enabled: true,
}
```

权限：

```text
查看：bidops.crawl.read
新增/编辑：bidops.crawl.manage
立即扫描：bidops.crawl.import
```

### 16.4 原始公告列表 `/bidops/crawl/raw-notices`

必须实现：

1. 关键词搜索。
2. 状态筛选。
3. 分页表格。
4. 查看详情。
5. 手动导入公开 URL。

表格列：

```text
Title
NoticeType
SourceId
ChannelId
PublishTime
FetchTime
Status
LastError
Actions
```

手动 URL 导入弹窗字段：

```text
sourceId
channelId
detailUrl
title
noticeType
textContent
```

规则：

1. `detailUrl` 必填。
2. 文案明确“仅允许导入公开可访问 URL”。
3. 不提供 Cookie、验证码、登录态配置。
4. 提交到 `POST /api/bidops/raw-notices/import-url`。
5. 成功后提示 `JobId`、`JobType`、`Queue`、`AlreadyExists`，并刷新列表。

权限：

```text
查看：bidops.crawl.read
手动导入：bidops.crawl.import
```

### 16.5 原始公告详情 `/bidops/crawl/raw-notices/:id`

当前后端只返回 `RawNoticeDto`，没有完整正文、HTML、附件接口。

页面展示：

```text
标题
来源 ID
栏目 ID
详情 URL
公告类型
发布时间
抓取时间
内容哈希
状态
错误信息
文本预览
```

若需要附件、原文快照、版本历史，显示禁用区块：

```text
后端接口待补充：
GET /api/bidops/raw-notices/{id}/attachments
GET /api/bidops/raw-notices/{id}/versions
```

不要臆造接口。

### 16.6 审核任务列表 `/bidops/review/tasks`

必须实现：

1. 关键词搜索。
2. 状态筛选。
3. 分页表格。
4. 查看审核详情。

表格列：

```text
TaskTitle
BizType
BizId
RawNoticeId
Priority
Status
Decision
Remark
CreatedAt
ReviewedAt
Actions
```

权限：

```text
查看：bidops.review.read
```

### 16.7 审核详情 `/bidops/review/tasks/:id`

必须实现左右分屏：

左侧：原始公告信息

```text
标题
详情 URL
公告类型
发布时间
抓取时间
状态
错误信息
文本预览
```

右侧：暂存解析信息

```text
NoticeStaging:
  NoticeType
  ProjectName
  ProjectCode
  BuyerName
  AgencyName
  Region
  BudgetAmount
  PublishTime
  SignupDeadline
  BidDeadline
  OpenBidTime
  AiConfidence
  ReviewStatus

PackageStaging:
  LotNo
  LotName
  PackageNo
  PackageName
  Category
  BudgetAmount
  AiConfidence
  ReviewStatus

RequirementStaging:
  RequirementType
  OriginalText
  IsMandatory
  IsRejectRisk
  RequiredEvidenceType
  RiskLevel
  AiExplanation
  AiConfidence
```

底部审核面板：

```text
备注 textarea
审核通过
忽略
```

接口：

```text
POST /api/bidops/review-tasks/{id}/approve
POST /api/bidops/review-tasks/{id}/ignore
```

审核通过后：

1. 提示成功。
2. 跳转正式公告库 `/bidops/notices` 或留在当前页并刷新状态。
3. 如果后端返回 NoticeDto，可显示“已生成正式公告”。

忽略后：

1. 提示成功。
2. 跳回审核任务列表。

权限：

```text
查看：bidops.review.read
通过/忽略：bidops.review.approve
```

### 16.8 正式公告库 `/bidops/notices`

必须实现：

1. 关键词搜索。
2. 分页表格。
3. 不做详情 API 调用，因为后端未实现详情。

表格列：

```text
Title
NoticeType
ProjectName
ProjectCode
BuyerName
Region
BudgetAmount
PublishTime
BidDeadline
Status
Actions
```

操作：

```text
查看包件：跳转 /bidops/packages?noticeId={id}
```

权限：

```text
查看：bidops.business.read
```

### 16.9 商机包件列表 `/bidops/packages`

必须实现：

1. 关键词搜索。
2. `noticeId` 查询参数筛选。
3. 分页表格。
4. 查看包件详情。

表格列：

```text
PackageNo
PackageName
Category
BudgetAmount
MaxPrice
DeliveryPlace
DeliveryPeriod
Status
Actions
```

权限：

```text
查看：bidops.business.read
```

### 16.10 包件详情 `/bidops/packages/:id`

当前后端没有包件详情接口，只有要求项接口。

页面实现：

1. 从路由参数取 `id`。
2. 调用 `GET /api/bidops/packages/{id}/requirements`。
3. 展示要求项列表。
4. 包件基础信息如果没有，就显示“后端接口待补充”。

要求项表格列：

```text
RequirementType
OriginalText
IsMandatory
IsRejectRisk
RequiredEvidenceType
RiskLevel
```

强制项和废标风险项要有明显标签。

---

## 17. 表格与分页约定

所有列表页统一：

```ts
const query = reactive({
  keyword: '',
  pageIndex: 1,
  pageSize: 20,
})
```

Element Plus 分页：

```vue
<el-pagination
  v-model:current-page="query.pageIndex"
  v-model:page-size="query.pageSize"
  :total="result.total"
  :page-sizes="[10, 20, 50, 100]"
  layout="total, sizes, prev, pager, next, jumper"
  @current-change="loadData"
  @size-change="loadData"
/>
```

搜索时重置：

```ts
query.pageIndex = 1
await loadData()
```

---

## 18. 状态展示

后端枚举当前可能序列化为数字，也可能在配置后序列化为字符串。前端先做兼容展示，不要依赖固定枚举名。

实现：

```ts
export function formatEnumValue(value: unknown) {
  if (value === null || value === undefined || value === '') return '-'
  return String(value)
}
```

`BidOpsStatusTag` 根据字符串粗略映射：

```text
Succeeded / Approved / Enabled / true -> success
Failed / Ignored / Disabled / false -> danger
Pending / Running / New -> warning
其他 -> info
```

---

## 19. 安全与合规要求

### 19.1 公开采集边界

在采集来源、采集栏目、手动 URL 导入页面明确提示：

```text
仅允许处理公开网页和公开附件。
不得配置需要登录、验证码、短信、人脸、企业证书、客户端证书才能访问的数据源。
不得绕过反爬、破解验证码、伪造登录态或高频压测目标网站。
```

### 19.2 原文预览安全

默认展示 `TextPreview`，不要展示 HTML。

如果后续新增 HTML 预览：

```ts
import DOMPurify from 'dompurify'

const safeHtml = computed(() => DOMPurify.sanitize(rawHtml.value))
```

### 19.3 投标合规边界

前端首版不实现：

```text
自动生成整本标书
自动报价
自动投标
自动联系厂家
多厂家同包件协同投标
专家/评委非公开信息分析
返点/好处费/灰色资金流功能
```

### 19.4 租户隔离

前端请求可带当前 `tenantId`、`storeId` 上下文，但不要在 BidOps 业务表单中出现 `TenantId` 输入框。

---

## 20. 文档输出要求

在前端项目内创建：

```text
frontend/atlas-admin/docs/BIDOPS_API_MAPPING.md
```

内容记录：

```text
页面 -> API -> 权限 -> 是否已实现
```

至少包含：

| 页面 | API | 权限 | 状态 |
|---|---|---|---|
| 采集来源 | GET/POST/PUT/enable/disable `/api/bidops/crawl-sources` | crawl.read / crawl.manage | 已接入 |
| 采集栏目 | GET/POST/PUT/scan-now `/api/bidops/crawl-channels` | crawl.read / crawl.manage / crawl.import | 已接入 |
| 原始公告 | GET/GET by id/POST import-url `/api/bidops/raw-notices` | crawl.read / crawl.import | 已接入 |
| 审核任务 | GET/GET by id/approve/ignore `/api/bidops/review-tasks` | review.read / review.approve | 已接入 |
| 正式公告 | GET `/api/bidops/notices` | business.read | 已接入 |
| 商机包件 | GET `/api/bidops/packages`, GET requirements | business.read | 已接入 |

创建：

```text
frontend/atlas-admin/docs/BIDOPS_FRONTEND_GAPS.md
```

记录后端待补接口：

```text
GET /api/bidops/dashboard/summary
GET /api/bidops/raw-notices/{id}/attachments
GET /api/bidops/raw-notices/{id}/versions
POST /api/bidops/raw-notices/{id}/reparse
GET /api/bidops/notices/{id}
GET /api/bidops/notices/{id}/packages
GET /api/bidops/packages/{id}
GET /api/bidops/packages/{id}/timeline
```

第二阶段再考虑：

```text
/api/bidops/suppliers
/api/bidops/matching
/api/bidops/pursuits
/api/bidops/compliance
```

---

## 21. README 要求

创建：

```text
frontend/atlas-admin/README.md
```

内容至少包含：

```md
# Atlas Admin Frontend

## 技术栈

Vue 3 + Vite + TypeScript + Element Plus + Pinia + Vue Router + Axios

## 启动

```bash
pnpm install
pnpm dev
```

## 构建

```bash
pnpm build
```

## 环境变量

复制 `.env.example` 为 `.env.local`。

```env
VITE_API_BASE_URL=/api
```

## 后端代理

开发环境默认把 `/api` 代理到 `https://localhost:5001`。

## BidOps 首版范围

- 采集来源
- 采集栏目
- 原始公告
- 手动公开 URL 导入
- 审核任务
- 正式公告库
- 商机包件
- 包件要求项
```

注意 README 里的 markdown 代码块需要正确闭合。

---

## 22. 验收标准

Codex 完成后必须运行：

```bash
cd frontend/atlas-admin
pnpm install
pnpm typecheck
pnpm build
```

如果使用 npm：

```bash
cd frontend/atlas-admin
npm install
npm run typecheck
npm run build
```

验收必须满足：

1. `pnpm build` 或 `npm run build` 成功。
2. 页面路由可打开。
3. 左侧菜单显示 BidOps 模块。
4. API baseURL 正确为 `/api`。
5. 列表页都有搜索、分页、加载态、空态。
6. 没有未处理的 TypeScript 编译错误。
7. 未接入的后端接口有文档记录，不臆造调用。
8. 采集、审核、业务读取的权限码和后端一致。
9. 页面无直接 `v-html` 展示原始公告。
10. README 与 docs 已创建。

---

## 23. 分阶段任务清单

### P0：工程骨架

任务：

```text
P0-01 创建 frontend/atlas-admin Vite Vue TS 项目
P0-02 安装 Element Plus / Pinia / Router / Axios / Dayjs / DOMPurify
P0-03 配置 vite alias 和 /api 代理
P0-04 创建 BasicLayout / BlankLayout
P0-05 创建 router / guards / stores
P0-06 创建 shared components 和 composables
P0-07 创建 README 与 env.example
```

验收：

```text
pnpm typecheck
pnpm build
```

### P1：BidOps 当前接口页面

任务：

```text
P1-01 创建 BidOps 类型与权限常量
P1-02 封装 6 组 API
P1-03 实现 BidOps 首页
P1-04 实现采集来源列表和表单
P1-05 实现采集栏目列表和表单
P1-06 实现原始公告列表、详情、手动 URL 导入
P1-07 实现审核任务列表和详情，支持通过/忽略
P1-08 实现正式公告列表
P1-09 实现商机包件列表和要求项详情
P1-10 创建 API mapping 和 gaps 文档
```

验收：

```text
pnpm typecheck
pnpm build
手动检查路由页面不白屏
```

### P2：后续预留，暂不强制实现

只预留菜单/文档，不调用接口：

```text
厂家能力库
匹配决策台
投标作业舱
合规风险中心
```

这些页面不要在首版菜单中显示，除非后端接口已实现。

---

## 24. 页面 UX 细节

### 24.1 加载态

每个列表页：

```text
loading 时显示 el-table v-loading
请求失败时 ElMessage 提示
无数据时显示 EmptyState
```

### 24.2 操作确认

以下操作必须确认：

```text
停用采集来源
立即扫描栏目
审核通过
忽略审核任务
```

### 24.3 日期格式

使用 `dayjs`：

```ts
export function formatDateTime(value?: string | null) {
  if (!value) return '-'
  return dayjs(value).format('YYYY-MM-DD HH:mm')
}
```

### 24.4 金额格式

```ts
export function formatMoney(value?: number | null) {
  if (value === null || value === undefined) return '-'
  return new Intl.NumberFormat('zh-CN', {
    style: 'currency',
    currency: 'CNY',
    maximumFractionDigits: 2,
  }).format(value)
}
```

---

## 25. Codex 执行后请输出的总结

执行完成后，请在最终回复中说明：

```text
1. 新增了哪些目录和文件
2. 已实现哪些页面
3. 已接入哪些 API
4. 哪些接口因后端缺失而只做了占位
5. 执行了哪些验证命令
6. 如果构建失败，失败原因和已定位的问题
```

不要只说“完成了”，必须给出可核查的文件和命令结果。

---

## 26. 最小可接受成果

如果时间有限，至少完成：

```text
frontend/atlas-admin 可构建
/api http 客户端
BidOps 路由
BasicLayout
采集来源列表
采集栏目列表
原始公告列表
审核任务列表
正式公告列表
商机包件列表
docs/BIDOPS_API_MAPPING.md
docs/BIDOPS_FRONTEND_GAPS.md
```

这就是首版可交付底线。

---

## 27. 质量要求

1. TypeScript 不使用 `any`，除非必须处理未知后端错误。
2. Vue 组件使用 `<script setup lang="ts">`。
3. API 封装独立于页面。
4. 页面不要重复写大量请求逻辑，尽量使用 `useTableQuery`。
5. 表单提交要有 loading 防重复。
6. 删除、停用、审核类动作要二次确认。
7. 业务组件放在 `modules/bidops/components`。
8. 通用组件放在 `shared/components`。
9. 不要在页面中硬编码 `/api`，统一通过 API 封装。
10. 不要吞掉错误；需要有可见提示。

---

## 28. 后续扩展方向，不在本次强制范围

后续可基于后端新增接口继续做：

```text
今日作业台 dashboard
原始公告附件与版本历史
公告详情
包件详情与时间轴
厂家能力库
包件-厂家匹配
投标作业舱
响应矩阵
合规风险中心
结果复盘
```

首版先把“采集 -> 原始公告 -> 审核 -> 正式公告 -> 商机包件”链路前端跑通。
