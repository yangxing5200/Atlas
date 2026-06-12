import type { RouteRecordRaw } from 'vue-router'
import BasicLayout from '@/layouts/BasicLayout.vue'
import BlankLayout from '@/layouts/BlankLayout.vue'
import { bidOpsRoutes } from '@/modules/bidops/routes'
import { operationsRoutes } from '@/modules/operations/routes'

export const routes: RouteRecordRaw[] = [
  {
    path: '/',
    component: BasicLayout,
    redirect: '/bidops',
    children: [
      {
        path: '403',
        name: 'Forbidden',
        component: () => import('@/shared/components/ForbiddenPage.vue'),
        meta: { title: '无权限' },
      },
      ...operationsRoutes,
      ...bidOpsRoutes,
    ],
  },
  {
    path: '/blank',
    component: BlankLayout,
    children: [
      {
        path: '/login',
        name: 'Login',
        component: () => import('@/pages/LoginPage.vue'),
        meta: { title: '登录', public: true },
      },
    ],
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/bidops',
  },
]
