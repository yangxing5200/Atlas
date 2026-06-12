import type { RouteRecordRaw } from 'vue-router'
import { BIDOPS_PERMISSIONS } from '@/modules/bidops/constants'

export const operationsRoutes: RouteRecordRaw[] = [
  {
    path: '/ops',
    redirect: '/ops/jobs',
    meta: { title: '系统运维', permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ] },
  },
  {
    path: '/ops/jobs',
    name: 'OperationsBackgroundJobs',
    component: () => import('./pages/BackgroundJobListPage.vue'),
    meta: { title: '后台任务', permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ] },
  },
  {
    path: '/ops/jobs/:id',
    name: 'OperationsBackgroundJobDetail',
    component: () => import('./pages/BackgroundJobDetailPage.vue'),
    meta: { title: '后台任务详情', permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ] },
  },
  {
    path: '/ops/workers',
    name: 'OperationsWorkers',
    component: () => import('./pages/WorkerListPage.vue'),
    meta: { title: 'Worker 节点', permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ] },
  },
]
