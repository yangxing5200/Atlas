<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
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
import { formatDuration, formatJobType, jobStatusOptions } from '../utils/display'

type SortOrder = 'ascending' | 'descending' | null
const JOB_QUERY_CACHE_VERSION = 1

interface JobTableQuery {
  keyword: string
  projectCode: string
  queue: string
  jobType: string
  status: BackgroundJobStatus | ''
  businessId: string
  deadOnly: boolean
  staleRunningOnly: boolean
  waitingRetryOnly: boolean
  sortBy: string
  sortDescending: boolean | null
  pageIndex: number
  pageSize: number
}

const route = useRoute()
const router = useRouter()
const bidOpsMode = computed(() => route.path.startsWith('/bidops/operations/jobs'))
const queryStorageKey = bidOpsMode.value
  ? 'atlas.bidops.background-jobs.query.v1'
  : 'atlas.ops.background-jobs.query.v1'
const pageTitle = computed(() => (bidOpsMode.value ? 'BidOps 后台任务' : '后台任务'))
const pageDescription = computed(() =>
  bidOpsMode.value
    ? '查看 BidOps 抓取、附件处理、结构化解析和恢复任务。'
    : '查看当前租户的后台任务状态、错误和脱敏 Payload。',
)

const query = reactive<JobTableQuery>({
  ...createDefaultQuery(),
  ...loadCachedQuery(),
  ...getRouteQueryOverrides(),
})
if (bidOpsMode.value) {
  query.queue = 'bidops'
}
const statusDefaultSortApplied = ref(isSucceededStatus(query.status) && query.sortBy === 'CompletedAt' && query.sortDescending !== false)
applySucceededStatusDefaultSort()
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
const paginationTotal = computed(() => Number(result.value.total || 0))
const completedAtSortOrder = computed<SortOrder>(() => {
  if (query.sortBy !== 'CompletedAt')
    return null

  return query.sortDescending === false ? 'ascending' : 'descending'
})

const metrics = computed(() => [
  { label: '待执行', value: summary.value?.pending ?? 0 },
  { label: '执行中', value: summary.value?.running ?? 0 },
  { label: '失败待重试', value: summary.value?.failed ?? 0 },
  { label: '死亡', value: summary.value?.dead ?? 0 },
  { label: '超时锁定', value: summary.value?.staleRunning ?? 0 },
])

function createDefaultQuery(): JobTableQuery {
  return {
    keyword: '',
    projectCode: '',
    queue: bidOpsMode.value ? 'bidops' : '',
    jobType: '',
    status: '',
    businessId: '',
    deadOnly: false,
    staleRunningOnly: false,
    waitingRetryOnly: false,
    sortBy: '',
    sortDescending: null,
    pageIndex: 1,
    pageSize: 20,
  }
}

function loadCachedQuery(): Partial<JobTableQuery> {
  try {
    const raw = window.localStorage.getItem(queryStorageKey)
    if (!raw)
      return {}

    const parsed = JSON.parse(raw) as { version?: number; query?: Partial<JobTableQuery> }
    if (parsed.version !== JOB_QUERY_CACHE_VERSION || !parsed.query)
      return {}

    return pickQueryFields(parsed.query)
  } catch {
    return {}
  }
}

function saveCachedQuery() {
  try {
    window.localStorage.setItem(
      queryStorageKey,
      JSON.stringify({
        version: JOB_QUERY_CACHE_VERSION,
        query: { ...query },
      }),
    )
  } catch {
    // Ignore storage quota/privacy-mode failures; the page should still work normally.
  }
}

function getRouteQueryOverrides(): Partial<JobTableQuery> {
  return pickQueryFields({
    keyword: firstRouteValue(route.query.keyword),
    projectCode: firstRouteValue(route.query.projectCode),
    queue: firstRouteValue(route.query.queue),
    jobType: firstRouteValue(route.query.jobType),
    status: firstRouteValue(route.query.status) as BackgroundJobStatus | '',
    businessId: firstRouteValue(route.query.businessId),
    deadOnly: parseBooleanRouteValue(route.query.deadOnly),
    staleRunningOnly: parseBooleanRouteValue(route.query.staleRunningOnly),
    waitingRetryOnly: parseBooleanRouteValue(route.query.waitingRetryOnly),
    sortBy: firstRouteValue(route.query.sortBy),
    sortDescending: parseNullableBooleanRouteValue(route.query.sortDescending),
    pageIndex: parseNumberRouteValue(route.query.pageIndex),
    pageSize: parseNumberRouteValue(route.query.pageSize),
  })
}

function pickQueryFields(value: Partial<JobTableQuery>): Partial<JobTableQuery> {
  const fields: Partial<JobTableQuery> = {}
  for (const key of Object.keys(createDefaultQuery()) as Array<keyof JobTableQuery>) {
    const fieldValue = value[key]
    if (fieldValue !== undefined && fieldValue !== null) {
      fields[key] = fieldValue as never
    }
  }

  return fields
}

function firstRouteValue(value: unknown) {
  if (Array.isArray(value))
    return value[0] == null ? undefined : String(value[0])

  return value == null ? undefined : String(value)
}

function parseBooleanRouteValue(value: unknown) {
  const normalized = firstRouteValue(value)
  if (normalized === undefined)
    return undefined

  return normalized === 'true'
}

function parseNullableBooleanRouteValue(value: unknown) {
  const normalized = firstRouteValue(value)
  if (normalized === undefined)
    return undefined
  if (normalized === 'true')
    return true
  if (normalized === 'false')
    return false

  return undefined
}

function parseNumberRouteValue(value: unknown) {
  const normalized = Number(firstRouteValue(value))
  return Number.isFinite(normalized) && normalized > 0 ? normalized : undefined
}

function normalizeQuery(): BackgroundJobSearchQuery {
  return {
    keyword: query.keyword.trim() || undefined,
    projectCode: query.projectCode.trim() || undefined,
    queue: query.queue.trim() || undefined,
    jobType: query.jobType.trim() || undefined,
    status: query.status || undefined,
    businessId: query.businessId.trim() || undefined,
    deadOnly: query.deadOnly || undefined,
    staleRunningOnly: query.staleRunningOnly || undefined,
    waitingRetryOnly: query.waitingRetryOnly || undefined,
    sortBy: query.sortBy || undefined,
    sortDescending: query.sortDescending,
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
  applySucceededStatusDefaultSort()
  await reload()
  saveCachedQuery()
}

async function reset() {
  Object.assign(query, {
    keyword: '',
    projectCode: '',
    queue: bidOpsMode.value ? 'bidops' : '',
    jobType: '',
    status: '',
    businessId: '',
    deadOnly: false,
    staleRunningOnly: false,
    waitingRetryOnly: false,
    sortBy: '',
    sortDescending: null,
    pageIndex: 1,
    pageSize: 20,
  })
  statusDefaultSortApplied.value = false
  await reload()
  saveCachedQuery()
}

async function handleSortChange({ prop, order }: { prop: string; order: SortOrder }) {
  statusDefaultSortApplied.value = false
  if (prop === 'completedAt' && order) {
    query.sortBy = 'CompletedAt'
    query.sortDescending = order === 'descending'
  } else {
    query.sortBy = ''
    query.sortDescending = null
  }

  query.pageIndex = 1
  await loadData()
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
  const running = isRunning(row)
  await ElMessageBox.confirm(
    running
      ? '将向 Worker 发送终止请求。任务会在当前处理器响应 CancellationToken 后结束，不会强杀进程。'
      : '将取消未完成任务，任务历史不会被删除。',
    running ? '确认终止' : '确认取消',
    {
      type: 'warning',
    },
  )
  const result = bidOpsMode.value
    ? await bidOpsOperationsApi.cancelJob(String(row.id))
    : await backgroundJobsApi.cancel(String(row.id))
  ElMessage.success(result.message)
  await reload()
}

async function forceCancelJob(row: BackgroundJobListItemDto) {
  await ElMessageBox.confirm(
    '将立即把任务标记为已取消并释放数据库锁；如果 Worker 正在等待外部 I/O，也会收到取消信号。任务历史不会被删除。',
    '确认强制终止',
    {
      type: 'error',
      confirmButtonText: '强制终止',
    },
  )
  const result = bidOpsMode.value
    ? await bidOpsOperationsApi.cancelJob(String(row.id), '强制终止', true)
    : await backgroundJobsApi.cancel(String(row.id), '强制终止', true)
  ElMessage.success(result.message)
  await reload()
}

function isRunning(row: BackgroundJobListItemDto) {
  return ['Running', '1'].includes(String(row.statusName || row.status))
}

function cancelActionText(row: BackgroundJobListItemDto) {
  if (row.isCancellationRequested) return '终止中'
  return isRunning(row) ? '终止' : '取消'
}

function canRetry(row: BackgroundJobListItemDto) {
  return !['Pending', 'Running', '0', '1'].includes(String(row.statusName || row.status))
}

function canCancel(row: BackgroundJobListItemDto) {
  if (row.isCancellationRequested) return false
  return !['Succeeded', 'Canceled', '2', '5'].includes(String(row.statusName || row.status))
}

function canForceCancel(row: BackgroundJobListItemDto) {
  return isRunning(row)
}

function jobDetailPath(id: string | number) {
  return bidOpsMode.value ? `/bidops/operations/jobs/${id}` : `/ops/jobs/${id}`
}

function isSucceededStatus(value: BackgroundJobStatus | '') {
  const normalized = String(value || '').trim()
  return normalized === 'Succeeded' || normalized === '2'
}

function applySucceededStatusDefaultSort() {
  if (!isSucceededStatus(query.status) || query.sortBy)
    return

  query.sortBy = 'CompletedAt'
  query.sortDescending = true
  statusDefaultSortApplied.value = true
}

watch(
  () => query.status,
  () => {
    if (isSucceededStatus(query.status)) {
      applySucceededStatusDefaultSort()
      return
    }

    if (statusDefaultSortApplied.value && query.sortBy === 'CompletedAt' && query.sortDescending !== false) {
      query.sortBy = ''
      query.sortDescending = null
    }
    statusDefaultSortApplied.value = false
  },
)

onMounted(async () => {
  await reload()
})
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
        <el-input v-model="query.keyword" clearable placeholder="任务类型 / 名称 / 错误" />
      </el-form-item>
      <el-form-item v-if="bidOpsMode" label="采购编号">
        <el-input v-model="query.projectCode" clearable placeholder="采购编号 / 项目编号" style="width: 210px" />
      </el-form-item>
      <el-form-item label="队列">
        <el-input v-model="query.queue" clearable :disabled="bidOpsMode" placeholder="default / bidops" />
      </el-form-item>
      <el-form-item label="类型">
        <el-input v-model="query.jobType" clearable placeholder="中文名或任务代码" />
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

    <DataTable :data="result.items" :loading="loading" @sort-change="handleSortChange">
      <el-table-column label="状态" width="130">
        <template #default="{ row }">
          <JobStatusTag
            :status="row.status"
            :status-name="row.statusName"
            :cancellation-requested="row.isCancellationRequested"
          />
        </template>
      </el-table-column>
      <el-table-column label="任务类型" min-width="260" show-overflow-tooltip>
        <template #default="{ row }">{{ formatJobType(row.jobType, row.jobTypeName) }}</template>
      </el-table-column>
      <el-table-column v-if="bidOpsMode" label="采购编号" min-width="170" show-overflow-tooltip>
        <template #default="{ row }">{{ row.projectCode || '-' }}</template>
      </el-table-column>
      <el-table-column prop="queue" label="队列" width="110" />
      <el-table-column prop="tenantId" label="租户" width="120" />
      <el-table-column label="重试" width="95">
        <template #default="{ row }">{{ row.attemptCount }} / {{ row.maxAttempts }}</template>
      </el-table-column>
      <el-table-column label="等待" width="90">
        <template #default="{ row }">{{ formatDuration(row.waitMilliseconds, row.waitSeconds) }}</template>
      </el-table-column>
      <el-table-column label="运行" width="90">
        <template #default="{ row }">{{ formatDuration(row.runMilliseconds, row.runSeconds) }}</template>
      </el-table-column>
      <el-table-column label="创建时间" width="170">
        <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column
        prop="completedAt"
        label="完成时间"
        width="170"
        sortable="custom"
        :sort-order="completedAtSortOrder"
      >
        <template #default="{ row }">{{ formatDateTime(row.completedAt) }}</template>
      </el-table-column>
      <el-table-column label="下次重试" width="170">
        <template #default="{ row }">{{ formatDateTime(row.nextAttemptAt) }}</template>
      </el-table-column>
      <el-table-column prop="lockedBy" label="锁定节点" min-width="150" show-overflow-tooltip />
      <el-table-column prop="lastErrorPreview" label="错误信息" min-width="240" show-overflow-tooltip />
      <el-table-column label="操作" width="285" fixed="right">
        <template #default="{ row }">
          <el-button size="small" :icon="View" @click="router.push(jobDetailPath(row.id))">详情</el-button>
          <el-button size="small" :icon="Refresh" :disabled="!canRetry(row)" @click="retryJob(row)">重试</el-button>
          <el-button size="small" :icon="Close" :disabled="!canCancel(row)" @click="cancelJob(row)">
            {{ cancelActionText(row) }}
          </el-button>
          <el-button
            size="small"
            :icon="Close"
            type="danger"
            plain
            :disabled="!canForceCancel(row)"
            @click="forceCancelJob(row)"
          >
            强停
          </el-button>
        </template>
      </el-table-column>
    </DataTable>

    <el-pagination
      v-model:current-page="query.pageIndex"
      v-model:page-size="query.pageSize"
      :total="paginationTotal"
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
