<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Close, Refresh, Search, View } from '@element-plus/icons-vue'
import { useRoute, useRouter } from 'vue-router'
import { backgroundJobsApi } from '@/api/operations/backgroundJobs.api'
import { bidOpsOperationsApi } from '@/api/bidops/operations.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { formatDateTime } from '@/shared/utils/date'
import JobStatusTag from '../components/JobStatusTag.vue'
import type {
  BackgroundJobListItemDto,
  BackgroundJobPagedResult,
  BackgroundJobSearchQuery,
  BackgroundJobStatus,
  BackgroundJobSummaryDto,
} from '../types'
import { formatSeconds, jobStatusOptions } from '../utils/display'

interface JobTableQuery {
  keyword: string
  queue: string
  jobType: string
  status: BackgroundJobStatus | ''
  businessId: string
  deadOnly: boolean
  staleRunningOnly: boolean
  waitingRetryOnly: boolean
  pageIndex: number
  pageSize: number
}

const route = useRoute()
const router = useRouter()
const bidOpsMode = computed(() => route.path.startsWith('/bidops/operations/jobs'))
const pageTitle = computed(() => (bidOpsMode.value ? 'BidOps 后台任务' : '后台任务'))
const pageDescription = computed(() =>
  bidOpsMode.value
    ? '查看 BidOps 抓取、附件处理、结构化解析和恢复任务。'
    : '查看当前租户的后台任务状态、错误和脱敏 Payload。',
)

const query = reactive<JobTableQuery>({
  keyword: String(route.query.keyword || ''),
  queue: bidOpsMode.value ? 'bidops' : String(route.query.queue || ''),
  jobType: String(route.query.jobType || ''),
  status: String(route.query.status || '') as BackgroundJobStatus | '',
  businessId: String(route.query.businessId || ''),
  deadOnly: false,
  staleRunningOnly: false,
  waitingRetryOnly: false,
  pageIndex: 1,
  pageSize: 20,
})
const result = ref<BackgroundJobPagedResult>({
  total: 0,
  items: [],
  pageIndex: 1,
  pageSize: 20,
  totalPages: 0,
  hasPrevious: false,
  hasNext: false,
})
const summary = ref<BackgroundJobSummaryDto | null>(null)
const loading = ref(false)
const summaryLoading = ref(false)

const metrics = computed(() => [
  { label: '待执行', value: summary.value?.pending ?? 0 },
  { label: '执行中', value: summary.value?.running ?? 0 },
  { label: '失败待重试', value: summary.value?.failed ?? 0 },
  { label: '死亡', value: summary.value?.dead ?? 0 },
  { label: '超时锁定', value: summary.value?.staleRunning ?? 0 },
])

function normalizeQuery(): BackgroundJobSearchQuery {
  return {
    keyword: query.keyword.trim() || undefined,
    queue: query.queue.trim() || undefined,
    jobType: query.jobType.trim() || undefined,
    status: query.status || undefined,
    businessId: query.businessId.trim() || undefined,
    deadOnly: query.deadOnly || undefined,
    staleRunningOnly: query.staleRunningOnly || undefined,
    waitingRetryOnly: query.waitingRetryOnly || undefined,
    pageIndex: query.pageIndex,
    pageSize: query.pageSize,
  }
}

async function loadSummary() {
  summaryLoading.value = true
  try {
    summary.value = bidOpsMode.value
      ? (await bidOpsOperationsApi.dashboard()).jobs
      : await backgroundJobsApi.summary(normalizeQuery())
  } finally {
    summaryLoading.value = false
  }
}

async function loadData() {
  loading.value = true
  try {
    result.value = bidOpsMode.value
      ? await bidOpsOperationsApi.jobs(normalizeQuery())
      : await backgroundJobsApi.search(normalizeQuery())
  } finally {
    loading.value = false
  }
}

async function reload() {
  await Promise.all([loadData(), loadSummary()])
}

async function search() {
  query.pageIndex = 1
  await reload()
}

async function reset() {
  Object.assign(query, {
    keyword: '',
    queue: bidOpsMode.value ? 'bidops' : '',
    jobType: '',
    status: '',
    businessId: '',
    deadOnly: false,
    staleRunningOnly: false,
    waitingRetryOnly: false,
    pageIndex: 1,
    pageSize: 20,
  })
  await reload()
}

async function retryJob(row: BackgroundJobListItemDto) {
  await ElMessageBox.confirm('将创建一个新的后台任务，原任务历史不会被覆盖。', '确认重试', {
    type: 'warning',
  })
  const result = bidOpsMode.value
    ? await bidOpsOperationsApi.retryJob(String(row.id))
    : await backgroundJobsApi.retry(String(row.id))
  ElMessage.success(`已创建重试任务：${result.newJobId}`)
  await reload()
}

async function cancelJob(row: BackgroundJobListItemDto) {
  await ElMessageBox.confirm('仅 Pending / Failed / Dead 任务会被取消，Running 任务不会被强制终止。', '确认取消', {
    type: 'warning',
  })
  const result = bidOpsMode.value
    ? await bidOpsOperationsApi.cancelJob(String(row.id))
    : await backgroundJobsApi.cancel(String(row.id))
  ElMessage.success(result.message)
  await reload()
}

function canRetry(row: BackgroundJobListItemDto) {
  return !['Pending', 'Running', '0', '1'].includes(String(row.statusName || row.status))
}

function canCancel(row: BackgroundJobListItemDto) {
  return !['Running', 'Succeeded', 'Canceled', '1', '2', '5'].includes(String(row.statusName || row.status))
}

onMounted(reload)
</script>

<template>
  <PageContainer :title="pageTitle" :description="pageDescription">
    <template #actions>
      <el-button :icon="Refresh" :loading="loading || summaryLoading" @click="reload">刷新</el-button>
    </template>

    <section class="metric-strip">
      <div v-for="item in metrics" :key="item.label" class="metric-cell">
        <span>{{ item.label }}</span>
        <strong>{{ item.value }}</strong>
      </div>
    </section>

    <SearchForm @search="search" @reset="reset">
      <el-form-item label="关键词">
        <el-input v-model="query.keyword" clearable placeholder="JobType / 名称 / 错误" />
      </el-form-item>
      <el-form-item label="队列">
        <el-input v-model="query.queue" clearable :disabled="bidOpsMode" placeholder="default / bidops" />
      </el-form-item>
      <el-form-item label="类型">
        <el-input v-model="query.jobType" clearable placeholder="bidops.ai.structured-parse" />
      </el-form-item>
      <el-form-item label="状态">
        <el-select v-model="query.status" clearable placeholder="全部" style="width: 160px">
          <el-option v-for="item in jobStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="业务 ID">
        <el-input v-model="query.businessId" clearable placeholder="ChannelId / RawNoticeId" />
      </el-form-item>
      <el-form-item label="筛选">
        <el-checkbox v-model="query.deadOnly">只看死亡</el-checkbox>
        <el-checkbox v-model="query.staleRunningOnly">超时锁定</el-checkbox>
        <el-checkbox v-model="query.waitingRetryOnly">等待重试</el-checkbox>
      </el-form-item>
      <el-button type="primary" :icon="Search" @click="search">查询</el-button>
    </SearchForm>

    <DataTable :data="result.items" :loading="loading">
      <el-table-column label="状态" width="130">
        <template #default="{ row }">
          <JobStatusTag :status="row.status" :status-name="row.statusName" />
        </template>
      </el-table-column>
      <el-table-column prop="jobType" label="任务类型" min-width="260" show-overflow-tooltip />
      <el-table-column prop="queue" label="队列" width="110" />
      <el-table-column prop="tenantId" label="租户" width="120" />
      <el-table-column label="重试" width="95">
        <template #default="{ row }">{{ row.attemptCount }} / {{ row.maxAttempts }}</template>
      </el-table-column>
      <el-table-column label="等待" width="90">
        <template #default="{ row }">{{ formatSeconds(row.waitSeconds) }}</template>
      </el-table-column>
      <el-table-column label="运行" width="90">
        <template #default="{ row }">{{ formatSeconds(row.runSeconds) }}</template>
      </el-table-column>
      <el-table-column label="创建时间" width="170">
        <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="下次重试" width="170">
        <template #default="{ row }">{{ formatDateTime(row.nextAttemptAtUtc) }}</template>
      </el-table-column>
      <el-table-column prop="lockedBy" label="锁定节点" min-width="150" show-overflow-tooltip />
      <el-table-column prop="lastErrorPreview" label="错误信息" min-width="240" show-overflow-tooltip />
      <el-table-column label="操作" width="230" fixed="right">
        <template #default="{ row }">
          <el-button size="small" :icon="View" @click="router.push(`/ops/jobs/${row.id}`)">详情</el-button>
          <el-button size="small" :icon="Refresh" :disabled="!canRetry(row)" @click="retryJob(row)">重试</el-button>
          <el-button size="small" :icon="Close" :disabled="!canCancel(row)" @click="cancelJob(row)">取消</el-button>
        </template>
      </el-table-column>
    </DataTable>

    <el-pagination
      v-model:current-page="query.pageIndex"
      v-model:page-size="query.pageSize"
      :total="result.total"
      :page-sizes="[10, 20, 50, 100, 200]"
      layout="total, sizes, prev, pager, next, jumper"
      class="table-pagination"
      @current-change="loadData"
      @size-change="loadData"
    />
  </PageContainer>
</template>

<style scoped>
.metric-strip {
  display: grid;
  grid-template-columns: repeat(5, minmax(120px, 1fr));
  gap: 10px;
  margin-bottom: 14px;
}

.metric-cell {
  display: grid;
  gap: 6px;
  min-height: 72px;
  padding: 12px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.metric-cell span {
  color: #687385;
  font-size: 13px;
}

.metric-cell strong {
  color: #17202a;
  font-size: 24px;
  line-height: 1.2;
}

.table-pagination {
  justify-content: flex-end;
  margin-top: 14px;
}

@media (max-width: 980px) {
  .metric-strip {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
