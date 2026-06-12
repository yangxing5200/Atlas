<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Refresh, Tickets, Warning } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'
import { bidOpsOperationsApi } from '@/api/bidops/operations.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import { formatDateTime } from '@/shared/utils/date'
import JobStatusTag from '@/modules/operations/components/JobStatusTag.vue'
import type { BidOpsOperationsDashboardDto } from '@/modules/operations/types'
import { severityType } from '@/modules/operations/utils/display'

const router = useRouter()
const loading = ref(false)
const dashboard = ref<BidOpsOperationsDashboardDto | null>(null)

const metrics = computed(() => {
  const data = dashboard.value
  return [
    { label: 'Pending', value: data?.jobs.pending ?? 0 },
    { label: 'Running', value: data?.jobs.running ?? 0 },
    { label: 'Failed', value: data?.jobs.failed ?? 0 },
    { label: 'Dead', value: data?.jobs.dead ?? 0 },
    { label: '今日 RawNotice', value: data?.rawNoticeCreatedToday ?? 0 },
    { label: '今日审核任务', value: data?.reviewTaskCreatedToday ?? 0 },
    { label: '待解析公告', value: data?.parseQueuedRawNotices ?? 0 },
    { label: '失败附件', value: data?.failedAttachments ?? 0 },
  ]
})

async function loadData() {
  loading.value = true
  try {
    dashboard.value = await bidOpsOperationsApi.dashboard()
  } finally {
    loading.value = false
  }
}

onMounted(loadData)
</script>

<template>
  <PageContainer title="BidOps 运营看板" description="查看 BidOps 后台任务、配置告警和今日处理概况。">
    <template #actions>
      <el-button :icon="Tickets" @click="router.push('/bidops/operations/jobs')">任务监控</el-button>
      <el-button :icon="Warning" @click="router.push('/bidops/operations/channels')">采集健康</el-button>
      <el-button :icon="Refresh" :loading="loading" @click="loadData">刷新</el-button>
    </template>

    <el-skeleton v-if="loading && !dashboard" :rows="10" animated />
    <template v-else>
      <section class="warning-panel">
        <el-alert
          v-if="dashboard?.configWarnings.length === 0"
          title="后台配置检查通过"
          type="success"
          show-icon
          :closable="false"
        />
        <el-alert
          v-for="item in dashboard?.configWarnings || []"
          :key="item.code"
          :title="item.title"
          :description="item.message"
          :type="severityType(item.severity)"
          show-icon
          :closable="false"
        />
      </section>

      <section class="runtime-grid">
        <div class="runtime-cell">
          <span>OneTime Worker</span>
          <strong>{{ dashboard?.backgroundJobWorkerEnabled ? '启用' : '未启用' }}</strong>
        </div>
        <div class="runtime-cell">
          <span>Recurring Runner</span>
          <strong>{{ dashboard?.recurringTaskRunnerEnabled ? '启用' : '未启用' }}</strong>
        </div>
        <div class="runtime-cell">
          <span>bidops 队列</span>
          <strong>{{ dashboard?.bidOpsQueueConfigured ? '已配置' : '缺失' }}</strong>
        </div>
      </section>

      <section class="metric-grid">
        <div v-for="item in metrics" :key="item.label" class="metric-cell">
          <span>{{ item.label }}</span>
          <strong>{{ item.value }}</strong>
        </div>
      </section>

      <section class="content-panel">
        <h2>最近失败任务</h2>
        <DataTable :data="dashboard?.recentFailedJobs || []" :loading="loading" empty-text="暂无失败任务">
          <el-table-column label="状态" width="130">
            <template #default="{ row }">
              <JobStatusTag :status="row.status" :status-name="row.statusName" />
            </template>
          </el-table-column>
          <el-table-column prop="jobType" label="任务类型" min-width="260" show-overflow-tooltip />
          <el-table-column prop="queue" label="队列" width="100" />
          <el-table-column label="创建时间" width="170">
            <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
          </el-table-column>
          <el-table-column prop="lastErrorPreview" label="错误信息" min-width="260" show-overflow-tooltip />
          <el-table-column label="操作" width="90" fixed="right">
            <template #default="{ row }">
              <el-button size="small" @click="router.push(`/ops/jobs/${row.id}`)">详情</el-button>
            </template>
          </el-table-column>
        </DataTable>
      </section>
    </template>
  </PageContainer>
</template>

<style scoped>
.warning-panel {
  display: grid;
  gap: 10px;
  margin-bottom: 14px;
}

.runtime-grid,
.metric-grid {
  display: grid;
  gap: 10px;
  margin-bottom: 14px;
}

.runtime-grid {
  grid-template-columns: repeat(3, minmax(160px, 1fr));
}

.metric-grid {
  grid-template-columns: repeat(4, minmax(130px, 1fr));
}

.runtime-cell,
.metric-cell,
.content-panel {
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.runtime-cell,
.metric-cell {
  display: grid;
  gap: 6px;
  min-height: 72px;
  padding: 12px;
}

.runtime-cell span,
.metric-cell span {
  color: #687385;
  font-size: 13px;
}

.runtime-cell strong,
.metric-cell strong {
  color: #17202a;
  font-size: 22px;
  line-height: 1.2;
}

.content-panel {
  padding: 14px;
}

.content-panel h2 {
  margin: 0 0 12px;
  color: #17202a;
  font-size: 16px;
}

@media (max-width: 980px) {
  .runtime-grid,
  .metric-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
