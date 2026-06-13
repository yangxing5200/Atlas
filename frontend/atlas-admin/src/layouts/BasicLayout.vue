<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Box,
  Collection,
  DataAnalysis,
  Document,
  Fold,
  HomeFilled,
  List,
  Menu as MenuIcon,
  Operation,
  Tickets,
  User,
} from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { authApi } from '@/api/auth.api'
import { BIDOPS_PERMISSIONS } from '@/modules/bidops/constants'
import { useAppStore } from '@/stores/app.store'
import { useAuthStore } from '@/stores/auth.store'

interface MenuItem {
  index: string
  title: string
  icon?: unknown
  permissionsAny?: string[]
  children?: MenuItem[]
}

const route = useRoute()
const router = useRouter()
const app = useAppStore()
const auth = useAuthStore()

const bidOpsAnyRead = [
  BIDOPS_PERMISSIONS.DASHBOARD_READ,
  BIDOPS_PERMISSIONS.CRAWL_READ,
  BIDOPS_PERMISSIONS.REVIEW_READ,
  BIDOPS_PERMISSIONS.BUSINESS_READ,
  BIDOPS_PERMISSIONS.OPPORTUNITY_READ,
  BIDOPS_PERMISSIONS.SUPPLIER_READ,
  BIDOPS_PERMISSIONS.MATCHING_READ,
  BIDOPS_PERMISSIONS.PURSUIT_READ,
]

const menuTree: MenuItem[] = [
  {
    index: 'ops',
    title: '系统运维',
    icon: Operation,
    permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
    children: [
      {
        index: '/ops/jobs',
        title: '后台任务',
        icon: DataAnalysis,
        permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
      },
      {
        index: '/ops/workers',
        title: 'Worker 节点',
        icon: Operation,
        permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
      },
    ],
  },
  {
    index: '/bidops/dashboard',
    title: '指挥中心',
    icon: HomeFilled,
    permissionsAny: bidOpsAnyRead,
  },
  {
    index: 'bidops-intelligence',
    title: '情报采集',
    icon: DataAnalysis,
    permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
    children: [
      {
        index: '/bidops/crawl/sources',
        title: '采集来源',
        icon: Operation,
        permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
      },
      {
        index: '/bidops/crawl/channels',
        title: '采集栏目',
        icon: List,
        permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
      },
      {
        index: '/bidops/crawl/raw-notices',
        title: '原始公告',
        icon: Document,
        permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
      },
      {
        index: '/bidops/intelligence/manual-import',
        title: '手动导入',
        icon: Operation,
        permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_IMPORT],
      },
      {
        index: '/bidops/intelligence/run-logs',
        title: '采集运行日志',
        icon: DataAnalysis,
        permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
      },
    ],
  },
  {
    index: 'bidops-processing',
    title: '解析审核',
    icon: Tickets,
    permissionsAny: [BIDOPS_PERMISSIONS.REVIEW_READ],
    children: [
      {
        index: '/bidops/review/tasks',
        title: '待审核池',
        icon: Tickets,
        permissionsAny: [BIDOPS_PERMISSIONS.REVIEW_READ],
      },
      {
        index: '/bidops/processing/failed',
        title: '解析失败',
        icon: List,
        permissionsAny: [BIDOPS_PERMISSIONS.REVIEW_READ],
      },
      {
        index: '/bidops/processing/duplicates',
        title: '疑似重复',
        icon: Collection,
        permissionsAny: [BIDOPS_PERMISSIONS.REVIEW_READ],
      },
      {
        index: '/bidops/processing/versions',
        title: '变更版本',
        icon: Document,
        permissionsAny: [BIDOPS_PERMISSIONS.REVIEW_READ],
      },
    ],
  },
  {
    index: 'bidops-opportunities',
    title: '商机包件',
    icon: Box,
    permissionsAny: [BIDOPS_PERMISSIONS.BUSINESS_READ, BIDOPS_PERMISSIONS.OPPORTUNITY_READ],
    children: [
      {
        index: '/bidops/opportunities',
        title: '商机列表',
        icon: Box,
        permissionsAny: [BIDOPS_PERMISSIONS.BUSINESS_READ, BIDOPS_PERMISSIONS.OPPORTUNITY_READ],
      },
      {
        index: '/bidops/opportunities/watchlist',
        title: '关注商机',
        icon: Tickets,
        permissionsAny: [BIDOPS_PERMISSIONS.BUSINESS_READ, BIDOPS_PERMISSIONS.OPPORTUNITY_READ],
      },
      {
        index: '/bidops/notices',
        title: '正式公告库',
        icon: Collection,
        permissionsAny: [BIDOPS_PERMISSIONS.BUSINESS_READ],
      },
      {
        index: '/bidops/packages',
        title: '包件中心',
        icon: Box,
        permissionsAny: [BIDOPS_PERMISSIONS.BUSINESS_READ],
      },
      {
        index: '/bidops/opportunities/calendar',
        title: '截止日历',
        icon: List,
        permissionsAny: [BIDOPS_PERMISSIONS.BUSINESS_READ],
      },
    ],
  },
  {
    index: 'bidops-public-orgs',
    title: '采购画像',
    icon: Collection,
    permissionsAny: bidOpsAnyRead,
    children: [
      {
        index: '/bidops/public-orgs/buyers',
        title: '采购方',
        icon: Collection,
        permissionsAny: bidOpsAnyRead,
      },
      {
        index: '/bidops/public-orgs/agencies',
        title: '代理机构',
        icon: Operation,
        permissionsAny: bidOpsAnyRead,
      },
      {
        index: '/bidops/public-orgs/stats',
        title: '公开历史统计',
        icon: DataAnalysis,
        permissionsAny: bidOpsAnyRead,
      },
    ],
  },
  {
    index: 'bidops-suppliers',
    title: '厂家能力',
    icon: Box,
    permissionsAny: [BIDOPS_PERMISSIONS.SUPPLIER_READ],
    children: [
      {
        index: '/bidops/suppliers',
        title: '厂家列表',
        icon: List,
        permissionsAny: [BIDOPS_PERMISSIONS.SUPPLIER_READ],
      },
      {
        index: '/bidops/suppliers/evidence-expiry',
        title: '材料有效期',
        icon: Document,
        permissionsAny: [BIDOPS_PERMISSIONS.SUPPLIER_READ],
      },
      {
        index: '/bidops/suppliers/analysis',
        title: '厂家分析',
        icon: DataAnalysis,
        permissionsAny: [BIDOPS_PERMISSIONS.SUPPLIER_READ],
      },
    ],
  },
  {
    index: 'bidops-matching',
    title: '匹配决策',
    icon: DataAnalysis,
    permissionsAny: [BIDOPS_PERMISSIONS.MATCHING_READ],
    children: [
      {
        index: '/bidops/matching/packages',
        title: '包件匹配',
        icon: Box,
        permissionsAny: [BIDOPS_PERMISSIONS.MATCHING_READ],
      },
      {
        index: '/bidops/matching/runs',
        title: '匹配记录',
        icon: List,
        permissionsAny: [BIDOPS_PERMISSIONS.MATCHING_READ],
      },
      {
        index: '/bidops/matching/decisions',
        title: '立项决策',
        icon: Tickets,
        permissionsAny: [BIDOPS_PERMISSIONS.MATCHING_READ],
      },
    ],
  },
  {
    index: 'bidops-pursuits',
    title: '投标作业',
    icon: Operation,
    permissionsAny: [BIDOPS_PERMISSIONS.PURSUIT_READ],
    children: [
      {
        index: '/bidops/pursuits',
        title: '作业列表',
        icon: List,
        permissionsAny: [BIDOPS_PERMISSIONS.PURSUIT_READ],
      },
      {
        index: '/bidops/pursuits/my-tasks',
        title: '我的任务',
        icon: Tickets,
        permissionsAny: [BIDOPS_PERMISSIONS.PURSUIT_READ],
      },
      {
        index: '/bidops/pursuits/calendar',
        title: '作业日历',
        icon: DataAnalysis,
        permissionsAny: [BIDOPS_PERMISSIONS.PURSUIT_READ],
      },
    ],
  },
  {
    index: 'bidops-responses',
    title: '响应文件',
    icon: Document,
    permissionsAny: bidOpsAnyRead,
    children: [
      {
        index: '/bidops/responses/matrix',
        title: '响应矩阵',
        icon: List,
        permissionsAny: bidOpsAnyRead,
      },
      {
        index: '/bidops/responses/files',
        title: '文件清单',
        icon: Document,
        permissionsAny: bidOpsAnyRead,
      },
      {
        index: '/bidops/responses/submission-checks',
        title: '提交检查',
        icon: Tickets,
        permissionsAny: bidOpsAnyRead,
      },
      {
        index: '/bidops/responses/templates',
        title: '模板库',
        icon: Collection,
        permissionsAny: bidOpsAnyRead,
      },
    ],
  },
  {
    index: 'bidops-outcomes',
    title: '结果复盘',
    icon: DataAnalysis,
    permissionsAny: bidOpsAnyRead,
    children: [
      {
        index: '/bidops/outcomes',
        title: '结果录入',
        icon: Tickets,
        permissionsAny: bidOpsAnyRead,
      },
      {
        index: '/bidops/outcomes/reviews',
        title: '复盘列表',
        icon: List,
        permissionsAny: bidOpsAnyRead,
      },
      {
        index: '/bidops/outcomes/analytics',
        title: '经营分析',
        icon: DataAnalysis,
        permissionsAny: bidOpsAnyRead,
      },
    ],
  },
  {
    index: 'bidops-compliance',
    title: '合规审计',
    icon: Tickets,
    permissionsAny: bidOpsAnyRead,
    children: [
      {
        index: '/bidops/compliance/checks',
        title: '合规检查',
        icon: Tickets,
        permissionsAny: bidOpsAnyRead,
      },
      {
        index: '/bidops/compliance/sensitive-words',
        title: '敏感词',
        icon: Document,
        permissionsAny: bidOpsAnyRead,
      },
      {
        index: '/bidops/compliance/audit',
        title: '操作审计',
        icon: DataAnalysis,
        permissionsAny: bidOpsAnyRead,
      },
    ],
  },
  {
    index: 'bidops-operations',
    title: '运维监控',
    icon: Operation,
    permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
    children: [
      {
        index: '/bidops/operations',
        title: '任务看板',
        icon: DataAnalysis,
        permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
      },
      {
        index: '/bidops/operations/jobs',
        title: '任务监控',
        icon: Tickets,
        permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
      },
      {
        index: '/bidops/operations/channels',
        title: '采集健康',
        icon: List,
        permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
      },
      {
        index: '/bidops/operations/logs',
        title: '日志查询',
        icon: Document,
        permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
      },
      {
        index: '/bidops/operations/worker-heartbeats',
        title: 'Worker 心跳',
        icon: Operation,
        permissionsAny: [BIDOPS_PERMISSIONS.CRAWL_READ],
      },
    ],
  },
  {
    index: 'bidops-settings',
    title: '规则配置',
    icon: Operation,
    permissionsAny: bidOpsAnyRead,
    children: [
      {
        index: '/bidops/settings/dictionaries',
        title: '字典配置',
        icon: List,
        permissionsAny: bidOpsAnyRead,
      },
      {
        index: '/bidops/settings/matching-rules',
        title: '匹配规则',
        icon: DataAnalysis,
        permissionsAny: bidOpsAnyRead,
      },
      {
        index: '/bidops/settings/compliance-rules',
        title: '合规规则',
        icon: Tickets,
        permissionsAny: bidOpsAnyRead,
      },
      {
        index: '/bidops/settings/ai-prompts',
        title: 'AI Prompt',
        icon: Document,
        permissionsAny: bidOpsAnyRead,
      },
      {
        index: '/bidops/settings/notifications',
        title: '通知规则',
        icon: Operation,
        permissionsAny: bidOpsAnyRead,
      },
    ],
  },
]

function canSee(item: MenuItem) {
  return auth.hasAnyPermission(item.permissionsAny || [])
}

function filterMenus(items: MenuItem[]): MenuItem[] {
  return items
    .map((item) => ({
      ...item,
      children: item.children ? filterMenus(item.children) : undefined,
    }))
    .filter((item) => canSee(item) && (!item.children || item.children.length > 0))
}

const menus = computed(() => filterMenus(menuTree))
const activeMenu = computed(() => {
  if (route.path.startsWith('/ops/jobs')) return '/ops/jobs'
  if (route.path.startsWith('/bidops/operations/channels')) return '/bidops/operations/channels'
  if (route.path.startsWith('/bidops/operations/jobs')) return '/bidops/operations/jobs'
  if (route.path.startsWith('/bidops/operations/logs')) return '/bidops/operations/logs'
  if (route.path.startsWith('/bidops/operations/worker-heartbeats')) return '/bidops/operations/worker-heartbeats'
  if (route.path.startsWith('/bidops/operations')) return '/bidops/operations'
  if (route.path.startsWith('/bidops/intelligence/sources')) return '/bidops/crawl/sources'
  if (route.path.startsWith('/bidops/intelligence/channels')) return '/bidops/crawl/channels'
  if (route.path.startsWith('/bidops/intelligence/raw-notices')) return '/bidops/crawl/raw-notices'
  if (route.path.startsWith('/bidops/crawl/raw-notices')) return '/bidops/crawl/raw-notices'
  if (route.path.startsWith('/bidops/processing/review-tasks')) return '/bidops/review/tasks'
  if (route.path.startsWith('/bidops/review/tasks')) return '/bidops/review/tasks'
  if (route.path.startsWith('/bidops/packages')) return '/bidops/packages'
  if (route.path.startsWith('/bidops/matching/packages')) return '/bidops/matching/packages'
  if (route.path.startsWith('/bidops/matching/decisions')) return '/bidops/matching/decisions'
  if (route.path.startsWith('/bidops/matching/runs')) return '/bidops/matching/runs'
  if (route.path.startsWith('/bidops/pursuits/my-tasks')) return '/bidops/pursuits/my-tasks'
  if (route.path.startsWith('/bidops/pursuits/calendar')) return '/bidops/pursuits/calendar'
  if (route.path.startsWith('/bidops/pursuits')) return '/bidops/pursuits'
  if (route.path.startsWith('/bidops/suppliers/evidence-expiry')) return '/bidops/suppliers/evidence-expiry'
  if (route.path.startsWith('/bidops/suppliers/analysis')) return '/bidops/suppliers/analysis'
  if (route.path.startsWith('/bidops/suppliers/capabilities')) return '/bidops/suppliers/analysis'
  if (route.path.startsWith('/bidops/suppliers')) return '/bidops/suppliers'
  return route.path
})

function onSelect(index: string) {
  if (index.startsWith('/')) {
    router.push(index)
  }
}

async function logout() {
  try {
    if (auth.token) {
      await authApi.logout()
    }
  } catch {
    // Token may already be invalid; local logout should still proceed.
  } finally {
    auth.logout()
    ElMessage.success('已退出登录')
    await router.push('/login')
  }
}
</script>

<template>
  <el-container class="layout-shell">
    <el-aside :width="app.sidebarCollapsed ? '64px' : '236px'" class="layout-aside">
      <div class="brand">
        <div class="brand-mark">A</div>
        <span v-if="!app.sidebarCollapsed">Atlas Admin</span>
      </div>
      <el-menu
        :default-active="activeMenu"
        :collapse="app.sidebarCollapsed"
        class="side-menu"
        @select="onSelect"
      >
        <template v-for="item in menus" :key="item.index">
          <el-sub-menu v-if="item.children?.length" :index="item.index">
            <template #title>
              <el-icon v-if="item.icon"><component :is="item.icon" /></el-icon>
              <span>{{ item.title }}</span>
            </template>
            <el-menu-item v-for="child in item.children" :key="child.index" :index="child.index">
              <el-icon v-if="child.icon"><component :is="child.icon" /></el-icon>
              <span>{{ child.title }}</span>
            </el-menu-item>
          </el-sub-menu>
          <el-menu-item v-else :index="item.index">
            <el-icon v-if="item.icon"><component :is="item.icon" /></el-icon>
            <span>{{ item.title }}</span>
          </el-menu-item>
        </template>
      </el-menu>
    </el-aside>

    <el-container>
      <el-header class="layout-header">
        <el-button text :icon="app.sidebarCollapsed ? MenuIcon : Fold" @click="app.toggleSidebar()" />
        <div class="header-spacer" />
        <div class="context-pill">Tenant {{ auth.tenantId || '-' }}</div>
        <div class="context-pill">Store {{ auth.storeId || '-' }}</div>
        <el-dropdown>
          <button class="user-trigger" type="button">
            <el-icon><User /></el-icon>
            <span>{{ auth.displayName }}</span>
          </button>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item disabled>{{ auth.user?.userName || '-' }}</el-dropdown-item>
              <el-dropdown-item divided @click="logout">退出登录</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </el-header>
      <el-main class="layout-main">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<style scoped>
.layout-shell {
  min-height: 100vh;
}

.layout-aside {
  border-right: 1px solid #dce3ee;
  background: #fff;
  transition: width 0.2s ease;
}

.brand {
  display: flex;
  align-items: center;
  gap: 10px;
  height: 56px;
  padding: 0 16px;
  color: #17202a;
  font-weight: 700;
  white-space: nowrap;
}

.brand-mark {
  display: grid;
  width: 32px;
  height: 32px;
  place-items: center;
  border-radius: 8px;
  background: #0f766e;
  color: #fff;
}

.side-menu {
  height: calc(100vh - 56px);
  border-right: 0;
  overflow-y: auto;
}

.layout-header {
  display: flex;
  align-items: center;
  gap: 10px;
  height: 56px;
  border-bottom: 1px solid #dce3ee;
  background: #fff;
}

.header-spacer {
  flex: 1;
}

.context-pill {
  max-width: 180px;
  overflow: hidden;
  padding: 5px 10px;
  border: 1px solid #dce3ee;
  border-radius: 999px;
  color: #526071;
  font-size: 13px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.user-trigger {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  max-width: 180px;
  padding: 6px 8px;
  border: 0;
  background: transparent;
  color: #334155;
  cursor: pointer;
}

.user-trigger span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.layout-main {
  min-width: 0;
  padding: 20px;
  background: #f5f7fb;
}

@media (max-width: 760px) {
  .layout-aside {
    width: 64px !important;
  }

  .context-pill {
    display: none;
  }
}
</style>
