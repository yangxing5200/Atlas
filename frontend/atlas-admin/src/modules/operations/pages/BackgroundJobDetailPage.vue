<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowLeft, Close, Refresh } from '@element-plus/icons-vue'
import { useRoute, useRouter } from 'vue-router'
import { bidOpsOperationsApi } from '@/api/bidops/operations.api'
import { backgroundJobsApi } from '@/api/operations/backgroundJobs.api'
import BidOpsJobParsedResultPanel from '@/modules/bidops/components/BidOpsJobParsedResultPanel.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import { formatDateTime } from '@/shared/utils/date'
import JobStatusTag from '../components/JobStatusTag.vue'
import type { BackgroundJobDetailDto } from '../types'
import { formatDuration, formatJobType } from '../utils/display'

interface AiResponseEntry {
  use: string
  provider: string
  model: string
  endpoint: string
  statusCode?: number | null
  elapsedMilliseconds?: number | null
  responseCharacters?: number | null
  assistantCharacters?: number | null
  finishReason: string
  rawResponseBody: string
  assistantContent: string
}

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const job = ref<BackgroundJobDetailDto | null>(null)
const jobId = computed(() => String(route.params.id || ''))
const bidOpsMode = computed(() => route.path.startsWith('/bidops/operations/jobs'))
const isBidOpsJob = computed(() => {
  const value = job.value
  return Boolean(value && (value.queue === 'bidops' || value.jobType?.startsWith('bidops.')))
})
const pageTitle = computed(() => (bidOpsMode.value ? 'BidOps 后台任务详情' : '后台任务详情'))
const formattedPayload = computed(() => formatJsonText(job.value?.payload))
const formattedResult = computed(() => formatJsonText(job.value?.result))
const parsedResult = computed(() => parseJsonObject(job.value?.result))
const aiResponses = computed(() => {
  const value = parsedResult.value?.aiResponses ?? parsedResult.value?.deepSeekResponses
  if (!Array.isArray(value)) return []

  return value
    .map((item) => normalizeAiResponse(item))
    .filter((item): item is AiResponseEntry => Boolean(item))
})
const hasAiResponses = computed(() => aiResponses.value.length > 0)
const isRunning = computed(() =>
  Boolean(job.value && ['Running', '1'].includes(String(job.value.statusName || job.value.status))),
)
const canCancelJob = computed(() => {
  if (!job.value || job.value.isCancellationRequested) return false
  return !['Succeeded', 'Canceled', '2', '5'].includes(String(job.value.statusName || job.value.status))
})
const cancelActionText = computed(() => {
  if (job.value?.isCancellationRequested) return '终止中'
  return isRunning.value ? '终止' : '取消'
})

async function loadData() {
  loading.value = true
  try {
    job.value = await backgroundJobsApi.get(jobId.value)
  } catch {
    job.value = null
  } finally {
    loading.value = false
  }
}

async function retryJob() {
  if (!job.value) return
  await ElMessageBox.confirm('将创建新的后台任务，原任务历史不会被覆盖。', '确认重试', { type: 'warning' })
  const result = bidOpsMode.value
    ? await bidOpsOperationsApi.retryJob(String(job.value.id))
    : await backgroundJobsApi.retry(String(job.value.id))
  ElMessage.success(`已创建重试任务：${result.newJobId}`)
  await loadData()
}

async function cancelJob() {
  if (!job.value) return
  await ElMessageBox.confirm(
    isRunning.value
      ? '将向 Worker 发送终止请求。任务会在当前处理器响应 CancellationToken 后结束，不会强杀进程。'
      : '将取消未完成任务，任务历史不会被删除。',
    isRunning.value ? '确认终止' : '确认取消',
    {
      type: 'warning',
    },
  )
  const result = bidOpsMode.value
    ? await bidOpsOperationsApi.cancelJob(String(job.value.id))
    : await backgroundJobsApi.cancel(String(job.value.id))
  ElMessage.success(result.message)
  await loadData()
}

async function forceCancelJob() {
  if (!job.value) return
  await ElMessageBox.confirm(
    '将立即把任务标记为已取消并释放数据库锁；如果 Worker 正在等待外部 I/O，也会收到取消信号。任务历史不会被删除。',
    '确认强制终止',
    {
      type: 'error',
      confirmButtonText: '强制终止',
    },
  )
  const result = bidOpsMode.value
    ? await bidOpsOperationsApi.cancelJob(String(job.value.id), '强制终止', true)
    : await backgroundJobsApi.cancel(String(job.value.id), '强制终止', true)
  ElMessage.success(result.message)
  await loadData()
}

function formatJsonText(value?: string | null) {
  if (!value?.trim()) return '-'

  try {
    return JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    return decodeUnicodeEscapes(value)
  }
}

function formatAiResponseText(value?: string | null) {
  if (!value?.trim()) return '-'

  return formatJsonText(value)
}

function parseJsonObject(value?: string | null): Record<string, unknown> | null {
  if (!value?.trim()) return null

  try {
    const parsed = JSON.parse(value)
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed)
      ? (parsed as Record<string, unknown>)
      : null
  } catch {
    return null
  }
}

function normalizeAiResponse(value: unknown): AiResponseEntry | null {
  if (!value || typeof value !== 'object') return null

  const source = value as Record<string, unknown>
  const rawResponseBody = getString(source, 'rawResponseBody')
  const assistantContent = getString(source, 'assistantContent')
  if (!rawResponseBody && !assistantContent) return null

  return {
    use: getString(source, 'use'),
    provider: getString(source, 'provider'),
    model: getString(source, 'model'),
    endpoint: getString(source, 'endpoint'),
    statusCode: getNumber(source, 'statusCode'),
    elapsedMilliseconds: getNumber(source, 'elapsedMilliseconds'),
    responseCharacters: getNumber(source, 'responseCharacters'),
    assistantCharacters: getNumber(source, 'assistantCharacters'),
    finishReason: getString(source, 'finishReason'),
    rawResponseBody,
    assistantContent,
  }
}

function getString(source: Record<string, unknown>, key: string) {
  const value = source[key]
  if (value === null || value === undefined) return ''
  return typeof value === 'string' ? value : String(value)
}

function getNumber(source: Record<string, unknown>, key: string) {
  const value = source[key]
  if (typeof value === 'number') return Number.isFinite(value) ? value : null
  if (typeof value === 'string' && value.trim()) {
    const parsed = Number(value)
    return Number.isFinite(parsed) ? parsed : null
  }
  return null
}

function decodeUnicodeEscapes(value: string) {
  return value.replace(/\\u([0-9a-fA-F]{4})/g, (_, hex: string) =>
    String.fromCharCode(Number.parseInt(hex, 16)),
  )
}

onMounted(loadData)
</script>

<template>
  <PageContainer :title="pageTitle" :description="`JobId ${jobId}`">
    <template #actions>
      <el-button :icon="ArrowLeft" @click="router.back()">返回</el-button>
      <el-button :icon="Refresh" :loading="loading" @click="loadData">刷新</el-button>
      <el-button :icon="Refresh" :disabled="!job" @click="retryJob">重试</el-button>
      <el-button :icon="Close" :disabled="!canCancelJob" @click="cancelJob">{{ cancelActionText }}</el-button>
      <el-button :icon="Close" type="danger" plain :disabled="!isRunning" @click="forceCancelJob">强停</el-button>
    </template>

    <el-skeleton v-if="loading" :rows="10" animated />
    <el-empty v-else-if="!job" description="未找到任务" />
    <template v-else>
      <section class="summary-band">
        <div>
          <span>状态</span>
          <JobStatusTag
            :status="job.status"
            :status-name="job.statusName"
            :cancellation-requested="job.isCancellationRequested"
          />
        </div>
        <div>
          <span>队列</span>
          <strong>{{ job.queue }}</strong>
        </div>
        <div v-if="isBidOpsJob">
          <span>采购编号</span>
          <strong>{{ job.projectCode || '-' }}</strong>
        </div>
        <div>
          <span>重试</span>
          <strong>{{ job.attemptCount }} / {{ job.maxAttempts }}</strong>
        </div>
        <div>
          <span>运行时长</span>
          <strong>{{ formatDuration(job.runMilliseconds, job.runSeconds) }}</strong>
        </div>
      </section>

      <el-tabs class="detail-tabs">
        <el-tab-pane label="基本信息">
          <el-descriptions :column="2" border>
            <el-descriptions-item label="JobId">{{ job.id }}</el-descriptions-item>
            <el-descriptions-item label="任务类型">{{ formatJobType(job.jobType, job.jobTypeName) }}</el-descriptions-item>
            <el-descriptions-item label="任务代码">{{ job.jobType }}</el-descriptions-item>
            <el-descriptions-item label="任务名称">{{ job.jobName || '-' }}</el-descriptions-item>
            <el-descriptions-item v-if="isBidOpsJob" label="采购编号">{{ job.projectCode || '-' }}</el-descriptions-item>
            <el-descriptions-item label="幂等键">{{ job.deduplicationKey || '-' }}</el-descriptions-item>
            <el-descriptions-item label="TenantId">{{ job.tenantId || '-' }}</el-descriptions-item>
            <el-descriptions-item label="StoreId">{{ job.storeId || '-' }}</el-descriptions-item>
            <el-descriptions-item label="优先级">{{ job.priority }}</el-descriptions-item>
            <el-descriptions-item label="超时锁定">{{ job.isStaleRunning ? '是' : '否' }}</el-descriptions-item>
            <el-descriptions-item label="创建时间">{{ formatDateTime(job.createdAt) }}</el-descriptions-item>
            <el-descriptions-item label="可执行时间">{{ formatDateTime(job.availableAt) }}</el-descriptions-item>
            <el-descriptions-item label="开始时间">{{ formatDateTime(job.startedAt) }}</el-descriptions-item>
            <el-descriptions-item label="完成时间">{{ formatDateTime(job.completedAt) }}</el-descriptions-item>
            <el-descriptions-item label="锁定时间">{{ formatDateTime(job.lockedAt) }}</el-descriptions-item>
            <el-descriptions-item label="锁定节点">{{ job.lockedBy || '-' }}</el-descriptions-item>
            <el-descriptions-item label="下次重试">{{ formatDateTime(job.nextAttemptAt) }}</el-descriptions-item>
            <el-descriptions-item label="终止请求时间">
              {{ formatDateTime(job.cancellationRequestedAt) }}
            </el-descriptions-item>
            <el-descriptions-item label="终止请求人">{{ job.cancellationRequestedBy || '-' }}</el-descriptions-item>
            <el-descriptions-item label="终止原因">{{ job.cancellationReason || '-' }}</el-descriptions-item>
          </el-descriptions>
        </el-tab-pane>

        <el-tab-pane label="Payload">
          <pre class="code-panel">{{ formattedPayload }}</pre>
        </el-tab-pane>

        <el-tab-pane label="结果">
          <pre class="code-panel">{{ formattedResult }}</pre>
        </el-tab-pane>

        <el-tab-pane v-if="hasAiResponses" label="AI 返回">
          <div class="deepseek-list">
            <section
              v-for="(item, index) in aiResponses"
              :key="`${item.use}-${index}`"
              class="deepseek-response"
            >
              <header class="deepseek-response__header">
                <div>
                  <strong>调用 {{ index + 1 }}</strong>
                  <span>{{ item.use || 'AI' }}</span>
                </div>
                <div class="deepseek-response__meta">
                  <el-tag size="small">{{ item.provider || 'AI' }}</el-tag>
                  <el-tag size="small" type="info">{{ item.model || '-' }}</el-tag>
                  <el-tag size="small" :type="item.statusCode && item.statusCode >= 400 ? 'danger' : 'success'">
                    HTTP {{ item.statusCode ?? '-' }}
                  </el-tag>
                </div>
              </header>
              <el-descriptions :column="2" border size="small">
                <el-descriptions-item label="Endpoint">{{ item.endpoint || '-' }}</el-descriptions-item>
                <el-descriptions-item label="Finish Reason">{{ item.finishReason || '-' }}</el-descriptions-item>
                <el-descriptions-item label="耗时">{{ item.elapsedMilliseconds ?? '-' }} ms</el-descriptions-item>
                <el-descriptions-item label="响应字符">
                  {{ item.responseCharacters ?? '-' }} / {{ item.assistantCharacters ?? '-' }}
                </el-descriptions-item>
              </el-descriptions>

              <section class="deepseek-response__section">
                <h3>Assistant Content</h3>
                <pre class="code-panel">{{ formatAiResponseText(item.assistantContent) }}</pre>
              </section>

              <section class="deepseek-response__section">
                <h3>Raw Response Body</h3>
                <pre class="code-panel">{{ formatAiResponseText(item.rawResponseBody) }}</pre>
              </section>
            </section>
          </div>
        </el-tab-pane>

        <el-tab-pane v-if="isBidOpsJob" label="解析结果">
          <BidOpsJobParsedResultPanel :job="job" />
        </el-tab-pane>

        <el-tab-pane label="错误">
          <pre class="code-panel error-panel">{{ job.lastError || '-' }}</pre>
        </el-tab-pane>
      </el-tabs>
    </template>
  </PageContainer>
</template>

<style scoped>
.summary-band {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: 10px;
  margin-bottom: 16px;
}

.summary-band > div {
  display: grid;
  gap: 6px;
  min-height: 70px;
  padding: 12px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.summary-band span {
  color: #687385;
  font-size: 13px;
}

.summary-band strong {
  color: #17202a;
  font-size: 17px;
}

.detail-tabs {
  padding: 14px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.code-panel {
  min-height: 260px;
  max-height: 560px;
  overflow: auto;
  padding: 14px;
  border: 1px solid #dce3ee;
  border-radius: 6px;
  background: #0f172a;
  color: #dbeafe;
  font-size: 12px;
  line-height: 1.55;
  white-space: pre-wrap;
  word-break: break-word;
}

.error-panel {
  color: #fecaca;
}

.deepseek-list {
  display: grid;
  gap: 16px;
}

.deepseek-response {
  display: grid;
  gap: 12px;
}

.deepseek-response + .deepseek-response {
  padding-top: 16px;
  border-top: 1px solid #dce3ee;
}

.deepseek-response__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.deepseek-response__header > div:first-child {
  display: flex;
  align-items: center;
  gap: 8px;
  color: #17202a;
}

.deepseek-response__header span {
  color: #687385;
  font-size: 13px;
}

.deepseek-response__meta {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 6px;
}

.deepseek-response__section {
  display: grid;
  gap: 8px;
}

.deepseek-response__section h3 {
  margin: 0;
  color: #17202a;
  font-size: 14px;
}

@media (max-width: 900px) {
  .summary-band {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .deepseek-response__header {
    align-items: flex-start;
    flex-direction: column;
  }

  .deepseek-response__meta {
    justify-content: flex-start;
  }
}
</style>
