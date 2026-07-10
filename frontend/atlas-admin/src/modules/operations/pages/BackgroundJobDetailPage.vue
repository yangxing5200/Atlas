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
  requestSummaryJson: string
  requestBodyJson: string
  requestPrompt: string
  summary: AiResponseSummary
}

interface AiResponseSummary {
  parseable: boolean
  records: number | null
  packages: number | null
  requirements: number | null
  supplierNameFilled: number | null
  lotNoFilled: number | null
  lotNameFilled: number | null
  packageNoFilled: number | null
  evidenceTextFilled: number | null
  sourceRowTextFilled: number | null
  fieldEvidenceFilled: number | null
  warningRows: number | null
}

interface OutcomeDiagnosticsSummary {
  present: boolean
  dryRun: boolean | null
  isOutcomeNotice: boolean | null
  existingCount: number | null
  previewExtractedCount: number | null
  previewSavedCount: number | null
  extractedCount: number | null
  savedCount: number | null
  candidateCount: number | null
  mergeGroupCount: number | null
  mergedCandidateCount: number | null
  deltaCount: number | null
  sourceCounts: Record<string, number>
  lotNoValidationCounts: Record<string, number>
  strengthCounts: Record<string, number>
}

interface CountMapEntry {
  key: string
  label: string
  value: number
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
const outcomeDiagnostics = computed(() => summarizeOutcomeDiagnostics(parsedResult.value))
const hasOutcomeDiagnostics = computed(() => outcomeDiagnostics.value.present)
const outcomeSourceEntries = computed(() => countMapEntries(outcomeDiagnostics.value.sourceCounts))
const outcomeLotNoValidationEntries = computed(() =>
  countMapEntries(outcomeDiagnostics.value.lotNoValidationCounts),
)
const outcomeStrengthEntries = computed(() => countMapEntries(outcomeDiagnostics.value.strengthCounts))
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

function summarizeOutcomeDiagnostics(root: Record<string, unknown> | null): OutcomeDiagnosticsSummary {
  const empty = createEmptyOutcomeDiagnostics()
  if (!root) return empty

  const source = getObject(root, 'outcomeSupplierExtraction') ?? root
  const sourceCounts = getNumberRecord(source, 'sourceCounts')
  const lotNoValidationCounts = getNumberRecord(source, 'lotNoValidationCounts')
  const strengthCounts = getNumberRecord(source, 'strengthCounts')
  const hasCounts =
    Object.keys(sourceCounts).length > 0 ||
    Object.keys(lotNoValidationCounts).length > 0 ||
    Object.keys(strengthCounts).length > 0
  const hasSummaryFields = [
    'dryRun',
    'isOutcomeNotice',
    'existingCount',
    'previewExtractedCount',
    'previewSavedCount',
    'extractedCount',
    'savedCount',
    'candidateCount',
    'mergeGroupCount',
    'mergedCandidateCount',
    'deltaCount',
  ].some((key) => getValue(source, key) !== undefined && getValue(source, key) !== null)

  if (!hasCounts && !hasSummaryFields) return empty

  return {
    present: true,
    dryRun: getBoolean(source, 'dryRun'),
    isOutcomeNotice: getBoolean(source, 'isOutcomeNotice'),
    existingCount: getNumber(source, 'existingCount'),
    previewExtractedCount: getNumber(source, 'previewExtractedCount'),
    previewSavedCount: getNumber(source, 'previewSavedCount'),
    extractedCount: getNumber(source, 'extractedCount'),
    savedCount: getNumber(source, 'savedCount'),
    candidateCount: getNumber(source, 'candidateCount'),
    mergeGroupCount: getNumber(source, 'mergeGroupCount'),
    mergedCandidateCount: getNumber(source, 'mergedCandidateCount'),
    deltaCount: getNumber(source, 'deltaCount'),
    sourceCounts,
    lotNoValidationCounts,
    strengthCounts,
  }
}

function createEmptyOutcomeDiagnostics(): OutcomeDiagnosticsSummary {
  return {
    present: false,
    dryRun: null,
    isOutcomeNotice: null,
    existingCount: null,
    previewExtractedCount: null,
    previewSavedCount: null,
    extractedCount: null,
    savedCount: null,
    candidateCount: null,
    mergeGroupCount: null,
    mergedCandidateCount: null,
    deltaCount: null,
    sourceCounts: {},
    lotNoValidationCounts: {},
    strengthCounts: {},
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
    requestSummaryJson: getString(source, 'requestSummaryJson'),
    requestBodyJson: getString(source, 'requestBodyJson'),
    requestPrompt: getString(source, 'requestPrompt'),
    summary: summarizeAiResponse(assistantContent),
  }
}

function summarizeAiResponse(assistantContent: string): AiResponseSummary {
  const empty: AiResponseSummary = {
    parseable: false,
    records: null,
    packages: null,
    requirements: null,
    supplierNameFilled: null,
    lotNoFilled: null,
    lotNameFilled: null,
    packageNoFilled: null,
    evidenceTextFilled: null,
    sourceRowTextFilled: null,
    fieldEvidenceFilled: null,
    warningRows: null,
  }
  const parsed = parseJsonObject(assistantContent)
  if (!parsed) return empty

  const records = getArray(parsed, 'records')
  return {
    parseable: true,
    records: records?.length ?? null,
    packages: getArray(parsed, 'packages')?.length ?? null,
    requirements: getArray(parsed, 'requirements')?.length ?? null,
    supplierNameFilled: records ? countFilled(records, 'supplierName') : null,
    lotNoFilled: records ? countFilled(records, 'lotNo') : null,
    lotNameFilled: records ? countFilled(records, 'lotName') : null,
    packageNoFilled: records ? countFilled(records, 'packageNo') : null,
    evidenceTextFilled: records ? countFilled(records, 'evidenceText') : null,
    sourceRowTextFilled: records ? countFilled(records, 'sourceRowText') : null,
    fieldEvidenceFilled: records ? countFilledFieldEvidence(records) : null,
    warningRows: records ? countWarningRows(records) : null,
  }
}

function getArray(source: Record<string, unknown>, key: string) {
  const value = getValue(source, key)
  return Array.isArray(value) ? value : null
}

function countFilled(rows: unknown[], key: string) {
  return rows.filter((row) => {
    if (!row || typeof row !== 'object') return false
    const value = (row as Record<string, unknown>)[key]
    return value !== null && value !== undefined && String(value).trim().length > 0
  }).length
}

function countFilledFieldEvidence(rows: unknown[]) {
  return rows.filter((row) => {
    if (!row || typeof row !== 'object') return false
    const value = (row as Record<string, unknown>).fieldEvidence
    if (!value || typeof value !== 'object' || Array.isArray(value)) return false
    return Object.values(value).some((item) => item !== null && item !== undefined && String(item).trim().length > 0)
  }).length
}

function countWarningRows(rows: unknown[]) {
  return rows.filter((row) => {
    if (!row || typeof row !== 'object') return false
    const value = (row as Record<string, unknown>).warnings
    return Array.isArray(value) && value.length > 0
  }).length
}

function formatAiUseName(use?: string | null) {
  const labels: Record<string, string> = {
    NoticeStaging: '公告结构解析',
    OutcomeSuppliers: '中标成交明细解析',
    LifecycleFieldEnrichment: '闭环字段补全',
  }
  const key = String(use || '')
  return labels[key] || key || 'AI 调用'
}

function aiResponseTitle(item: AiResponseEntry, index: number) {
  return `调用 ${index + 1}：${formatAiUseName(item.use)}`
}

function formatFilledRatio(filled: number | null, total: number | null) {
  if (filled === null || total === null) return '-'
  if (total === 0) return '0 / 0'
  return `${filled} / ${total}`
}

function formatNullableNumber(value: number | null) {
  return value === null ? '-' : String(value)
}

function formatDeltaCount(value: number | null) {
  if (value === null) return '-'
  return value > 0 ? `+${value}` : String(value)
}

function formatBooleanFlag(value: boolean | null, truthyLabel: string, falsyLabel: string) {
  if (value === null) return '-'
  return value ? truthyLabel : falsyLabel
}

function countMapEntries(map: Record<string, number>): CountMapEntry[] {
  return Object.entries(map)
    .filter(([, value]) => Number.isFinite(value))
    .sort(([leftKey, leftValue], [rightKey, rightValue]) => rightValue - leftValue || leftKey.localeCompare(rightKey))
    .map(([key, value]) => ({
      key,
      label: formatDiagnosticKey(key),
      value,
    }))
}

function formatDiagnosticKey(key: string) {
  const labels: Record<string, string> = {
    Unknown: '未知来源',
    AiOutcomeSuppliers: 'AI 明细解析',
    LegacyTextParser: '旧文本解析',
    WrappedTextParser: '分段文本解析',
    PdfStructuredTable: 'PDF 结构表',
    CandidateEvidenceParser: '候选证据',
    AwardEvidenceParser: '成交证据',
    NotValidated: '未校验',
    Empty: '为空',
    Accepted: '已通过',
    Rejected: '已驳回',
    NotScored: '未评分',
    Strong: '强候选',
    Weak: '弱候选',
    Unsupported: '无支撑',
  }
  return labels[key] || key || '-'
}

function getValue(source: Record<string, unknown>, key: string) {
  if (Object.prototype.hasOwnProperty.call(source, key)) return source[key]

  const pascalKey = key.charAt(0).toUpperCase() + key.slice(1)
  if (Object.prototype.hasOwnProperty.call(source, pascalKey)) return source[pascalKey]

  return undefined
}

function getString(source: Record<string, unknown>, key: string) {
  const value = getValue(source, key)
  if (value === null || value === undefined) return ''
  return typeof value === 'string' ? value : String(value)
}

function getNumber(source: Record<string, unknown>, key: string) {
  const value = getValue(source, key)
  if (typeof value === 'number') return Number.isFinite(value) ? value : null
  if (typeof value === 'string' && value.trim()) {
    const parsed = Number(value)
    return Number.isFinite(parsed) ? parsed : null
  }
  return null
}

function getBoolean(source: Record<string, unknown>, key: string) {
  const value = getValue(source, key)
  if (typeof value === 'boolean') return value
  if (typeof value === 'string') {
    const normalized = value.trim().toLowerCase()
    if (normalized === 'true') return true
    if (normalized === 'false') return false
  }
  return null
}

function getObject(source: Record<string, unknown>, key: string): Record<string, unknown> | null {
  const value = getValue(source, key)
  return value && typeof value === 'object' && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : null
}

function getNumberRecord(source: Record<string, unknown>, key: string) {
  const value = getObject(source, key)
  if (!value) return {}

  return Object.entries(value).reduce<Record<string, number>>((accumulator, [entryKey, entryValue]) => {
    const parsed =
      typeof entryValue === 'number'
        ? entryValue
        : typeof entryValue === 'string' && entryValue.trim()
          ? Number(entryValue)
          : Number.NaN
    if (Number.isFinite(parsed)) accumulator[entryKey] = parsed
    return accumulator
  }, {})
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
          <section v-if="hasOutcomeDiagnostics" class="outcome-diagnostics">
            <header class="outcome-diagnostics__header">
              <strong>中标明细解析摘要</strong>
              <div class="outcome-diagnostics__tags">
                <el-tag v-if="outcomeDiagnostics.dryRun !== null" size="small" :type="outcomeDiagnostics.dryRun ? 'warning' : 'success'">
                  {{ outcomeDiagnostics.dryRun ? 'Dry Run' : '已落库' }}
                </el-tag>
                <el-tag v-if="outcomeDiagnostics.isOutcomeNotice !== null" size="small" type="info">
                  {{ formatBooleanFlag(outcomeDiagnostics.isOutcomeNotice, '中标公告', '非中标公告') }}
                </el-tag>
              </div>
            </header>
            <div class="ai-summary-grid">
              <div>
                <span>已有明细</span>
                <strong>{{ formatNullableNumber(outcomeDiagnostics.existingCount) }}</strong>
              </div>
              <div>
                <span>预览抽取</span>
                <strong>{{ formatNullableNumber(outcomeDiagnostics.previewExtractedCount) }}</strong>
              </div>
              <div>
                <span>预览保留</span>
                <strong>{{ formatNullableNumber(outcomeDiagnostics.previewSavedCount) }}</strong>
              </div>
              <div>
                <span>本次抽取</span>
                <strong>{{ formatNullableNumber(outcomeDiagnostics.extractedCount) }}</strong>
              </div>
              <div>
                <span>本次落库</span>
                <strong>{{ formatNullableNumber(outcomeDiagnostics.savedCount) }}</strong>
              </div>
              <div>
                <span>合并前候选</span>
                <strong>{{ formatNullableNumber(outcomeDiagnostics.candidateCount) }}</strong>
              </div>
              <div>
                <span>Merge Groups</span>
                <strong>{{ formatNullableNumber(outcomeDiagnostics.mergeGroupCount) }}</strong>
              </div>
              <div>
                <span>被合并候选</span>
                <strong>{{ formatNullableNumber(outcomeDiagnostics.mergedCandidateCount) }}</strong>
              </div>
              <div>
                <span>差异</span>
                <strong>{{ formatDeltaCount(outcomeDiagnostics.deltaCount) }}</strong>
              </div>
            </div>

            <div class="diagnostic-map-grid">
              <section class="diagnostic-map-section">
                <h3>来源分布</h3>
                <div v-if="outcomeSourceEntries.length" class="diagnostic-count-list">
                  <div v-for="entry in outcomeSourceEntries" :key="entry.key" class="diagnostic-count-row">
                    <span>{{ entry.label }}</span>
                    <strong>{{ entry.value }}</strong>
                  </div>
                </div>
                <span v-else class="diagnostic-empty">暂无统计</span>
              </section>
              <section class="diagnostic-map-section">
                <h3>分标编号校验</h3>
                <div v-if="outcomeLotNoValidationEntries.length" class="diagnostic-count-list">
                  <div v-for="entry in outcomeLotNoValidationEntries" :key="entry.key" class="diagnostic-count-row">
                    <span>{{ entry.label }}</span>
                    <strong>{{ entry.value }}</strong>
                  </div>
                </div>
                <span v-else class="diagnostic-empty">暂无统计</span>
              </section>
              <section class="diagnostic-map-section">
                <h3>强弱评分</h3>
                <div v-if="outcomeStrengthEntries.length" class="diagnostic-count-list">
                  <div v-for="entry in outcomeStrengthEntries" :key="entry.key" class="diagnostic-count-row">
                    <span>{{ entry.label }}</span>
                    <strong>{{ entry.value }}</strong>
                  </div>
                </div>
                <span v-else class="diagnostic-empty">暂无统计</span>
              </section>
            </div>
          </section>
          <pre class="code-panel">{{ formattedResult }}</pre>
        </el-tab-pane>

        <el-tab-pane v-if="hasAiResponses" label="AI 返回">
          <div class="deepseek-list">
            <el-alert
              title="AI 原始返回用于诊断，不等同于审核页最终落库结果；最终中标明细还会经过确定性解析、合并、清洗和持久化。"
              type="info"
              show-icon
              :closable="false"
            />
            <section
              v-for="(item, index) in aiResponses"
              :key="`${item.use}-${index}`"
              class="deepseek-response"
            >
              <header class="deepseek-response__header">
                <div>
                  <strong>{{ aiResponseTitle(item, index) }}</strong>
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
              <div class="ai-summary-grid">
                <div>
                  <span>Records</span>
                  <strong>{{ item.summary.records ?? '-' }}</strong>
                </div>
                <div>
                  <span>Packages</span>
                  <strong>{{ item.summary.packages ?? '-' }}</strong>
                </div>
                <div>
                  <span>Requirements</span>
                  <strong>{{ item.summary.requirements ?? '-' }}</strong>
                </div>
                <div>
                  <span>供应商</span>
                  <strong>{{ formatFilledRatio(item.summary.supplierNameFilled, item.summary.records) }}</strong>
                </div>
                <div>
                  <span>分标编号</span>
                  <strong>{{ formatFilledRatio(item.summary.lotNoFilled, item.summary.records) }}</strong>
                </div>
                <div>
                  <span>分标名称</span>
                  <strong>{{ formatFilledRatio(item.summary.lotNameFilled, item.summary.records) }}</strong>
                </div>
                <div>
                  <span>包号</span>
                  <strong>{{ formatFilledRatio(item.summary.packageNoFilled, item.summary.records) }}</strong>
                </div>
                <div>
                  <span>证据文本</span>
                  <strong>{{ formatFilledRatio(item.summary.evidenceTextFilled, item.summary.records) }}</strong>
                </div>
                <div>
                  <span>原始行</span>
                  <strong>{{ formatFilledRatio(item.summary.sourceRowTextFilled, item.summary.records) }}</strong>
                </div>
                <div>
                  <span>字段证据</span>
                  <strong>{{ formatFilledRatio(item.summary.fieldEvidenceFilled, item.summary.records) }}</strong>
                </div>
                <div>
                  <span>警告行</span>
                  <strong>{{ item.summary.warningRows ?? '-' }}</strong>
                </div>
              </div>

              <section v-if="item.requestSummaryJson" class="deepseek-response__section">
                <h3>AI 请求摘要</h3>
                <pre class="code-panel">{{ formatAiResponseText(item.requestSummaryJson) }}</pre>
              </section>

              <section v-if="item.requestBodyJson" class="deepseek-response__section">
                <h3>AI 请求体</h3>
                <pre class="code-panel">{{ formatAiResponseText(item.requestBodyJson) }}</pre>
              </section>

              <section v-if="item.requestPrompt" class="deepseek-response__section">
                <h3>AI 提示词 / 输入内容</h3>
                <pre class="code-panel">{{ formatAiResponseText(item.requestPrompt) }}</pre>
              </section>

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

.outcome-diagnostics {
  display: grid;
  gap: 12px;
  margin-bottom: 14px;
}

.outcome-diagnostics__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.outcome-diagnostics__header strong {
  color: #17202a;
  font-size: 15px;
}

.outcome-diagnostics__tags {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 6px;
}

.diagnostic-map-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 10px;
}

.diagnostic-map-section {
  display: grid;
  align-content: start;
  gap: 8px;
  padding: 10px;
  border: 1px solid #dce3ee;
  border-radius: 6px;
  background: #fff;
}

.diagnostic-map-section h3 {
  margin: 0;
  color: #17202a;
  font-size: 13px;
}

.diagnostic-count-list {
  display: grid;
  gap: 6px;
}

.diagnostic-count-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  color: #445064;
  font-size: 13px;
}

.diagnostic-count-row strong {
  color: #17202a;
  font-size: 13px;
}

.diagnostic-empty {
  color: #8a94a6;
  font-size: 13px;
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

.ai-summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
  gap: 8px;
}

.ai-summary-grid > div {
  display: grid;
  gap: 4px;
  padding: 10px;
  border: 1px solid #dce3ee;
  border-radius: 6px;
  background: #f8fafc;
}

.ai-summary-grid span {
  color: #687385;
  font-size: 12px;
}

.ai-summary-grid strong {
  color: #17202a;
  font-size: 14px;
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
