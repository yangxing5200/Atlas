<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { rawNoticesApi } from '@/api/bidops/rawNotices.api'
import { reviewTasksApi } from '@/api/bidops/reviewTasks.api'
import { suppliersApi } from '@/api/bidops/suppliers.api'
import type { BackgroundJobDetailDto } from '@/modules/operations/types'
import { formatDateTime } from '@/shared/utils/date'
import { formatMoney } from '@/shared/utils/money'
import RawNoticePipelinePanel from './RawNoticePipelinePanel.vue'
import type {
  OutcomeSupplierRecordDto,
  RawNoticePipelineDto,
  ReviewTaskDetailDto,
} from '../types'
import { formatCategory, formatCommonStatus, formatNoticeType, formatPackageNo } from '../utils/display'

const props = defineProps<{
  job?: BackgroundJobDetailDto | null
}>()

const router = useRouter()
const loading = ref(false)
const loadError = ref('')
const pipeline = ref<RawNoticePipelineDto | null>(null)
const reviewDetail = ref<ReviewTaskDetailDto | null>(null)
const directOutcomeRows = ref<OutcomeSupplierRecordDto[]>([])

const rawNoticeId = computed(() => extractRawNoticeId(props.job))
const outcomeRows = computed(() => {
  const rows = reviewDetail.value?.outcomeSuppliers || []
  return rows.length > 0 ? rows : directOutcomeRows.value
})
const packages = computed(() => reviewDetail.value?.packages || [])
const notice = computed(() => reviewDetail.value?.notice || null)

watch(
  () => [props.job?.id, rawNoticeId.value],
  () => {
    void loadParsedResult()
  },
  { immediate: true },
)

async function loadParsedResult() {
  pipeline.value = null
  reviewDetail.value = null
  directOutcomeRows.value = []
  loadError.value = ''

  const id = rawNoticeId.value
  if (!id) return

  loading.value = true
  try {
    const [pipelineResult, outcomeResult] = await Promise.all([
      rawNoticesApi.pipeline(id).catch(() => null),
      suppliersApi.outcomeRecords({ rawNoticeId: id, pageIndex: 1, pageSize: 200 }).catch(() => null),
    ])

    pipeline.value = pipelineResult
    directOutcomeRows.value = outcomeResult?.items || []

    if (pipelineResult?.reviewTaskId) {
      reviewDetail.value = await reviewTasksApi.get(String(pipelineResult.reviewTaskId)).catch(() => null)
    }
  } finally {
    loading.value = false
    if (!pipeline.value && directOutcomeRows.value.length === 0 && !reviewDetail.value) {
      loadError.value = '暂时没有读取到该任务对应的解析结果。'
    }
  }
}

function openRawNotice() {
  if (!rawNoticeId.value) return
  void router.push(`/bidops/crawl/raw-notices/${rawNoticeId.value}`)
}

function openReviewTask() {
  if (!pipeline.value?.reviewTaskId) return
  void router.push(`/bidops/review/tasks/${pipeline.value.reviewTaskId}`)
}

function confidencePercent(value?: number | null) {
  return `${Math.round(Number(value || 0) * 100)}%`
}

function formatRank(value?: number | null) {
  return value ? String(value) : '-'
}

function extractRawNoticeId(job?: BackgroundJobDetailDto | null) {
  if (!job) return ''

  const sources = [job.result, job.payload, job.payloadPreview, job.resultPreview, job.deduplicationKey]
  for (const source of sources) {
    const matched = extractIdFromText(source)
    if (matched) return matched
  }

  const fromPayload = extractIdFromJson(job.payload, ['rawNoticeId'])
  if (fromPayload) return fromPayload

  return ''
}

function extractIdFromJson(value: unknown, keys: string[]) {
  if (typeof value !== 'string' || !value.trim()) return ''

  try {
    return findIdValue(JSON.parse(value), keys.map((key) => key.toLowerCase()))
  } catch {
    return ''
  }
}

function findIdValue(source: unknown, normalizedKeys: string[]): string {
  if (source === null || source === undefined) return ''

  if (Array.isArray(source)) {
    for (const item of source) {
      const value = findIdValue(item, normalizedKeys)
      if (value) return value
    }
    return ''
  }

  if (typeof source !== 'object') return ''

  for (const [key, value] of Object.entries(source as Record<string, unknown>)) {
    if (normalizedKeys.includes(key.toLowerCase())) {
      const normalized = normalizeId(value)
      if (normalized) return normalized
    }
  }

  for (const value of Object.values(source as Record<string, unknown>)) {
    const nested = findIdValue(value, normalizedKeys)
    if (nested) return nested
  }

  return ''
}

function extractIdFromText(value?: string | null) {
  if (!value) return ''

  const patterns = [
    /"rawNoticeId"\s*:\s*"?(\d+)"?/i,
    /\brawNoticeId\s*=\s*(\d+)/i,
    /\brawNoticeId\s*:\s*(\d+)/i,
    /bidops:structured-parse:[^:]+:\d+:(\d+)/i,
    /bidops:review-outcome-ai-reparse:\d+:(\d+):/i,
    /bidops:outcome-supplier-extract:\d+:(\d+):/i,
    /bidops:manual-reparse:\d+:(\d+):/i,
  ]

  for (const pattern of patterns) {
    const match = value.match(pattern)
    const normalized = normalizeId(match?.[1])
    if (normalized) return normalized
  }

  return ''
}

function normalizeId(value: unknown) {
  if (value === null || value === undefined) return ''
  const text = String(value).trim()
  return /^\d+$/.test(text) ? text : ''
}
</script>

<template>
  <el-empty v-if="!rawNoticeId" description="该任务没有关联 RawNoticeId" />
  <el-skeleton v-else-if="loading" :rows="8" animated />
  <div v-else class="parsed-result-panel">
    <section class="result-actions">
      <el-button type="primary" text @click="openRawNotice">打开原始公告</el-button>
      <el-button v-if="pipeline?.reviewTaskId" type="primary" text @click="openReviewTask">打开审核详情</el-button>
      <el-button text :loading="loading" @click="loadParsedResult">刷新解析结果</el-button>
    </section>

    <el-alert v-if="loadError" type="warning" show-icon :closable="false" :title="loadError" />

    <el-descriptions :column="2" border>
      <el-descriptions-item label="RawNoticeId">{{ rawNoticeId }}</el-descriptions-item>
      <el-descriptions-item label="审核任务">{{ pipeline?.reviewTaskId || '-' }}</el-descriptions-item>
      <el-descriptions-item label="暂存公告">{{ pipeline?.noticeStagingId || '-' }}</el-descriptions-item>
      <el-descriptions-item label="正式公告">{{ pipeline?.noticeId || '-' }}</el-descriptions-item>
      <el-descriptions-item label="包件 / 要求">{{ pipeline?.packageCount ?? 0 }} / {{ pipeline?.requirementCount ?? 0 }}</el-descriptions-item>
      <el-descriptions-item label="中标/候选明细">{{ outcomeRows.length }}</el-descriptions-item>
    </el-descriptions>

    <section class="parsed-section">
      <h3>处理流水线</h3>
      <RawNoticePipelinePanel :pipeline="pipeline" />
    </section>

    <section class="parsed-section">
      <h3>暂存公告</h3>
      <el-empty v-if="!notice" description="暂无暂存公告" />
      <el-descriptions v-else :column="2" border>
        <el-descriptions-item label="公告类型">{{ formatNoticeType(notice.noticeType) }}</el-descriptions-item>
        <el-descriptions-item label="置信度">{{ confidencePercent(notice.aiConfidence) }}</el-descriptions-item>
        <el-descriptions-item label="项目名称" :span="2">{{ notice.projectName || '-' }}</el-descriptions-item>
        <el-descriptions-item label="采购编号">{{ notice.projectCode || '-' }}</el-descriptions-item>
        <el-descriptions-item label="采购人">{{ notice.buyerName || '-' }}</el-descriptions-item>
        <el-descriptions-item label="代理机构">{{ notice.agencyName || '-' }}</el-descriptions-item>
        <el-descriptions-item label="地区">{{ notice.region || '-' }}</el-descriptions-item>
        <el-descriptions-item label="发布时间">{{ formatDateTime(notice.publishTime) }}</el-descriptions-item>
        <el-descriptions-item label="投标截止">{{ formatDateTime(notice.bidDeadline) }}</el-descriptions-item>
      </el-descriptions>
    </section>

    <section class="parsed-section">
      <h3>中标/候选明细</h3>
      <el-table :data="outcomeRows" border size="small" empty-text="暂无中标/候选明细">
        <el-table-column prop="projectCode" label="采购编号" min-width="150" show-overflow-tooltip />
        <el-table-column prop="projectName" label="项目名称" min-width="220" show-overflow-tooltip />
        <el-table-column prop="lotNo" label="分标编号" min-width="140" show-overflow-tooltip />
        <el-table-column prop="lotName" label="分标名称" min-width="140" show-overflow-tooltip />
        <el-table-column label="包号" width="110" show-overflow-tooltip>
          <template #default="{ row }">{{ formatPackageNo(row.packageNo) }}</template>
        </el-table-column>
        <el-table-column prop="packageName" label="包名称" min-width="180" show-overflow-tooltip />
        <el-table-column label="结果" width="100">
          <template #default="{ row }">{{ formatCommonStatus(row.outcomeType) }}</template>
        </el-table-column>
        <el-table-column label="排名" width="80">
          <template #default="{ row }">{{ formatRank(row.rank) }}</template>
        </el-table-column>
        <el-table-column prop="supplierName" label="厂家" min-width="210" show-overflow-tooltip />
        <el-table-column label="金额" width="140" align="right">
          <template #default="{ row }">{{ formatMoney(row.awardAmount) }}</template>
        </el-table-column>
        <el-table-column label="代理服务费" width="140" align="right">
          <template #default="{ row }">{{ formatMoney(row.procurementAgencyServiceFeeAmount) }}</template>
        </el-table-column>
        <el-table-column label="置信度" width="100">
          <template #default="{ row }">{{ confidencePercent(row.extractionConfidence) }}</template>
        </el-table-column>
        <el-table-column prop="evidenceText" label="证据" min-width="260" show-overflow-tooltip />
      </el-table>
    </section>

    <section class="parsed-section">
      <h3>包件明细</h3>
      <el-table :data="packages" border size="small" empty-text="暂无包件明细">
        <el-table-column prop="lotNo" label="分标编号" min-width="130" show-overflow-tooltip />
        <el-table-column prop="lotName" label="分标名称" min-width="140" show-overflow-tooltip />
        <el-table-column label="包号" width="110">
          <template #default="{ row }">{{ formatPackageNo(row.packageNo) }}</template>
        </el-table-column>
        <el-table-column prop="packageName" label="包名称" min-width="220" show-overflow-tooltip />
        <el-table-column label="品类" width="110">
          <template #default="{ row }">{{ formatCategory(row.category) }}</template>
        </el-table-column>
        <el-table-column label="预算" width="130" align="right">
          <template #default="{ row }">{{ formatMoney(row.budgetAmount) }}</template>
        </el-table-column>
        <el-table-column label="要求项" width="90">
          <template #default="{ row }">{{ row.requirements?.length || 0 }}</template>
        </el-table-column>
      </el-table>
    </section>
  </div>
</template>

<style scoped>
.parsed-result-panel {
  display: grid;
  gap: 16px;
}

.result-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.parsed-section {
  display: grid;
  gap: 10px;
}

.parsed-section h3 {
  margin: 0;
  color: var(--el-text-color-primary);
  font-size: 15px;
}
</style>
