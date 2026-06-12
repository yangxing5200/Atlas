<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Refresh, Tickets, View, WarningFilled } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'
import { bidOpsDashboardApi } from '@/api/bidops/dashboard.api'
import DataTable from '@/shared/components/DataTable.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import { formatDateTime } from '@/shared/utils/date'
import type {
  BidOpsDashboardDeadlineRiskDto,
  BidOpsDashboardSummaryDto,
} from '@/modules/bidops/types'
import {
  formatOpportunityDecision,
  formatOpportunityStage,
  formatOpportunityValueLevel,
} from '@/modules/bidops/utils/display'

const router = useRouter()
const loading = ref(false)
const summary = ref<BidOpsDashboardSummaryDto | null>(null)

const metrics = computed(() => {
  const data = summary.value
  return [
    { label: '今日公告', value: data?.rawNoticeCreatedToday ?? 0 },
    { label: '今日审核', value: data?.reviewTaskCreatedToday ?? 0 },
    { label: '待审核', value: data?.pendingReviewTasks ?? 0 },
    { label: '今日入库', value: data?.formalNoticeCreatedToday ?? 0 },
    { label: '今日包件', value: data?.packageCreatedToday ?? 0 },
    { label: '有效包件', value: data?.activePackageCount ?? 0 },
    { label: '有效商机', value: data?.activeOpportunityCount ?? 0 },
    { label: '截止风险', value: data?.deadlineRiskCount ?? 0 },
  ]
})

const stageTotal = computed(() =>
  Math.max(1, (summary.value?.opportunityStageFunnel || []).reduce((total, item) => total + item.count, 0)),
)

async function loadData() {
  loading.value = true
  try {
    summary.value = await bidOpsDashboardApi.summary()
  } finally {
    loading.value = false
  }
}

function bucketWidth(count: number) {
  return `${Math.max(4, Math.round((count / stageTotal.value) * 100))}%`
}

function openRoute(route: string) {
  if (route) router.push(route)
}

function openOpportunity(id: string | number) {
  router.push(`/bidops/opportunities/${id}`)
}

function todoTypeLabel(type: string) {
  if (type === 'ReviewTask') return '审核'
  if (type === 'Opportunity') return '商机'
  return type || '-'
}

function todoTypeTag(type: string) {
  if (type === 'ReviewTask') return 'warning'
  if (type === 'Opportunity') return 'success'
  return 'info'
}

function deadlineRiskLabel(row: BidOpsDashboardDeadlineRiskDto) {
  if (row.riskLevel === 'Overdue') return '已逾期'
  if (row.riskLevel === 'Urgent') return '3 日内'
  return '7 日内'
}

function deadlineRiskTag(row: BidOpsDashboardDeadlineRiskDto) {
  if (row.riskLevel === 'Overdue') return 'danger'
  if (row.riskLevel === 'Urgent') return 'warning'
  return 'info'
}

function formatMoney(value?: number | null) {
  if (value === null || value === undefined) return '-'
  return new Intl.NumberFormat('zh-CN', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  }).format(value)
}

onMounted(loadData)
</script>

<template>
  <PageContainer title="指挥中心" description="BidOps 业务指标、待办和截止风险。">
    <template #actions>
      <el-button :icon="Tickets" @click="router.push('/bidops/review/tasks')">待审核</el-button>
      <el-button :icon="View" @click="router.push('/bidops/opportunities')">商机列表</el-button>
      <el-button :icon="Refresh" :loading="loading" @click="loadData">刷新</el-button>
    </template>

    <el-skeleton v-if="loading && !summary" :rows="12" animated />
    <template v-else>
      <section class="metric-grid">
        <div v-for="item in metrics" :key="item.label" class="metric-cell">
          <span>{{ item.label }}</span>
          <strong>{{ item.value }}</strong>
        </div>
      </section>

      <section class="dashboard-grid">
        <div class="panel">
          <div class="panel-head">
            <h2>商机漏斗</h2>
            <el-tag effect="plain">{{ summary?.activeOpportunityCount || 0 }}</el-tag>
          </div>
          <div class="bucket-list">
            <div v-for="item in summary?.opportunityStageFunnel || []" :key="item.code" class="bucket-row">
              <div class="bucket-label">
                <span>{{ item.label }}</span>
                <strong>{{ item.count }}</strong>
              </div>
              <div class="bucket-track">
                <span class="bucket-bar stage" :style="{ width: bucketWidth(item.count) }" />
              </div>
            </div>
          </div>
        </div>

        <div class="panel">
          <div class="panel-head">
            <h2>价值分布</h2>
            <el-tag type="success" effect="plain">{{ summary?.highValueOpportunityCount || 0 }}</el-tag>
          </div>
          <div class="value-grid">
            <div v-for="item in summary?.opportunityValueDistribution || []" :key="item.code" class="value-cell">
              <span>{{ item.label }}</span>
              <strong>{{ item.count }}</strong>
            </div>
          </div>
          <div class="risk-line">
            <el-icon><WarningFilled /></el-icon>
            <span>废标风险要求项</span>
            <strong>{{ summary?.rejectRiskRequirementCount || 0 }}</strong>
          </div>
        </div>
      </section>

      <section class="work-grid">
        <div class="panel">
          <div class="panel-head">
            <h2>业务待办</h2>
          </div>
          <DataTable :data="summary?.todos || []" :loading="loading" empty-text="暂无待办">
            <el-table-column label="类型" width="84">
              <template #default="{ row }">
                <el-tag :type="todoTypeTag(row.type)" effect="plain">{{ todoTypeLabel(row.type) }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="title" label="事项" min-width="220" show-overflow-tooltip />
            <el-table-column label="时间" width="150">
              <template #default="{ row }">
                {{ formatDateTime(row.dueAtUtc) }}
              </template>
            </el-table-column>
            <el-table-column label="操作" width="82" fixed="right">
              <template #default="{ row }">
                <el-button size="small" @click="openRoute(row.route)">查看</el-button>
              </template>
            </el-table-column>
          </DataTable>
        </div>

        <div class="panel">
          <div class="panel-head">
            <h2>截止风险</h2>
          </div>
          <DataTable :data="summary?.deadlineRisks || []" :loading="loading" empty-text="暂无截止风险">
            <el-table-column label="风险" width="86">
              <template #default="{ row }">
                <el-tag :type="deadlineRiskTag(row)" effect="plain">{{ deadlineRiskLabel(row) }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="title" label="商机" min-width="220" show-overflow-tooltip />
            <el-table-column label="截止" width="150">
              <template #default="{ row }">
                {{ formatDateTime(row.bidDeadline) }}
              </template>
            </el-table-column>
            <el-table-column label="操作" width="82" fixed="right">
              <template #default="{ row }">
                <el-button size="small" @click="openOpportunity(row.opportunityId)">查看</el-button>
              </template>
            </el-table-column>
          </DataTable>
        </div>
      </section>

      <section class="panel">
        <div class="panel-head">
          <h2>高价值商机</h2>
          <el-button size="small" @click="router.push('/bidops/opportunities?valueLevel=High')">全部</el-button>
        </div>
        <DataTable
          v-if="summary?.highValueOpportunities.length"
          :data="summary?.highValueOpportunities || []"
          :loading="loading"
          empty-text="暂无高价值商机"
        >
          <el-table-column prop="opportunityNo" label="编号" width="160" show-overflow-tooltip />
          <el-table-column prop="title" label="标题" min-width="260" show-overflow-tooltip />
          <el-table-column label="阶段" width="110">
            <template #default="{ row }">{{ formatOpportunityStage(row.stage) }}</template>
          </el-table-column>
          <el-table-column label="价值" width="110">
            <template #default="{ row }">{{ formatOpportunityValueLevel(row.valueLevel) }}</template>
          </el-table-column>
          <el-table-column label="评分" width="90">
            <template #default="{ row }">{{ row.valueScore ?? '-' }}</template>
          </el-table-column>
          <el-table-column label="金额" width="130">
            <template #default="{ row }">{{ formatMoney(row.estimatedAmount) }}</template>
          </el-table-column>
          <el-table-column label="决策" width="100">
            <template #default="{ row }">{{ formatOpportunityDecision(row.decision) }}</template>
          </el-table-column>
          <el-table-column label="操作" width="82" fixed="right">
            <template #default="{ row }">
              <el-button size="small" @click="openOpportunity(row.opportunityId)">查看</el-button>
            </template>
          </el-table-column>
        </DataTable>
        <EmptyState v-else title="暂无高价值商机" />
      </section>
    </template>
  </PageContainer>
</template>

<style scoped>
.metric-grid,
.dashboard-grid,
.work-grid {
  display: grid;
  gap: 12px;
  margin-bottom: 12px;
}

.metric-grid {
  grid-template-columns: repeat(4, minmax(150px, 1fr));
}

.dashboard-grid,
.work-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.metric-cell,
.panel {
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.metric-cell {
  display: grid;
  gap: 8px;
  min-height: 78px;
  padding: 14px;
}

.metric-cell span,
.bucket-label,
.value-cell span,
.risk-line {
  color: #667085;
  font-size: 13px;
}

.metric-cell strong {
  color: #162033;
  font-size: 24px;
  line-height: 1.15;
}

.panel {
  padding: 14px;
}

.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 12px;
}

.panel-head h2 {
  margin: 0;
  color: #17202a;
  font-size: 16px;
}

.bucket-list {
  display: grid;
  gap: 10px;
}

.bucket-row {
  display: grid;
  gap: 6px;
}

.bucket-label {
  display: flex;
  justify-content: space-between;
}

.bucket-label strong {
  color: #1f2937;
}

.bucket-track {
  height: 9px;
  overflow: hidden;
  border-radius: 999px;
  background: #edf2f7;
}

.bucket-bar {
  display: block;
  height: 100%;
  border-radius: inherit;
}

.bucket-bar.stage {
  background: #0f766e;
}

.value-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 10px;
}

.value-cell {
  display: grid;
  gap: 6px;
  padding: 10px;
  border: 1px solid #e4e9f2;
  border-radius: 8px;
  background: #f8fafc;
}

.value-cell strong {
  color: #1f2937;
  font-size: 20px;
}

.risk-line {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 12px;
}

.risk-line strong {
  margin-left: auto;
  color: #b42318;
  font-size: 18px;
}

@media (max-width: 1100px) {
  .metric-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .dashboard-grid,
  .work-grid {
    grid-template-columns: 1fr;
  }
}
</style>
