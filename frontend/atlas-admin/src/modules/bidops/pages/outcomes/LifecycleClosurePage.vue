<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Check, Close, Link as LinkIcon, Refresh, Search, Tickets, Upload, View } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { lifecycleApi } from '@/api/bidops/lifecycle.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { usePermission } from '@/shared/composables/usePermission'
import { formatDateTime } from '@/shared/utils/date'
import { formatMoney } from '@/shared/utils/money'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import RawAttachmentTable from '../../components/RawAttachmentTable.vue'
import { BIDOPS_PERMISSIONS } from '../../constants'
import type { LifecycleNoticeRefDto, LifecyclePackageLinkDto, LifecycleProcurementNoticeCandidateDto } from '../../types'
import {
  formatNoticeType,
  lifecycleLinkMatchTypeOptions,
  lifecycleLinkStatusOptions,
} from '../../utils/display'

interface LifecycleLinkQuery {
  keyword: string
  projectCode: string
  lotNo: string
  lotName: string
  packageNo: string
  supplierName: string
  linkStatus: string
  matchType: string
  requiresManualReview: boolean | ''
  rawNoticeId: string
  sortBy: string
  pageIndex: number
  pageSize: number
}

type SortOrder = 'ascending' | 'descending' | null

const lifecycleSortFields: Record<string, { ascending: string; descending: string }> = {
  projectCode: { ascending: 'ProjectCodeAsc', descending: 'ProjectCodeDesc' },
  lotNo: { ascending: 'LotNoAsc', descending: 'LotNoDesc' },
  lotName: { ascending: 'LotNameAsc', descending: 'LotNameDesc' },
  packageNo: { ascending: 'PackageNoAsc', descending: 'PackageNoDesc' },
  supplierName: { ascending: 'SupplierNameAsc', descending: 'SupplierNameDesc' },
  finalAwardAmount: { ascending: 'AmountAsc', descending: 'AmountDesc' },
  matchScore: { ascending: 'ScoreAsc', descending: 'ScoreDesc' },
  linkStatus: { ascending: 'LinkStatusAsc', descending: 'LinkStatusDesc' },
  requiresManualReview: { ascending: 'ReviewRequiredAsc', descending: 'ReviewRequiredDesc' },
  updatedAt: { ascending: 'UpdatedAsc', descending: 'UpdatedDesc' },
}

const route = useRoute()
const router = useRouter()
const { visible: canApprove } = usePermission(BIDOPS_PERMISSIONS.REVIEW_APPROVE)
const detailVisible = ref(false)
const decisionVisible = ref(false)
const decisionLoading = ref(false)
const decisionMode = ref<'confirm' | 'reject'>('confirm')
const activeLink = ref<LifecyclePackageLinkDto | null>(null)
const decisionLink = ref<LifecyclePackageLinkDto | null>(null)
const procurementSearchVisible = ref(false)
const procurementSearchLoading = ref(false)
const procurementImportingKey = ref('')
const procurementSearchLink = ref<LifecyclePackageLinkDto | null>(null)
const procurementCandidates = ref<LifecycleProcurementNoticeCandidateDto[]>([])
const fieldEnrichmentLoadingKey = ref('')
const fieldPromptVisible = ref(false)
const fieldPromptLink = ref<LifecyclePackageLinkDto | null>(null)

const decisionForm = reactive({
  remark: '',
  finalAwardAmount: null as number | null,
  finalAwardAmountSource: '',
})
const fieldPromptForm = reactive({
  reviewerPrompt: '',
})

const table = useTableQuery<LifecyclePackageLinkDto, LifecycleLinkQuery>(
  (params) =>
    lifecycleApi.links({
      ...params,
      keyword: params.keyword.trim() || undefined,
      projectCode: params.projectCode.trim() || undefined,
      lotNo: params.lotNo.trim() || undefined,
      lotName: params.lotName.trim() || undefined,
      packageNo: params.packageNo.trim() || undefined,
      supplierName: params.supplierName.trim() || undefined,
      linkStatus: params.linkStatus || undefined,
      matchType: params.matchType || undefined,
      requiresManualReview: params.requiresManualReview === '' ? undefined : params.requiresManualReview,
      rawNoticeId: params.rawNoticeId.trim() || undefined,
      sortBy: params.sortBy || undefined,
    }),
  {
    keyword: '',
    projectCode: '',
    lotNo: '',
    lotName: '',
    packageNo: '',
    supplierName: '',
    linkStatus: 'Suggested',
    matchType: '',
    requiresManualReview: '',
    rawNoticeId: '',
    sortBy: 'UpdatedDesc',
    pageIndex: 1,
    pageSize: 20,
  },
  {
    storageKey: 'atlas.bidops.lifecycle-links.query.v1',
    immediate: false,
  },
)

const selectedReasons = computed(() => parseJsonTextList(activeLink.value?.matchReasonsJson))
const selectedMissingFields = computed(() => parseJsonTextList(activeLink.value?.missingFieldsJson))
const selectedEvidenceJson = computed(() => prettyJson(activeLink.value?.evidenceJson))
const selectedFieldEnrichment = computed(() => fieldEnrichment(activeLink.value))
const decisionTitle = computed(() => (decisionMode.value === 'confirm' ? '确认生命周期关联' : '驳回生命周期关联'))

function sortOrder(ascending: string, descending: string): SortOrder {
  if (table.query.sortBy === ascending)
    return 'ascending'
  if (table.query.sortBy === descending)
    return 'descending'

  return null
}

async function handleSortChange({ prop, order }: { prop: string; order: SortOrder }) {
  const field = lifecycleSortFields[prop]
  table.query.sortBy = field && order ? field[order] : ''
  table.query.pageIndex = 1
  await table.loadData()
}

function openDetail(row: LifecyclePackageLinkDto) {
  activeLink.value = row
  detailVisible.value = true
}

function openDecision(row: LifecyclePackageLinkDto, mode: 'confirm' | 'reject') {
  decisionLink.value = row
  decisionMode.value = mode
  decisionForm.remark = row.manualRemark || ''
  decisionForm.finalAwardAmount = row.finalAwardAmount ?? null
  decisionForm.finalAwardAmountSource = row.finalAwardAmountSource || ''
  decisionVisible.value = true
}

async function submitDecision() {
  if (!decisionLink.value) return
  if (decisionMode.value === 'reject' && !decisionForm.remark.trim()) {
    ElMessage.warning('请填写驳回原因')
    return
  }

  decisionLoading.value = true
  try {
    const request = {
      remark: decisionForm.remark.trim(),
      finalAwardAmount: decisionForm.finalAwardAmount,
      finalAwardAmountSource: decisionForm.finalAwardAmountSource.trim() || null,
      requiresManualReview: false,
    }
    const updated = decisionMode.value === 'confirm'
      ? await lifecycleApi.confirmLink(decisionLink.value.id, request)
      : await lifecycleApi.rejectLink(decisionLink.value.id, request)
    replaceRow(updated)
    if (activeLink.value?.id === updated.id) activeLink.value = updated
    ElMessage.success(decisionMode.value === 'confirm' ? '关联已确认' : '关联已驳回')
    decisionVisible.value = false
  } finally {
    decisionLoading.value = false
  }
}

function replaceRow(updated: LifecyclePackageLinkDto) {
  const index = table.result.items.findIndex((item) => item.id === updated.id)
  if (index >= 0) table.result.items.splice(index, 1, updated)
}

async function enqueueFieldEnrichment(row?: LifecyclePackageLinkDto | null, reviewerPrompt = '') {
  const link = row || activeLink.value
  if (!link) return
  if (!link.awardRawNoticeId) {
    ElMessage.warning('当前记录没有中标公告 RawNotice，无法补全')
    return
  }

  const key = `${link.id}:${reviewerPrompt ? 'prompt' : 'auto'}`
  fieldEnrichmentLoadingKey.value = key
  try {
    const job = await lifecycleApi.enqueueFieldEnrichment(link.id, {
      reviewerPrompt: reviewerPrompt.trim() || null,
    })
    ElMessage.success(job.alreadyExists ? `字段补全任务已存在：${job.jobId}` : `字段补全任务已提交：${job.jobId}`)
    fieldPromptVisible.value = false
    fieldPromptForm.reviewerPrompt = ''
  } finally {
    fieldEnrichmentLoadingKey.value = ''
  }
}

function openFieldPrompt(row?: LifecyclePackageLinkDto | null) {
  const link = row || activeLink.value
  if (!link) return
  fieldPromptLink.value = link
  fieldPromptForm.reviewerPrompt = ''
  fieldPromptVisible.value = true
}

async function openProcurementSearch(row?: LifecyclePackageLinkDto | null) {
  const link = row || activeLink.value
  if (!link) return
  if (!link.projectCode) {
    ElMessage.warning('当前闭环记录没有采购编号，无法按项目编号搜索')
    return
  }

  procurementSearchLink.value = link
  procurementSearchVisible.value = true
  await searchProcurementCandidates()
}

async function searchProcurementCandidates() {
  if (!procurementSearchLink.value) return

  procurementSearchLoading.value = true
  try {
    procurementCandidates.value = await lifecycleApi.procurementCandidates(procurementSearchLink.value.id)
  } finally {
    procurementSearchLoading.value = false
  }
}

async function importProcurementCandidate(candidate: LifecycleProcurementNoticeCandidateDto) {
  const link = procurementSearchLink.value
  if (!link) return

  const key = candidateKey(candidate)
  procurementImportingKey.value = key
  try {
    const result = await lifecycleApi.importProcurementCandidate(link.id, {
      detailUrl: candidate.detailUrl,
      title: candidate.title,
      noticeType: candidate.noticeType,
      projectCode: candidate.projectCode,
      sourceId: candidate.sourceId,
      channelId: candidate.channelId,
    })

    if (result.rawNoticeId) {
      ElMessage.success('采购公告已关联到闭环记录')
    } else if (result.importJob) {
      ElMessage.success(`采购公告导入任务已提交：${result.importJob.jobId}`)
    } else {
      ElMessage.success(result.message || '操作已提交')
    }

    await table.loadData()
    const updated = table.result.items.find((item) => item.id === link.id)
    if (updated) {
      procurementSearchLink.value = updated
      if (activeLink.value?.id === updated.id) activeLink.value = updated
    }
    procurementSearchVisible.value = false
  } finally {
    procurementImportingKey.value = ''
  }
}

function candidateKey(candidate: LifecycleProcurementNoticeCandidateDto) {
  return candidate.detailUrl || String(candidate.noticeId)
}

function scorePercent(value: number) {
  return Math.max(0, Math.min(100, Math.round(Number(value || 0) * 100)))
}

function lifecycleLotNo(row: LifecyclePackageLinkDto) {
  return firstDisplayText(
    row.lotNo,
    evidenceString(row, ['lotNo']),
    evidenceString(row, ['award', 'lotNo']),
    evidenceString(row, ['matchedCandidate', 'lotNo']),
    evidenceString(row, ['tender', 'lotNo']),
  )
}

function lifecycleLotName(row: LifecyclePackageLinkDto) {
  return firstDisplayText(
    row.lotName,
    evidenceString(row, ['lotName']),
    evidenceString(row, ['award', 'lotName']),
    evidenceString(row, ['matchedCandidate', 'lotName']),
    evidenceString(row, ['tender', 'lotName']),
  )
}

function lifecyclePackageText(row: LifecyclePackageLinkDto) {
  const packageNo = firstDisplayText(row.packageNo, evidenceString(row, ['packageNo']), evidenceString(row, ['tender', 'packageNo']))
  const packageName = firstDisplayText(row.packageName, evidenceString(row, ['packageName']), evidenceString(row, ['tender', 'packageName']))
  return [packageNo, packageName].filter(Boolean).join(' / ') || '-'
}

function shortTitle(value?: string | null, max = 34) {
  const text = firstDisplayText(value)
  if (!text) return '-'
  return text.length > max ? `${text.slice(0, max)}...` : text
}

function noticeMatchLabel(value?: string | null) {
  if (value === 'InferredByProjectCode') return '按采购编号推断'
  if (value === 'Linked') return '已关联'
  return value || '-'
}

function openRawNotice(id?: string | null) {
  if (id) router.push(`/bidops/crawl/raw-notices/${id}`)
}

function openNoticeRef(notice?: LifecycleNoticeRefDto | null) {
  if (notice?.rawNoticeId) openRawNotice(notice.rawNoticeId)
}

function openTenderPackage(id?: string | null) {
  if (id) router.push(`/bidops/packages/${id}`)
}

function openExternalUrl(url?: string | null) {
  if (url) window.open(url, '_blank', 'noopener,noreferrer')
}

function parseJsonTextList(value?: string | null) {
  if (!value) return []

  try {
    const parsed = JSON.parse(value)
    if (Array.isArray(parsed)) return parsed.map((item) => String(item)).filter(Boolean)
    if (typeof parsed === 'string') return [parsed]
    return Object.entries(parsed).map(([key, item]) => `${key}: ${String(item)}`)
  } catch {
    return [value]
  }
}

function prettyJson(value?: string | null) {
  if (!value) return ''

  try {
    return JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    return value
  }
}

function firstDisplayText(...values: Array<unknown>) {
  for (const value of values) {
    if (value === null || value === undefined) continue
    const text = String(value).trim()
    if (text && text !== '-') return text
  }
  return ''
}

function evidenceString(row: LifecyclePackageLinkDto, path: string[]) {
  if (!row.evidenceJson) return ''

  try {
    let current: unknown = JSON.parse(row.evidenceJson)
    for (const key of path) {
      if (!current || typeof current !== 'object' || !(key in current)) return ''
      current = (current as Record<string, unknown>)[key]
    }

    return firstDisplayText(current)
  } catch {
    return ''
  }
}

function missingFieldCount(row: LifecyclePackageLinkDto) {
  return parseJsonTextList(row.missingFieldsJson).length
}

interface FieldEnrichmentField {
  fieldName?: string
  value?: string
  numericValue?: number | string | null
  sourceStage?: string
  sourceRawNoticeId?: number | string | null
  sourceRawAttachmentId?: number | string | null
  evidenceText?: string
  confidence?: number
  reason?: string
}

interface FieldEnrichmentView {
  generatedAtUtc?: string
  reviewerPromptProvided?: boolean
  result?: {
    fields?: FieldEnrichmentField[]
    confidence?: number
    requiresManualReview?: boolean
    summary?: string
    conflicts?: string[]
  }
}

function fieldEnrichment(row?: LifecyclePackageLinkDto | null): FieldEnrichmentView | null {
  if (!row?.evidenceJson) return null
  try {
    const parsed = JSON.parse(row.evidenceJson) as Record<string, unknown>
    const enrichment = parsed.fieldEnrichment as FieldEnrichmentView | undefined
    return enrichment || null
  } catch {
    return null
  }
}

function fieldConfidencePercent(value?: number) {
  return scorePercent(Number(value || 0))
}

async function loadForRoute() {
  const rawNoticeId = String(route.query.rawNoticeId || '').trim()
  if (rawNoticeId) {
    table.query.rawNoticeId = rawNoticeId
    table.query.pageIndex = 1
    await table.search()
    return
  }

  await table.loadData()
}

onMounted(loadForRoute)

watch(
  () => route.query.rawNoticeId,
  async (value, oldValue) => {
    if (value === oldValue) return
    const rawNoticeId = String(value || '').trim()
    if (!rawNoticeId) return
    table.query.rawNoticeId = rawNoticeId
    table.query.pageIndex = 1
    await table.search()
  },
)
</script>

<template>
  <PageContainer title="闭环任务与审核中心" description="查看中标结果闭环分析进度、包件建议、证据、失败/缺失原因和人工确认结果。">
    <template #actions>
      <el-button :icon="Tickets" @click="router.push('/bidops/notices')">结果公告</el-button>
      <el-button :icon="Tickets" @click="router.push('/bidops/operations/jobs')">后台任务</el-button>
      <el-button :icon="Refresh" :loading="table.loading" @click="table.loadData">刷新</el-button>
    </template>

    <SearchForm @search="table.search" @reset="table.reset()">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="项目 / 分标 / 包件 / 厂家" />
      </el-form-item>
      <el-form-item label="采购编号">
        <el-input v-model="table.query.projectCode" clearable placeholder="采购编号 / 项目编号" style="width: 210px" />
      </el-form-item>
      <el-form-item label="分标编号">
        <el-input v-model="table.query.lotNo" clearable placeholder="分标编号" style="width: 150px" />
      </el-form-item>
      <el-form-item label="分标名称">
        <el-input v-model="table.query.lotName" clearable placeholder="分标名称" style="width: 170px" />
      </el-form-item>
      <el-form-item label="包号">
        <el-input v-model="table.query.packageNo" clearable placeholder="包号" style="width: 130px" />
      </el-form-item>
      <el-form-item label="供应商">
        <el-input v-model="table.query.supplierName" clearable placeholder="中标/候选供应商" style="width: 190px" />
      </el-form-item>
      <el-form-item label="状态">
        <el-select v-model="table.query.linkStatus" clearable placeholder="全部" style="width: 140px">
          <el-option v-for="item in lifecycleLinkStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="匹配">
        <el-select v-model="table.query.matchType" clearable placeholder="全部" style="width: 140px">
          <el-option v-for="item in lifecycleLinkMatchTypeOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="人工复核">
        <el-select v-model="table.query.requiresManualReview" clearable placeholder="全部" style="width: 130px">
          <el-option label="需要" :value="true" />
          <el-option label="不需要" :value="false" />
        </el-select>
      </el-form-item>
      <el-form-item label="RawNoticeId">
        <el-input v-model="table.query.rawNoticeId" clearable placeholder="任一关联公告" style="width: 170px" />
      </el-form-item>
    </SearchForm>

    <DataTable :data="table.result.items" :loading="table.loading" empty-text="暂无生命周期关联建议" @sort-change="handleSortChange">
      <el-table-column
        prop="projectCode"
        label="项目"
        min-width="300"
        show-overflow-tooltip
        sortable="custom"
        :sort-order="sortOrder('ProjectCodeAsc', 'ProjectCodeDesc')"
      >
        <template #default="{ row }">
          <div class="main-cell">
            <strong>{{ row.projectName || '-' }}</strong>
            <span>{{ row.projectCode || '未识别采购编号' }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="采购公告" min-width="280" show-overflow-tooltip>
        <template #default="{ row }">
          <div v-if="row.procurementNotice" class="main-cell">
            <el-button link type="primary" :icon="LinkIcon" @click="openNoticeRef(row.procurementNotice)">
              {{ shortTitle(row.procurementNotice.title) }}
            </el-button>
            <span>{{ noticeMatchLabel(row.procurementNotice.matchSource) }} · {{ formatDateTime(row.procurementNotice.publishTime) || '-' }}</span>
          </div>
          <div v-else class="missing-procurement-cell">
            <el-tag type="warning" effect="light">未匹配采购公告</el-tag>
            <el-button link type="primary" :icon="Search" @click="openProcurementSearch(row)">搜索</el-button>
          </div>
        </template>
      </el-table-column>
      <el-table-column
        prop="lotNo"
        label="分标编号"
        min-width="150"
        show-overflow-tooltip
        sortable="custom"
        :sort-order="sortOrder('LotNoAsc', 'LotNoDesc')"
      >
        <template #default="{ row }">{{ lifecycleLotNo(row) || '-' }}</template>
      </el-table-column>
      <el-table-column
        prop="lotName"
        label="分标名称"
        min-width="180"
        show-overflow-tooltip
        sortable="custom"
        :sort-order="sortOrder('LotNameAsc', 'LotNameDesc')"
      >
        <template #default="{ row }">{{ lifecycleLotName(row) || '-' }}</template>
      </el-table-column>
      <el-table-column
        prop="packageNo"
        label="包件"
        min-width="240"
        show-overflow-tooltip
        sortable="custom"
        :sort-order="sortOrder('PackageNoAsc', 'PackageNoDesc')"
      >
        <template #default="{ row }">{{ lifecyclePackageText(row) }}</template>
      </el-table-column>
      <el-table-column
        prop="supplierName"
        label="中标商家"
        min-width="220"
        show-overflow-tooltip
        sortable="custom"
        :sort-order="sortOrder('SupplierNameAsc', 'SupplierNameDesc')"
      >
        <template #default="{ row }">{{ row.supplierName || '-' }}</template>
      </el-table-column>
      <el-table-column
        prop="finalAwardAmount"
        label="中标金额"
        width="150"
        align="right"
        sortable="custom"
        :sort-order="sortOrder('AmountAsc', 'AmountDesc')"
      >
        <template #default="{ row }">{{ formatMoney(row.finalAwardAmount) }}</template>
      </el-table-column>
      <el-table-column
        prop="matchScore"
        label="匹配分"
        width="130"
        sortable="custom"
        :sort-order="sortOrder('ScoreAsc', 'ScoreDesc')"
      >
        <template #default="{ row }">
          <el-progress :percentage="scorePercent(row.matchScore)" :stroke-width="8" />
        </template>
      </el-table-column>
      <el-table-column label="匹配类型" width="120">
        <template #default="{ row }"><BidOpsStatusTag :value="row.matchType" /></template>
      </el-table-column>
      <el-table-column
        prop="linkStatus"
        label="状态"
        width="120"
        sortable="custom"
        :sort-order="sortOrder('LinkStatusAsc', 'LinkStatusDesc')"
      >
        <template #default="{ row }"><BidOpsStatusTag :value="row.linkStatus" /></template>
      </el-table-column>
      <el-table-column
        prop="requiresManualReview"
        label="复核"
        width="100"
        sortable="custom"
        :sort-order="sortOrder('ReviewRequiredAsc', 'ReviewRequiredDesc')"
      >
        <template #default="{ row }">
          <el-tag :type="row.requiresManualReview ? 'warning' : 'success'" effect="light">
            {{ row.requiresManualReview ? '需要' : '不需要' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="失败/缺失" width="120">
        <template #default="{ row }">
          <el-tag :type="missingFieldCount(row) > 0 ? 'warning' : 'success'" effect="light">
            {{ missingFieldCount(row) > 0 ? `${missingFieldCount(row)} 项` : '无' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column
        prop="updatedAt"
        label="更新时间"
        width="170"
        sortable="custom"
        :sort-order="sortOrder('UpdatedAsc', 'UpdatedDesc')"
      >
        <template #default="{ row }">{{ formatDateTime(row.updatedAt || row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="360" fixed="right">
        <template #default="{ row }">
          <el-button size="small" :icon="View" @click="openDetail(row)">详情</el-button>
          <el-button
            size="small"
            type="primary"
            plain
            :icon="Tickets"
            :loading="fieldEnrichmentLoadingKey === `${row.id}:auto`"
            @click="enqueueFieldEnrichment(row)"
          >
            AI补全
          </el-button>
          <el-button
            size="small"
            plain
            :loading="fieldEnrichmentLoadingKey === `${row.id}:prompt`"
            @click="openFieldPrompt(row)"
          >
            提示词
          </el-button>
          <el-button
            v-if="canApprove"
            size="small"
            type="success"
            plain
            :icon="Check"
            :disabled="row.linkStatus === 'Confirmed'"
            @click="openDecision(row, 'confirm')"
          >
            确认
          </el-button>
          <el-button
            v-if="canApprove"
            size="small"
            type="danger"
            plain
            :icon="Close"
            :disabled="row.linkStatus === 'Rejected'"
            @click="openDecision(row, 'reject')"
          >
            驳回
          </el-button>
        </template>
      </el-table-column>
    </DataTable>

    <el-pagination
      v-model:current-page="table.query.pageIndex"
      v-model:page-size="table.query.pageSize"
      :total="table.result.total"
      :page-sizes="[10, 20, 50, 100]"
      layout="total, sizes, prev, pager, next, jumper"
      class="table-pagination"
      @current-change="table.loadData"
      @size-change="table.loadData"
    />

    <el-drawer v-model="detailVisible" size="920px" title="生命周期关联详情">
      <template v-if="activeLink">
        <el-descriptions :column="2" border>
          <el-descriptions-item label="采购编号">{{ activeLink.projectCode || '-' }}</el-descriptions-item>
          <el-descriptions-item label="状态"><BidOpsStatusTag :value="activeLink.linkStatus" /></el-descriptions-item>
          <el-descriptions-item label="项目名称" :span="2">{{ activeLink.projectName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="分标编号">{{ lifecycleLotNo(activeLink) || '-' }}</el-descriptions-item>
          <el-descriptions-item label="分标名称">{{ lifecycleLotName(activeLink) || '-' }}</el-descriptions-item>
          <el-descriptions-item label="包件">{{ lifecyclePackageText(activeLink) }}</el-descriptions-item>
          <el-descriptions-item label="中标商家">{{ activeLink.supplierName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="中标金额">{{ formatMoney(activeLink.finalAwardAmount) }}</el-descriptions-item>
          <el-descriptions-item label="采购公告 Raw">
            <el-button link type="primary" :disabled="!activeLink.procurementRawNoticeId" @click="openRawNotice(activeLink.procurementRawNoticeId)">
              {{ activeLink.procurementRawNoticeId || '-' }}
            </el-button>
          </el-descriptions-item>
          <el-descriptions-item label="候选公示 Raw">
            <el-button link type="primary" :disabled="!activeLink.candidateRawNoticeId" @click="openRawNotice(activeLink.candidateRawNoticeId)">
              {{ activeLink.candidateRawNoticeId || '-' }}
            </el-button>
          </el-descriptions-item>
          <el-descriptions-item label="中标公告 Raw">
            <el-button link type="primary" :disabled="!activeLink.awardRawNoticeId" @click="openRawNotice(activeLink.awardRawNoticeId)">
              {{ activeLink.awardRawNoticeId || '-' }}
            </el-button>
          </el-descriptions-item>
          <el-descriptions-item label="正式包件">
            <el-button link type="primary" :disabled="!activeLink.tenderPackageId" @click="openTenderPackage(activeLink.tenderPackageId)">
              {{ activeLink.tenderPackageId || '-' }}
            </el-button>
          </el-descriptions-item>
          <el-descriptions-item label="确认时间">{{ formatDateTime(activeLink.confirmedAt) }}</el-descriptions-item>
          <el-descriptions-item label="备注">{{ activeLink.manualRemark || '-' }}</el-descriptions-item>
        </el-descriptions>
        <div class="detail-actions">
          <el-button
            type="primary"
            plain
            :icon="Tickets"
            :loading="fieldEnrichmentLoadingKey === `${activeLink.id}:auto`"
            @click="enqueueFieldEnrichment(activeLink)"
          >
            AI补全字段
          </el-button>
          <el-button
            plain
            :loading="fieldEnrichmentLoadingKey === `${activeLink.id}:prompt`"
            @click="openFieldPrompt(activeLink)"
          >
            人工提示词补全
          </el-button>
        </div>

        <section class="detail-section">
          <h2>采购公告证据</h2>
          <div v-if="!activeLink.procurementNotice" class="missing-procurement">
            <el-alert
              :title="activeLink.procurementNoticeMissingReason || '未匹配到采购公告 RawNotice。'"
              type="warning"
              show-icon
              :closable="false"
            />
            <div class="notice-actions">
              <el-button
                type="primary"
                plain
                :icon="Search"
                :loading="procurementSearchLoading && procurementSearchLink?.id === activeLink.id"
                :disabled="!activeLink.projectCode"
                @click="openProcurementSearch(activeLink)"
              >
                按采购编号搜索采购公告
              </el-button>
            </div>
          </div>
          <template v-else>
            <el-descriptions :column="2" border>
              <el-descriptions-item label="公告标题" :span="2">{{ activeLink.procurementNotice.title || '-' }}</el-descriptions-item>
              <el-descriptions-item label="RawNoticeId">
                <el-button link type="primary" @click="openNoticeRef(activeLink.procurementNotice)">
                  {{ activeLink.procurementNotice.rawNoticeId }}
                </el-button>
              </el-descriptions-item>
              <el-descriptions-item label="匹配方式">{{ noticeMatchLabel(activeLink.procurementNotice.matchSource) }}</el-descriptions-item>
              <el-descriptions-item label="发布时间">{{ formatDateTime(activeLink.procurementNotice.publishTime) }}</el-descriptions-item>
              <el-descriptions-item label="原文地址">
                <el-button link type="primary" :disabled="!activeLink.procurementNotice.detailUrl" @click="openExternalUrl(activeLink.procurementNotice.detailUrl)">
                  打开原公告
                </el-button>
              </el-descriptions-item>
            </el-descriptions>
            <RawAttachmentTable
              class="attachment-table"
              :attachments="activeLink.procurementAttachments || []"
              :raw-notice-id="activeLink.procurementNotice.rawNoticeId"
            />
          </template>
        </section>

        <section class="detail-section">
          <h2>中标公告原文与附件</h2>
          <el-descriptions v-if="activeLink.awardNotice" :column="2" border>
            <el-descriptions-item label="公告标题" :span="2">{{ activeLink.awardNotice.title || '-' }}</el-descriptions-item>
            <el-descriptions-item label="RawNoticeId">
              <el-button link type="primary" @click="openNoticeRef(activeLink.awardNotice)">
                {{ activeLink.awardNotice.rawNoticeId }}
              </el-button>
            </el-descriptions-item>
            <el-descriptions-item label="发布时间">{{ formatDateTime(activeLink.awardNotice.publishTime) }}</el-descriptions-item>
            <el-descriptions-item label="原文地址">
              <el-button link type="primary" :disabled="!activeLink.awardNotice.detailUrl" @click="openExternalUrl(activeLink.awardNotice.detailUrl)">
                打开原公告
              </el-button>
            </el-descriptions-item>
          </el-descriptions>
          <RawAttachmentTable
            class="attachment-table"
            :attachments="activeLink.awardAttachments || []"
            :raw-notice-id="activeLink.awardNotice?.rawNoticeId || activeLink.awardRawNoticeId"
          />
        </section>

        <section v-if="activeLink.candidateNotice" class="detail-section">
          <h2>候选公示附件</h2>
          <el-descriptions :column="2" border>
            <el-descriptions-item label="公告标题" :span="2">{{ activeLink.candidateNotice.title || '-' }}</el-descriptions-item>
            <el-descriptions-item label="RawNoticeId">
              <el-button link type="primary" @click="openNoticeRef(activeLink.candidateNotice)">
                {{ activeLink.candidateNotice.rawNoticeId }}
              </el-button>
            </el-descriptions-item>
            <el-descriptions-item label="发布时间">{{ formatDateTime(activeLink.candidateNotice.publishTime) }}</el-descriptions-item>
          </el-descriptions>
          <RawAttachmentTable
            class="attachment-table"
            :attachments="activeLink.candidateAttachments || []"
            :raw-notice-id="activeLink.candidateNotice.rawNoticeId"
          />
        </section>

        <section class="detail-section">
          <h2>字段补全建议</h2>
          <template v-if="selectedFieldEnrichment?.result">
            <el-alert
              class="dialog-tip"
              type="info"
              show-icon
              :closable="false"
              :title="selectedFieldEnrichment.result.summary || 'AI 字段补全已生成'"
            />
            <el-descriptions :column="3" border>
              <el-descriptions-item label="生成时间">{{ formatDateTime(selectedFieldEnrichment.generatedAtUtc) }}</el-descriptions-item>
              <el-descriptions-item label="整体置信度">{{ fieldConfidencePercent(selectedFieldEnrichment.result.confidence) }}%</el-descriptions-item>
              <el-descriptions-item label="人工提示词">{{ selectedFieldEnrichment.reviewerPromptProvided ? '是' : '否' }}</el-descriptions-item>
            </el-descriptions>
            <el-table
              class="attachment-table"
              :data="selectedFieldEnrichment.result.fields || []"
              border
              size="small"
              empty-text="AI 未找到可补全字段"
            >
              <el-table-column label="字段" prop="fieldName" width="130" />
              <el-table-column label="建议值" min-width="180" show-overflow-tooltip>
                <template #default="{ row }">
                  {{ row.value || row.numericValue || '-' }}
                </template>
              </el-table-column>
              <el-table-column label="来源" prop="sourceStage" width="120" />
              <el-table-column label="置信度" width="90">
                <template #default="{ row }">{{ fieldConfidencePercent(row.confidence) }}%</template>
              </el-table-column>
              <el-table-column label="证据" prop="evidenceText" min-width="260" show-overflow-tooltip />
              <el-table-column label="说明" prop="reason" min-width="220" show-overflow-tooltip />
            </el-table>
            <div v-if="selectedFieldEnrichment.result.conflicts?.length" class="tag-list enrichment-conflicts">
              <el-tag v-for="item in selectedFieldEnrichment.result.conflicts" :key="item" type="warning" effect="light">{{ item }}</el-tag>
            </div>
          </template>
          <span v-else class="muted-text">暂无字段补全建议，可点击“AI补全字段”生成。</span>
        </section>

        <section class="detail-section">
          <h2>匹配理由</h2>
          <div class="tag-list">
            <el-tag v-for="item in selectedReasons" :key="item" type="success" effect="light">{{ item }}</el-tag>
            <span v-if="!selectedReasons.length" class="muted-text">-</span>
          </div>
        </section>

        <section class="detail-section">
          <h2>缺失字段</h2>
          <div class="tag-list">
            <el-tag v-for="item in selectedMissingFields" :key="item" type="warning" effect="light">{{ item }}</el-tag>
            <span v-if="!selectedMissingFields.length" class="muted-text">-</span>
          </div>
        </section>

        <section class="detail-section">
          <h2>证据 JSON</h2>
          <pre>{{ selectedEvidenceJson || '-' }}</pre>
        </section>
      </template>
    </el-drawer>

    <el-dialog v-model="fieldPromptVisible" title="人工提示词补全" width="640px">
      <el-alert
        class="dialog-tip"
        type="info"
        show-icon
        :closable="false"
        title="用于自动补全失败后的定向重跑。提示词只应说明公开证据中的字段位置、表格含义或解析纠错要求。"
      />
      <el-form label-width="96px">
        <el-form-item label="提示词">
          <el-input
            v-model="fieldPromptForm.reviewerPrompt"
            type="textarea"
            :rows="6"
            maxlength="2000"
            show-word-limit
            placeholder="例如：采购公告附件中的“货物清单”表里，包号和分标名称在同一行，请用该表补全分标名称；金额优先找中标公告，没有则不要编造。"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="fieldPromptVisible = false">取消</el-button>
        <el-button
          type="primary"
          :loading="!!fieldPromptLink && fieldEnrichmentLoadingKey === `${fieldPromptLink.id}:prompt`"
          @click="enqueueFieldEnrichment(fieldPromptLink, fieldPromptForm.reviewerPrompt)"
        >
          提交补全任务
        </el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="procurementSearchVisible" title="搜索采购公告" width="960px">
      <el-alert
        v-if="procurementSearchLink"
        class="dialog-tip"
        type="info"
        :closable="false"
        show-icon
        :title="`按采购编号 ${procurementSearchLink.projectCode || '-'} 搜索国网公开采购/招标公告`"
      />
      <el-table
        v-loading="procurementSearchLoading"
        :data="procurementCandidates"
        border
        empty-text="未搜索到采购公告候选"
      >
        <el-table-column label="公告" min-width="320" show-overflow-tooltip>
          <template #default="{ row }">
            <div class="candidate-title">
              <strong>{{ row.title || '-' }}</strong>
              <span>{{ row.detailUrl }}</span>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="采购编号" width="130">
          <template #default="{ row }">
            <el-tag :type="row.isExactProjectCodeMatch ? 'success' : 'warning'" effect="light">
              {{ row.projectCode || '-' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="类型" width="150">
          <template #default="{ row }">{{ formatNoticeType(row.noticeType) || row.doctype }}</template>
        </el-table-column>
        <el-table-column label="发布单位" min-width="180" show-overflow-tooltip>
          <template #default="{ row }">{{ row.publishOrgName || '-' }}</template>
        </el-table-column>
        <el-table-column label="发布时间" width="150">
          <template #default="{ row }">{{ formatDateTime(row.publishTime) }}</template>
        </el-table-column>
        <el-table-column label="本地状态" width="150">
          <template #default="{ row }">
            <el-button v-if="row.existingRawNoticeId" link type="primary" @click="openRawNotice(row.existingRawNoticeId)">
              {{ row.existingRawNoticeId }}
            </el-button>
            <BidOpsStatusTag
              v-if="row.existingRawNoticeStatus !== null && row.existingRawNoticeStatus !== undefined"
              :value="row.existingRawNoticeStatus"
              kind="rawNotice"
            />
            <span v-if="!row.existingRawNoticeId" class="muted-text">未导入</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="190" fixed="right">
          <template #default="{ row }">
            <el-button size="small" :icon="LinkIcon" @click="openExternalUrl(row.detailUrl)">原页</el-button>
            <el-button
              size="small"
              type="primary"
              :icon="row.existingRawNoticeId ? LinkIcon : Upload"
              :loading="procurementImportingKey === candidateKey(row)"
              @click="importProcurementCandidate(row)"
            >
              {{ row.existingRawNoticeId ? '关联' : '导入' }}
            </el-button>
          </template>
        </el-table-column>
      </el-table>
      <template #footer>
        <el-button :icon="Refresh" :loading="procurementSearchLoading" @click="searchProcurementCandidates">重新搜索</el-button>
        <el-button @click="procurementSearchVisible = false">关闭</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="decisionVisible" :title="decisionTitle" width="560px">
      <el-form label-width="96px">
        <el-form-item v-if="decisionMode === 'confirm'" label="中标金额">
          <el-input-number
            v-model="decisionForm.finalAwardAmount"
            :min="0"
            :precision="2"
            :step="1000"
            controls-position="right"
            style="width: 220px"
          />
        </el-form-item>
        <el-form-item v-if="decisionMode === 'confirm'" label="金额来源">
          <el-input v-model="decisionForm.finalAwardAmountSource" clearable maxlength="128" placeholder="AwardNotice / Manual" />
        </el-form-item>
        <el-form-item :label="decisionMode === 'confirm' ? '确认备注' : '驳回原因'">
          <el-input
            v-model="decisionForm.remark"
            type="textarea"
            :rows="4"
            maxlength="1000"
            show-word-limit
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="decisionVisible = false">取消</el-button>
        <el-button
          :type="decisionMode === 'confirm' ? 'success' : 'danger'"
          :loading="decisionLoading"
          @click="submitDecision"
        >
          {{ decisionMode === 'confirm' ? '确认关联' : '驳回关联' }}
        </el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.table-pagination {
  justify-content: flex-end;
  margin-top: 14px;
}

.main-cell {
  display: grid;
  gap: 4px;
  min-width: 0;
}

.main-cell strong {
  overflow: hidden;
  color: #1f2d3d;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.main-cell span,
.muted-text {
  color: #687385;
  font-size: 13px;
}

.missing-procurement-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.detail-section {
  margin-top: 18px;
}

.detail-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 12px;
}

.missing-procurement {
  display: grid;
  gap: 12px;
}

.notice-actions {
  display: flex;
  justify-content: flex-start;
}

.attachment-table {
  margin-top: 12px;
}

.detail-section h2 {
  margin: 0 0 10px;
  color: #1f2d3d;
  font-size: 15px;
  font-weight: 600;
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.enrichment-conflicts {
  margin-top: 12px;
}

.dialog-tip {
  margin-bottom: 12px;
}

.candidate-title {
  display: grid;
  gap: 4px;
  min-width: 0;
}

.candidate-title strong,
.candidate-title span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.candidate-title span {
  color: #687385;
  font-size: 12px;
}

pre {
  max-height: 360px;
  padding: 12px;
  overflow: auto;
  border: 1px solid #e4e7ed;
  border-radius: 6px;
  background: #f8fafc;
  color: #344054;
  font-size: 12px;
  line-height: 1.55;
  white-space: pre-wrap;
  word-break: break-word;
}

</style>
