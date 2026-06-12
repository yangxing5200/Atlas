<script setup lang="ts">
import { useRouter } from 'vue-router'
import { Box, Collection, DataAnalysis, Document, List, Operation, Tickets } from '@element-plus/icons-vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import { useAuthStore } from '@/stores/auth.store'
import { BIDOPS_PERMISSIONS } from '../constants'

const router = useRouter()
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

const modules = [
  {
    title: '指挥中心',
    description: '汇总采集、审核、商机和后台任务状态。',
    path: '/bidops/dashboard',
    icon: DataAnalysis,
    permissions: bidOpsAnyRead,
    status: '已实现',
  },
  {
    title: '情报采集',
    description: '公开来源、采集栏目、原始公告和附件文本。',
    path: '/bidops/crawl/raw-notices',
    icon: Document,
    permissions: [BIDOPS_PERMISSIONS.CRAWL_READ],
    status: '已实现',
  },
  {
    title: '解析审核',
    description: '结构化暂存结果进入待审核池，人工确认后入库。',
    path: '/bidops/review/tasks',
    icon: Tickets,
    permissions: [BIDOPS_PERMISSIONS.REVIEW_READ],
    status: '已实现',
  },
  {
    title: '商机包件',
    description: '包件商机、阶段跟进、价值评估和要求项。',
    path: '/bidops/opportunities',
    icon: Box,
    permissions: [BIDOPS_PERMISSIONS.BUSINESS_READ, BIDOPS_PERMISSIONS.OPPORTUNITY_READ],
    status: '已实现',
  },
  {
    title: '采购画像',
    description: '采购方、代理机构和公开历史统计。',
    path: '/bidops/public-orgs/buyers',
    icon: Collection,
    permissions: bidOpsAnyRead,
    status: 'ComingSoon',
  },
  {
    title: '厂家能力',
    description: '厂家档案、材料有效期和能力标签。',
    path: '/bidops/suppliers',
    icon: Box,
    permissions: [BIDOPS_PERMISSIONS.SUPPLIER_READ],
    status: '已实现',
  },
  {
    title: '匹配决策',
    description: '包件匹配、匹配记录和立项决策。',
    path: '/bidops/matching/runs',
    icon: DataAnalysis,
    permissions: [BIDOPS_PERMISSIONS.MATCHING_READ],
    status: '已实现',
  },
  {
    title: '投标作业',
    description: '作业列表、我的任务和作业日历。',
    path: '/bidops/pursuits',
    icon: Operation,
    permissions: [BIDOPS_PERMISSIONS.PURSUIT_READ],
    status: '部分实现',
  },
  {
    title: '响应文件',
    description: '响应矩阵、文件清单、提交检查和模板库。',
    path: '/bidops/responses/matrix',
    icon: Document,
    permissions: bidOpsAnyRead,
    status: 'ComingSoon',
  },
  {
    title: '结果复盘',
    description: '结果录入、复盘列表和经营分析。',
    path: '/bidops/outcomes',
    icon: DataAnalysis,
    permissions: bidOpsAnyRead,
    status: 'ComingSoon',
  },
  {
    title: '合规审计',
    description: '合规检查、敏感词和操作审计。',
    path: '/bidops/compliance/checks',
    icon: Tickets,
    permissions: bidOpsAnyRead,
    status: 'ComingSoon',
  },
  {
    title: '运维监控',
    description: '任务看板、任务监控、采集健康和日志占位。',
    path: '/bidops/operations',
    icon: Operation,
    permissions: [BIDOPS_PERMISSIONS.CRAWL_READ],
    status: '已实现',
  },
  {
    title: '规则配置',
    description: '字典、匹配规则、合规规则和通知规则。',
    path: '/bidops/settings/dictionaries',
    icon: List,
    permissions: bidOpsAnyRead,
    status: 'ComingSoon',
  },
]

function statusType(status: string) {
  if (status === '已实现') return 'success'
  if (status === '部分实现') return 'warning'
  return 'info'
}
</script>

<template>
  <PageContainer title="指挥中心" description="BidOps 13 个业务模块入口。未接入后端的模块统一显示 ComingSoon。">
    <div class="home-grid">
      <article
        v-for="module in modules"
        v-show="auth.hasAnyPermission(module.permissions)"
        :key="module.path"
        class="module-card"
      >
        <div class="card-head">
          <div class="entry-icon">
            <el-icon :size="22"><component :is="module.icon" /></el-icon>
          </div>
          <el-tag :type="statusType(module.status)" effect="plain">{{ module.status }}</el-tag>
        </div>
        <h2>{{ module.title }}</h2>
        <p>{{ module.description }}</p>
        <el-button type="primary" @click="router.push(module.path)">进入</el-button>
      </article>
    </div>
  </PageContainer>
</template>

<style scoped>
.home-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 14px;
}

.module-card {
  display: grid;
  gap: 10px;
  min-height: 190px;
  padding: 18px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.card-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 10px;
}

.entry-icon {
  display: grid;
  width: 42px;
  height: 42px;
  place-items: center;
  border-radius: 8px;
  background: #e6f4f1;
  color: #0f766e;
}

.module-card h2 {
  margin: 0;
  font-size: 17px;
  line-height: 1.35;
}

.module-card p {
  margin: 0;
  color: #526071;
  line-height: 1.55;
}
</style>
