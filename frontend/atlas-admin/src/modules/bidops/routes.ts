import type { RouteRecordRaw } from 'vue-router'
import { BIDOPS_PERMISSIONS } from './constants'

const BIDOPS_ANY_READ = [
  BIDOPS_PERMISSIONS.DASHBOARD_READ,
  BIDOPS_PERMISSIONS.CRAWL_READ,
  BIDOPS_PERMISSIONS.REVIEW_READ,
  BIDOPS_PERMISSIONS.BUSINESS_READ,
  BIDOPS_PERMISSIONS.OPPORTUNITY_READ,
  BIDOPS_PERMISSIONS.SUPPLIER_READ,
  BIDOPS_PERMISSIONS.MATCHING_READ,
  BIDOPS_PERMISSIONS.PURSUIT_READ,
]

interface ComingSoonRouteOptions {
  path: string
  name: string
  title: string
  moduleName: string
  permissionsAny?: string[]
}

function comingSoonRoute(options: ComingSoonRouteOptions): RouteRecordRaw {
  return {
    path: options.path,
    name: options.name,
    component: () => import('./pages/ComingSoonPage.vue'),
    props: {
      title: options.title,
      moduleName: options.moduleName,
    },
    meta: {
      title: options.title,
      permissionsAny: options.permissionsAny ?? BIDOPS_ANY_READ,
    },
  }
}

export const bidOpsRoutes: RouteRecordRaw[] = [
  {
    path: '/bidops',
    name: 'BidOpsHome',
    component: () => import('./pages/BidOpsHomePage.vue'),
    meta: {
      title: '指挥中心',
      permissionsAny: BIDOPS_ANY_READ,
    },
  },
  {
    path: '/bidops/dashboard',
    name: 'BidOpsDashboard',
    component: () => import('./pages/dashboard/BidOpsDashboardPage.vue'),
    meta: {
      title: '指挥中心看板',
      permissionsAny: [
        BIDOPS_PERMISSIONS.DASHBOARD_READ,
        BIDOPS_PERMISSIONS.CRAWL_READ,
        BIDOPS_PERMISSIONS.REVIEW_READ,
        BIDOPS_PERMISSIONS.BUSINESS_READ,
        BIDOPS_PERMISSIONS.OPPORTUNITY_READ,
        BIDOPS_PERMISSIONS.SUPPLIER_READ,
        BIDOPS_PERMISSIONS.MATCHING_READ,
        BIDOPS_PERMISSIONS.PURSUIT_READ,
      ],
    },
  },
  {
    path: '/bidops/crawl/sources',
    alias: ['/bidops/intelligence/sources'],
    name: 'BidOpsCrawlSources',
    component: () => import('./pages/crawl/CrawlSourceListPage.vue'),
    meta: { title: '采集来源', permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ] },
  },
  {
    path: '/bidops/crawl/channels',
    alias: ['/bidops/intelligence/channels'],
    name: 'BidOpsCrawlChannels',
    component: () => import('./pages/crawl/CrawlChannelListPage.vue'),
    meta: { title: '采集栏目', permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ] },
  },
  {
    path: '/bidops/crawl/raw-notices',
    alias: ['/bidops/intelligence/raw-notices'],
    name: 'BidOpsRawNotices',
    component: () => import('./pages/crawl/RawNoticeListPage.vue'),
    meta: { title: '原始公告', permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ] },
  },
  {
    path: '/bidops/crawl/raw-notices/:id',
    alias: ['/bidops/intelligence/raw-notices/:id'],
    name: 'BidOpsRawNoticeDetail',
    component: () => import('./pages/crawl/RawNoticeDetailPage.vue'),
    meta: { title: '原始公告详情', permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ] },
  },
  comingSoonRoute({
    path: '/bidops/intelligence/manual-import',
    name: 'BidOpsManualImportPlaceholder',
    title: '手动导入',
    moduleName: '情报采集中心',
    permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
  }),
  {
    path: '/bidops/intelligence/run-logs',
    alias: ['/bidops/crawl/run-logs'],
    name: 'BidOpsCrawlRunLogs',
    component: () => import('./pages/crawl/CrawlRunLogListPage.vue'),
    meta: { title: '采集运行日志', permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ] },
  },
  {
    path: '/bidops/intelligence/run-logs/:id',
    alias: ['/bidops/crawl/run-logs/:id'],
    name: 'BidOpsCrawlRunLogDetail',
    component: () => import('./pages/crawl/CrawlRunLogDetailPage.vue'),
    meta: { title: '采集运行日志详情', permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ] },
  },
  {
    path: '/bidops/review/tasks',
    alias: ['/bidops/processing/review-tasks'],
    name: 'BidOpsReviewTasks',
    component: () => import('./pages/review/ReviewTaskListPage.vue'),
    meta: { title: '待审核池', permissionsAny: [BIDOPS_PERMISSIONS.REVIEW_READ] },
  },
  {
    path: '/bidops/review/tasks/:id',
    alias: ['/bidops/processing/review-tasks/:id'],
    name: 'BidOpsReviewTaskDetail',
    component: () => import('./pages/review/ReviewTaskDetailPage.vue'),
    meta: { title: '审核详情', permissionsAny: [BIDOPS_PERMISSIONS.REVIEW_READ] },
  },
  {
    path: '/bidops/processing/failed',
    name: 'BidOpsProcessingFailed',
    component: () => import('./pages/review/ProcessingFailureListPage.vue'),
    meta: { title: '解析失败', permissionsAny: [BIDOPS_PERMISSIONS.REVIEW_READ] },
  },
  comingSoonRoute({
    path: '/bidops/processing/duplicates',
    name: 'BidOpsProcessingDuplicatesPlaceholder',
    title: '疑似重复',
    moduleName: '解析审核中心',
    permissionsAny: [BIDOPS_PERMISSIONS.REVIEW_READ],
  }),
  comingSoonRoute({
    path: '/bidops/processing/versions',
    name: 'BidOpsProcessingVersionsPlaceholder',
    title: '变更版本',
    moduleName: '解析审核中心',
    permissionsAny: [BIDOPS_PERMISSIONS.REVIEW_READ],
  }),
  {
    path: '/bidops/notices',
    name: 'BidOpsNotices',
    component: () => import('./pages/business/NoticeListPage.vue'),
    meta: { title: '正式公告库', permissionsAny: [BIDOPS_PERMISSIONS.BUSINESS_READ] },
  },
  {
    path: '/bidops/packages',
    name: 'BidOpsPackages',
    component: () => import('./pages/business/PackageListPage.vue'),
    meta: { title: '包件中心', permissionsAny: [BIDOPS_PERMISSIONS.BUSINESS_READ] },
  },
  {
    path: '/bidops/packages/:id',
    name: 'BidOpsPackageDetail',
    component: () => import('./pages/business/PackageDetailPage.vue'),
    meta: { title: '包件详情', permissionsAny: [BIDOPS_PERMISSIONS.BUSINESS_READ] },
  },
  {
    path: '/bidops/opportunities',
    name: 'BidOpsOpportunities',
    component: () => import('./pages/business/OpportunityListPage.vue'),
    meta: {
      title: '商机列表',
      permissionsAny: [BIDOPS_PERMISSIONS.BUSINESS_READ, BIDOPS_PERMISSIONS.OPPORTUNITY_READ],
    },
  },
  {
    path: '/bidops/opportunities/:id',
    name: 'BidOpsOpportunityDetail',
    component: () => import('./pages/business/OpportunityDetailPage.vue'),
    meta: {
      title: '商机详情',
      permissionsAny: [BIDOPS_PERMISSIONS.BUSINESS_READ, BIDOPS_PERMISSIONS.OPPORTUNITY_READ],
    },
  },
  {
    path: '/bidops/opportunities/watchlist',
    name: 'BidOpsOpportunityWatchlist',
    component: () => import('./pages/business/OpportunityListPage.vue'),
    props: { watchedOnly: true },
    meta: {
      title: '关注商机',
      permissionsAny: [BIDOPS_PERMISSIONS.BUSINESS_READ, BIDOPS_PERMISSIONS.OPPORTUNITY_READ],
    },
  },
  comingSoonRoute({
    path: '/bidops/opportunities/calendar',
    name: 'BidOpsOpportunityCalendarPlaceholder',
    title: '截止日历',
    moduleName: '商机包件中心',
    permissionsAny: [BIDOPS_PERMISSIONS.BUSINESS_READ],
  }),
  comingSoonRoute({
    path: '/bidops/public-orgs/buyers',
    name: 'BidOpsBuyerProfilesPlaceholder',
    title: '采购方',
    moduleName: '采购方与代理机构画像',
  }),
  comingSoonRoute({
    path: '/bidops/public-orgs/agencies',
    name: 'BidOpsAgencyProfilesPlaceholder',
    title: '代理机构',
    moduleName: '采购方与代理机构画像',
  }),
  comingSoonRoute({
    path: '/bidops/public-orgs/stats',
    name: 'BidOpsPublicOrgStatsPlaceholder',
    title: '公开历史统计',
    moduleName: '采购方与代理机构画像',
  }),
  {
    path: '/bidops/suppliers',
    name: 'BidOpsSuppliers',
    component: () => import('./pages/suppliers/SupplierListPage.vue'),
    meta: { title: '厂家列表', permissionsAny: [BIDOPS_PERMISSIONS.SUPPLIER_READ] },
  },
  comingSoonRoute({
    path: '/bidops/suppliers/evidence-expiry',
    name: 'BidOpsSupplierEvidenceExpiryPlaceholder',
    title: '材料有效期',
    moduleName: '厂家能力库',
    permissionsAny: [BIDOPS_PERMISSIONS.SUPPLIER_READ],
  }),
  {
    path: '/bidops/suppliers/analysis',
    alias: ['/bidops/suppliers/capabilities'],
    name: 'BidOpsSupplierAnalysis',
    component: () => import('./pages/suppliers/SupplierAnalysisPage.vue'),
    meta: { title: '厂家分析', permissionsAny: [BIDOPS_PERMISSIONS.SUPPLIER_READ] },
  },
  {
    path: '/bidops/suppliers/:id',
    name: 'BidOpsSupplierDetail',
    component: () => import('./pages/suppliers/SupplierDetailPage.vue'),
    meta: { title: '厂家详情', permissionsAny: [BIDOPS_PERMISSIONS.SUPPLIER_READ] },
  },
  {
    path: '/bidops/matching/packages',
    name: 'BidOpsPackageMatching',
    component: () => import('./pages/matching/MatchingRunListPage.vue'),
    props: { packageMode: true },
    meta: { title: '包件匹配', permissionsAny: [BIDOPS_PERMISSIONS.MATCHING_READ] },
  },
  {
    path: '/bidops/matching/runs',
    name: 'BidOpsMatchingRuns',
    component: () => import('./pages/matching/MatchingRunListPage.vue'),
    meta: { title: '匹配记录', permissionsAny: [BIDOPS_PERMISSIONS.MATCHING_READ] },
  },
  {
    path: '/bidops/matching/runs/:id',
    name: 'BidOpsMatchingRunDetail',
    component: () => import('./pages/matching/MatchingRunDetailPage.vue'),
    meta: { title: '匹配详情', permissionsAny: [BIDOPS_PERMISSIONS.MATCHING_READ] },
  },
  {
    path: '/bidops/matching/decisions',
    name: 'BidOpsGoNoGoDecisions',
    component: () => import('./pages/matching/MatchingRunListPage.vue'),
    props: { decisionMode: true },
    meta: { title: '立项决策', permissionsAny: [BIDOPS_PERMISSIONS.MATCHING_READ] },
  },
  comingSoonRoute({
    path: '/bidops/matching/rules-preview',
    name: 'BidOpsMatchingRulesPreviewPlaceholder',
    title: '规则预览',
    moduleName: '匹配与立项决策台',
    permissionsAny: [BIDOPS_PERMISSIONS.MATCHING_READ],
  }),
  {
    path: '/bidops/pursuits',
    name: 'BidOpsPursuits',
    component: () => import('./pages/pursuits/PursuitListPage.vue'),
    meta: { title: '作业列表', permissionsAny: [BIDOPS_PERMISSIONS.PURSUIT_READ] },
  },
  {
    path: '/bidops/pursuits/:id',
    name: 'BidOpsPursuitDetail',
    component: () => import('./pages/pursuits/PursuitDetailPage.vue'),
    meta: { title: '作业详情', permissionsAny: [BIDOPS_PERMISSIONS.PURSUIT_READ] },
  },
  {
    path: '/bidops/pursuits/my-tasks',
    name: 'BidOpsPursuitMyTasks',
    component: () => import('./pages/pursuits/PursuitListPage.vue'),
    props: { mineOnly: true },
    meta: { title: '我的任务', permissionsAny: [BIDOPS_PERMISSIONS.PURSUIT_READ] },
  },
  comingSoonRoute({
    path: '/bidops/pursuits/calendar',
    name: 'BidOpsPursuitCalendarPlaceholder',
    title: '作业日历',
    moduleName: '投标作业中心',
  }),
  comingSoonRoute({
    path: '/bidops/responses/matrix',
    name: 'BidOpsResponseMatrixPlaceholder',
    title: '响应矩阵',
    moduleName: '响应矩阵与文件中心',
  }),
  comingSoonRoute({
    path: '/bidops/responses/files',
    name: 'BidOpsResponseFilesPlaceholder',
    title: '文件清单',
    moduleName: '响应矩阵与文件中心',
  }),
  comingSoonRoute({
    path: '/bidops/responses/submission-checks',
    name: 'BidOpsSubmissionChecksPlaceholder',
    title: '提交检查',
    moduleName: '响应矩阵与文件中心',
  }),
  comingSoonRoute({
    path: '/bidops/responses/templates',
    name: 'BidOpsDocumentTemplatesPlaceholder',
    title: '模板库',
    moduleName: '响应矩阵与文件中心',
  }),
  comingSoonRoute({
    path: '/bidops/outcomes',
    name: 'BidOpsOutcomesPlaceholder',
    title: '结果录入',
    moduleName: '结果复盘中心',
  }),
  comingSoonRoute({
    path: '/bidops/outcomes/reviews',
    name: 'BidOpsOutcomeReviewsPlaceholder',
    title: '复盘列表',
    moduleName: '结果复盘中心',
  }),
  comingSoonRoute({
    path: '/bidops/outcomes/analytics',
    name: 'BidOpsOutcomeAnalyticsPlaceholder',
    title: '经营分析',
    moduleName: '结果复盘中心',
  }),
  comingSoonRoute({
    path: '/bidops/compliance/checks',
    name: 'BidOpsComplianceChecksPlaceholder',
    title: '合规检查',
    moduleName: '合规风控与审计中心',
  }),
  comingSoonRoute({
    path: '/bidops/compliance/sensitive-words',
    name: 'BidOpsSensitiveWordsPlaceholder',
    title: '敏感词',
    moduleName: '合规风控与审计中心',
  }),
  comingSoonRoute({
    path: '/bidops/compliance/audit',
    name: 'BidOpsAuditPlaceholder',
    title: '操作审计',
    moduleName: '合规风控与审计中心',
  }),
  {
    path: '/bidops/operations',
    name: 'BidOpsOperations',
    component: () => import('./pages/operations/BidOpsOperationsDashboardPage.vue'),
    meta: { title: '任务看板', permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ] },
  },
  {
    path: '/bidops/operations/jobs',
    name: 'BidOpsOperationsJobs',
    component: () => import('@/modules/operations/pages/BackgroundJobListPage.vue'),
    meta: { title: 'BidOps 后台任务', permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ] },
  },
  {
    path: '/bidops/operations/channels',
    name: 'BidOpsOperationsChannels',
    component: () => import('./pages/operations/BidOpsChannelHealthPage.vue'),
    meta: { title: '采集健康', permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ] },
  },
  comingSoonRoute({
    path: '/bidops/operations/logs',
    name: 'BidOpsOperationLogsPlaceholder',
    title: '日志查询',
    moduleName: '后台任务与日志监控中心',
    permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
  }),
  {
    path: '/bidops/operations/worker-heartbeats',
    name: 'BidOpsWorkerHeartbeats',
    component: () => import('@/modules/operations/pages/WorkerListPage.vue'),
    meta: { title: 'Worker 心跳', permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ] },
  },
  comingSoonRoute({
    path: '/bidops/settings/dictionaries',
    name: 'BidOpsDictionariesPlaceholder',
    title: '字典配置',
    moduleName: '规则与配置中心',
  }),
  comingSoonRoute({
    path: '/bidops/settings/matching-rules',
    name: 'BidOpsMatchingRulesPlaceholder',
    title: '匹配规则',
    moduleName: '规则与配置中心',
  }),
  comingSoonRoute({
    path: '/bidops/settings/compliance-rules',
    name: 'BidOpsComplianceRulesPlaceholder',
    title: '合规规则',
    moduleName: '规则与配置中心',
  }),
  comingSoonRoute({
    path: '/bidops/settings/ai-prompts',
    name: 'BidOpsAiPromptsPlaceholder',
    title: 'AI Prompt',
    moduleName: '规则与配置中心',
  }),
  comingSoonRoute({
    path: '/bidops/settings/notifications',
    name: 'BidOpsNotificationRulesPlaceholder',
    title: '通知规则',
    moduleName: '规则与配置中心',
  }),
]
