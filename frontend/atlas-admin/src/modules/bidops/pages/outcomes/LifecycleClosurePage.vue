<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Bottom, Check, Close, Delete, Edit, Link as LinkIcon, Refresh, Search, Tickets, Top, Upload, View } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { lifecycleApi } from '@/api/bidops/lifecycle.api'
import { rawNoticesApi } from '@/api/bidops/rawNotices.api'
import { backgroundJobsApi } from '@/api/operations/backgroundJobs.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { usePermission } from '@/shared/composables/usePermission'
import { formatDateTime } from '@/shared/utils/date'
import { formatMoney } from '@/shared/utils/money'
import type { BackgroundJobDetailDto } from '@/modules/operations/types'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import RawAttachmentTable from '../../components/RawAttachmentTable.vue'
import { BIDOPS_PERMISSIONS } from '../../constants'
import type { AmountCandidateDto, AmountCandidateOperationResultDto, LifecycleNoticeRefDto, LifecyclePackageLinkDto, LifecycleProcurementNoticeCandidateDto } from '../../types'
import {
  formatAmountCandidateType,
  formatLifecycleAmountSource,
  formatCommonStatus,
  formatNoticeType,
  lifecycleAmountSourceOptions,
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

interface NoticeContextItem {
  key: string
  notice: LifecycleNoticeRefDto | null
  rawNoticeId: string
  links: LifecyclePackageLinkDto[]
}

interface NoticeContextSummary {
  primary: NoticeContextItem | null
  items: NoticeContextItem[]
  missingCount: number
}

type SortOrder = 'ascending' | 'descending' | null
type NoticePromptKind = 'award' | 'procurement'

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
const projectCodeVisible = ref(false)
const projectCodeLoading = ref(false)
const projectCodeLink = ref<LifecyclePackageLinkDto | null>(null)
const procurementSearchVisible = ref(false)
const procurementSearchLoading = ref(false)
const procurementImportingKey = ref('')
const procurementSearchLink = ref<LifecyclePackageLinkDto | null>(null)
const procurementSearchApplyToRelatedLinks = ref(false)
const procurementCandidates = ref<LifecycleProcurementNoticeCandidateDto[]>([])
const amountCandidateLoading = ref(false)
const amountCandidateActionLoadingKey = ref('')
const outcomeReparseLoadingKey = ref('')
const procurementReparseLoadingKey = ref('')
const noticePromptVisible = ref(false)
const noticePromptKind = ref<NoticePromptKind>('award')
const noticePromptItem = ref<NoticeContextItem | null>(null)
const noticePromptLoadingKey = ref('')
const selectedRows = ref<LifecyclePackageLinkDto[]>([])
const batchReviewLoading = ref(false)
const batchAmountClearLoading = ref(false)
const autoCollectLoading = ref(false)
const pageTopRef = ref<HTMLElement | null>(null)
const pageBottomRef = ref<HTMLElement | null>(null)
const promptTaskTimers = new Map<string, number>()

const decisionForm = reactive({
  remark: '',
  finalAwardAmount: null as number | null,
  finalAwardAmountSource: '',
})
const projectCodeForm = reactive({
  projectCode: '',
  remark: '',
  applyToRelatedLinks: true,
  clearProcurementNotice: true,
})
const noticePromptForm = reactive({
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
    linkStatus: '',
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

const selectedReasons = computed(() => parseJsonTextList(activeLink.value?.matchReasonsJson).map(lifecycleReviewTextLabel))
const selectedMissingFields = computed(() => parseJsonTextList(activeLink.value?.missingFieldsJson).map(lifecycleReviewTextLabel))
const selectedEvidenceJson = computed(() => prettyJson(activeLink.value?.evidenceJson))
const selectedFieldEnrichment = computed(() => fieldEnrichment(activeLink.value))
const decisionTitle = computed(() => (decisionMode.value === 'confirm' ? '确认生命周期关联' : '驳回生命周期关联'))
const projectCodeDialogTitle = computed(() => `修改项目编号${projectCodeLink.value?.projectCode ? `（当前 ${projectCodeLink.value.projectCode}）` : ''}`)
const noticePromptTitle = computed(() =>
  noticePromptKind.value === 'award'
    ? '中标公告 AI 提示词辅助解析'
    : '前置公告 AI 提示词辅助解析',
)
const noticePromptHelpText = computed(() =>
  noticePromptKind.value === 'award'
    ? '按当前中标公告重新抽取全部中标/候选厂家明细，并在后台任务成功后刷新闭环列表。'
    : '按当前前置公告重新结构化解析包件、需求、金额等暂存证据，并在后台任务成功后刷新闭环列表。',
)
const noticePromptPlaceholder = computed(() =>
  noticePromptKind.value === 'award'
    ? '例如：附件 PDF 的“成交结果明细”表中，分标编号、分标名称、包号、成交人和成交金额在同一行；金额单位按表头或附近“万元”处理。'
    : '例如：前置公告附件的“货物清单/需求一览表”中，分标名称和包号对应包级预算金额；优先按分标名称+包号匹配金额，不要把分标总金额当成包金额。',
)
const noticePromptSubmitLoading = computed(() => {
  const rawNoticeId = noticePromptRawNoticeId(noticePromptItem.value)
  return !!rawNoticeId && noticePromptLoadingKey.value === noticePromptTaskKey(noticePromptKind.value, rawNoticeId)
})
const decisionAmountSourceOptions = computed(() => {
  const value = decisionForm.finalAwardAmountSource.trim()
  if (!value || lifecycleAmountSourceOptions.some((item) => item.value === value)) return lifecycleAmountSourceOptions
  return [{ label: '其他来源', value }, ...lifecycleAmountSourceOptions]
})
const selectedReviewRows = computed(() => selectedRows.value.filter(canSelectLifecycleRow))
const selectedRowsWithFinalAmount = computed(() =>
  selectedReviewRows.value.filter((row) => (row.finalAwardAmount !== null && row.finalAwardAmount !== undefined) || !!row.finalAwardAmountSource),
)
const awardNoticeContext = computed(() => buildNoticeContext('award'))
const procurementNoticeContext = computed(() => buildNoticeContext('procurement'))
const closureProjectCodeEditTarget = computed(() => {
  const links = awardNoticeContext.value.primary?.links?.length
    ? awardNoticeContext.value.primary.links
    : table.result.items
  return links.find((link) => canOperateLifecycleRow(link) && link.linkStatus !== 'Rejected') || links.find(canOperateLifecycleRow) || null
})
const closureCurrentProjectCode = computed(() => {
  const links = awardNoticeContext.value.primary?.links?.length
    ? awardNoticeContext.value.primary.links
    : table.result.items
  const projectCodes = uniqueDisplayTexts(links.map((item) => item.projectCode))
  if (projectCodes.length === 1) return projectCodes[0]
  if (projectCodes.length > 1) return `多个：${projectCodes.join(' / ')}`
  return '未识别'
})
const procurementSearchTarget = computed(() =>
  table.result.items.find((item) => canOperateLifecycleRow(item) && !item.procurementNotice && !!item.projectCode) ||
  table.result.items.find((item) => canOperateLifecycleRow(item) && !!item.projectCode) ||
  null,
)
const procurementSearchDialogTitle = computed(() => {
  const code = procurementSearchLink.value?.projectCode || '-'
  return procurementSearchApplyToRelatedLinks.value
    ? `按招标编号/采购编号 ${code} 重新匹配前置公告`
    : `按招标编号/采购编号 ${code} 搜索国网公开前置公告`
})
const procurementSearchDialogDescription = computed(() =>
  procurementSearchApplyToRelatedLinks.value
    ? '选择已导入候选后，会同步替换同一中标/成交公告下当前指向同一个错误前置 RawNotice 的待审核闭环行。'
    : '',
)
const closureProjectSummary = computed(() => {
  const projectCodes = uniqueDisplayTexts(table.result.items.map((item) => item.projectCode))
  const projectNames = uniqueDisplayTexts(table.result.items.map((item) => item.projectName))
  if (projectCodes.length === 1 && projectNames.length === 1) return `${projectCodes[0]} · ${projectNames[0]}`
  if (projectCodes.length === 1) return projectCodes[0]
  if (projectNames.length === 1) return projectNames[0]
  if (table.result.items.length > 0) return `当前筛选包含 ${projectCodes.length || table.result.items.length} 个项目`
  return '暂无闭环数据'
})
const activeRawNoticeId = computed(() => firstDisplayText(table.query.rawNoticeId, route.query.rawNoticeId))
const amountCandidateGroups = computed(() => {
  const rows = activeLink.value?.amountCandidates || []
  const order = ['Selected', 'Recommended', 'Candidate', 'Unresolved', 'Rejected']
  return order
    .map((status) => ({
      status,
      rows: rows.filter((row) => row.status === status),
    }))
    .filter((group) => group.rows.length > 0)
})

const amountCandidateTypeOptions = [
  { label: '中标金额', value: 'winning_amount' },
  { label: '成交金额', value: 'deal_amount' },
  { label: '中标价', value: 'winning_price' },
  { label: '成交价', value: 'deal_price' },
  { label: '报价金额', value: 'quote_amount' },
  { label: '投标报价', value: 'bid_quote' },
  { label: '响应报价', value: 'response_quote' },
  { label: '最终报价', value: 'final_quote' },
  { label: '总报价', value: 'total_quote' },
  { label: '预算金额', value: 'budget_amount' },
  { label: '最高限价', value: 'ceiling_price' },
  { label: '代理服务费', value: 'agency_fee' },
  { label: '保证金', value: 'deposit' },
  { label: '单价', value: 'unit_price' },
  { label: '费率', value: 'rate' },
  { label: '折扣率', value: 'discount_rate' },
  { label: '下浮率', value: 'reduction_rate' },
  { label: '未知金额', value: 'unknown' },
]
const finalAmountCandidateTypes = new Set([
  'winning_amount',
  'deal_amount',
  'winning_price',
  'deal_price',
  'quote_amount',
  'bid_quote',
  'response_quote',
  'final_quote',
  'total_quote',
])

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

function canSelectLifecycleRow(row: LifecyclePackageLinkDto) {
  return row.linkStatus === 'Suggested'
}

function isStatusOnlyLifecycleRow(row?: LifecyclePackageLinkDto | null) {
  return row?.linkStatus === 'StatusOnly'
}

function canOperateLifecycleRow(row?: LifecyclePackageLinkDto | null) {
  return !!row && !isStatusOnlyLifecycleRow(row)
}

function handleSelectionChange(rows: LifecyclePackageLinkDto[]) {
  selectedRows.value = rows
}

function scrollToPageTop() {
  pageTopRef.value?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

function scrollToPageBottom() {
  pageBottomRef.value?.scrollIntoView({ behavior: 'smooth', block: 'end' })
}

async function openDetail(row: LifecyclePackageLinkDto) {
  activeLink.value = row
  detailVisible.value = true
  if (isStatusOnlyLifecycleRow(row)) return
  await refreshAmountCandidates(row)
}

function openDecision(row: LifecyclePackageLinkDto, mode: 'confirm' | 'reject') {
  if (!canOperateLifecycleRow(row)) return
  decisionLink.value = row
  decisionMode.value = mode
  decisionForm.remark = row.manualRemark || ''
  decisionForm.finalAwardAmount = row.finalAwardAmount ?? null
  decisionForm.finalAwardAmountSource = row.finalAwardAmountSource || ''
  decisionVisible.value = true
}

function openProjectCodeEdit(row?: LifecyclePackageLinkDto | null) {
  const link = row || activeLink.value
  if (!link || isStatusOnlyLifecycleRow(link)) return

  projectCodeLink.value = link
  projectCodeForm.projectCode = link.projectCode || ''
  projectCodeForm.remark = ''
  projectCodeForm.applyToRelatedLinks = true
  projectCodeForm.clearProcurementNotice = true
  projectCodeVisible.value = true
}

function openClosureProjectCodeEdit() {
  if (awardNoticeContext.value.items.length > 1) {
    ElMessage.warning('当前列表包含多个中标公告，请先按 RawNoticeId 筛选到单次闭环')
    return
  }

  const link = closureProjectCodeEditTarget.value
  if (!link) {
    ElMessage.warning('当前闭环公告没有可修改的明细')
    return
  }

  openProjectCodeEdit(link)
  projectCodeForm.applyToRelatedLinks = true
  projectCodeForm.clearProcurementNotice = true
}

function closeProjectCodeEdit() {
  projectCodeVisible.value = false
}

async function submitProjectCodeEdit() {
  const link = projectCodeLink.value
  const projectCode = projectCodeForm.projectCode.trim()
  if (!link) return
  if (!projectCode) {
    ElMessage.warning('请填写项目编号')
    return
  }

  projectCodeLoading.value = true
  try {
    const oldQueryProjectCode = normalizeProcurementSearchCode(table.query.projectCode)
    const oldLinkProjectCode = normalizeProcurementSearchCode(link.projectCode)
    const targetAwardRawNoticeId = link.awardRawNoticeId
    const result = await lifecycleApi.updateProjectCode(link.id, {
      projectCode,
      remark: projectCodeForm.remark.trim() || null,
      applyToRelatedLinks: projectCodeForm.applyToRelatedLinks,
      clearProcurementNotice: projectCodeForm.clearProcurementNotice,
    })
    replaceRow(result.link)
    if (activeLink.value?.id === result.link.id) activeLink.value = result.link
    applyProjectCodeToVisibleRows(targetAwardRawNoticeId, result.projectCode, projectCodeForm.clearProcurementNotice)
    if (oldQueryProjectCode && oldQueryProjectCode === oldLinkProjectCode) {
      table.query.projectCode = result.projectCode
    }
    await table.loadData()
    if (activeLink.value) {
      const activeUpdated = table.result.items.find((item) => item.id === activeLink.value?.id)
      if (activeUpdated) activeLink.value = activeUpdated
    }
    ElMessage.success(result.message || `项目编号已更新为 ${result.projectCode}`)
    projectCodeVisible.value = false
  } finally {
    projectCodeLoading.value = false
  }
}

function applyProjectCodeToVisibleRows(
  awardRawNoticeId: LifecyclePackageLinkDto['awardRawNoticeId'],
  projectCode: string,
  clearProcurementNotice: boolean,
) {
  for (const row of table.result.items) {
    if (awardRawNoticeId && row.awardRawNoticeId !== awardRawNoticeId) continue
    row.projectCode = projectCode
    row.requiresManualReview = true
    if (clearProcurementNotice) {
      row.procurementRawNoticeId = null
      row.procurementNotice = null
      row.procurementNoticeMissingReason = `未匹配到前置公告 RawNotice；请先采集或导入招标编号/采购编号 ${projectCode} 对应的前置公告。`
    }
  }
}

function closeDecision() {
  decisionVisible.value = false
}

function handleDecisionBeforeClose(done: () => void) {
  done()
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

async function batchConfirmSelected() {
  const rows = selectedReviewRows.value
  if (rows.length === 0) {
    ElMessage.warning('请先选择待确认的闭环明细')
    return
  }

  try {
    await ElMessageBox.confirm(
      `将批量确认当前选中的 ${rows.length} 条待确认闭环明细。每行会使用当前展示的中标金额和金额来源。`,
      '批量确认闭环明细',
      {
        type: 'warning',
        confirmButtonText: '批量确认',
      },
    )
  } catch {
    return
  }

  await runBatchDecision(rows, 'confirm', '批量确认')
}

async function batchRejectSelected() {
  const rows = selectedReviewRows.value
  if (rows.length === 0) {
    ElMessage.warning('请先选择待驳回的闭环明细')
    return
  }

  let result: { value: string }
  try {
    result = await ElMessageBox.prompt(
      `将批量驳回当前选中的 ${rows.length} 条待确认闭环明细，请填写驳回原因。`,
      '批量驳回闭环明细',
      {
        inputType: 'textarea',
        inputPlaceholder: '请输入驳回原因',
        confirmButtonText: '批量驳回',
        inputValidator: (value) => !!String(value || '').trim() || '请填写驳回原因',
      },
    ) as { value: string }
  } catch {
    return
  }

  await runBatchDecision(rows, 'reject', result.value.trim())
}

async function batchClearFinalAwardAmounts() {
  if (selectedReviewRows.value.length === 0) {
    ElMessage.warning('请先选择待清空金额的闭环明细')
    return
  }

  const rows = selectedRowsWithFinalAmount.value
  if (rows.length === 0) {
    ElMessage.warning('当前选中明细没有可清空的中标金额')
    return
  }

  try {
    await ElMessageBox.confirm(
      `将清空当前选中 ${rows.length} 条明细的中标金额，并撤销已采用金额候选。该操作适用于误把服务费当作中标金额的情况，不会确认或驳回闭环明细。`,
      '批量清空中标金额',
      {
        type: 'warning',
        confirmButtonText: '清空金额',
      },
    )
  } catch {
    return
  }

  batchAmountClearLoading.value = true
  try {
    const result = await lifecycleApi.clearFinalAwardAmounts({
      linkIds: rows.map((row) => row.id),
      reason: '批量清空误用服务费或非最终中标金额',
    })
    for (const item of result.items) {
      const row = table.result.items.find((candidate) => String(candidate.id) === String(item.linkId))
      if (!row || !item.succeeded) continue
      row.finalAwardAmount = item.finalAwardAmount ?? null
      row.finalAwardAmountSource = item.finalAwardAmountSource || 'Missing'
      row.requiresManualReview = true
      row.updatedAt = item.linkUpdatedAt || row.updatedAt
      if (activeLink.value?.id === row.id) activeLink.value = { ...row, amountCandidates: activeLink.value.amountCandidates }
    }

    selectedRows.value = []
    await table.loadData()
    if (activeLink.value) {
      const activeUpdated = table.result.items.find((item) => item.id === activeLink.value?.id)
      if (activeUpdated) {
        activeLink.value = activeUpdated
        await refreshAmountCandidates(activeUpdated)
      }
    }

    if (result.failedCount > 0) {
      ElMessage.warning(`批量清空完成：成功 ${result.succeededCount} 条，跳过 ${result.skippedCount} 条，失败 ${result.failedCount} 条`)
    } else {
      ElMessage.success(`批量清空完成：成功 ${result.succeededCount} 条，跳过 ${result.skippedCount} 条`)
    }
  } finally {
    batchAmountClearLoading.value = false
  }
}

async function runBatchDecision(rows: LifecyclePackageLinkDto[], mode: 'confirm' | 'reject', remark: string) {
  batchReviewLoading.value = true
  try {
    const result = await lifecycleApi.batchReviewLinks({
      linkIds: rows.map((row) => row.id),
      decision: mode === 'confirm' ? 'Confirm' : 'Reject',
      remark,
      requiresManualReview: false,
    })
    for (const item of result.items) {
      if (!item.succeeded || !item.link) continue
      replaceRow(item.link)
      if (activeLink.value?.id === item.link.id) activeLink.value = item.link
    }

    selectedRows.value = []
    await table.loadData()
    if (result.failedCount > 0 || result.skippedCount > 0) {
      ElMessage.warning(`批量审核完成：成功 ${result.succeededCount} 条，跳过 ${result.skippedCount} 条，失败 ${result.failedCount} 条`)
    } else {
      ElMessage.success(`批量审核完成：成功 ${result.succeededCount} 条`)
    }
  } finally {
    batchReviewLoading.value = false
  }
}

async function autoCollectAndReviewProcurementNotice() {
  const rawNoticeId = normalizeNumericId(awardNoticeContext.value.primary?.rawNoticeId || activeRawNoticeId.value)
  if (!rawNoticeId) {
    ElMessage.warning('请先筛选到单个中标/成交公告')
    return
  }

  autoCollectLoading.value = true
  try {
    const result = await lifecycleApi.autoCollectProcurementNotice(rawNoticeId, {
      autoReview: true,
      forceRefresh: false,
    })
    await table.loadData()
    if (activeLink.value) {
      const activeUpdated = table.result.items.find((item) => item.id === activeLink.value?.id)
      if (activeUpdated) activeLink.value = activeUpdated
    }

    if (result.failedCount > 0 || result.skippedCount > 0) {
      ElMessage.warning(result.message || `自动处理完成：关联 ${result.updatedLinkCount} 条，自动审核 ${result.autoReview?.succeededCount || 0} 条`)
    } else {
      ElMessage.success(result.message || `自动处理完成：关联 ${result.updatedLinkCount} 条，自动审核 ${result.autoReview?.succeededCount || 0} 条`)
    }
  } finally {
    autoCollectLoading.value = false
  }
}

function replaceRow(updated: LifecyclePackageLinkDto) {
  const index = table.result.items.findIndex((item) => item.id === updated.id)
  if (index >= 0) table.result.items.splice(index, 1, updated)
}

async function refreshAmountCandidates(row?: LifecyclePackageLinkDto | null) {
  const link = row || activeLink.value
  if (!link) return
  if (isStatusOnlyLifecycleRow(link)) {
    link.amountCandidates = []
    if (activeLink.value?.id === link.id) activeLink.value = { ...link, amountCandidates: [] }
    return
  }

  amountCandidateLoading.value = true
  try {
    const candidates = await lifecycleApi.amountCandidates(link.id)
    link.amountCandidates = candidates
    if (activeLink.value?.id === link.id) activeLink.value = { ...link, amountCandidates: candidates }
    replaceRow(link)
  } finally {
    amountCandidateLoading.value = false
  }
}

async function selectAmountCandidate(row: AmountCandidateDto) {
  const link = activeLink.value
  if (!link) return

  amountCandidateActionLoadingKey.value = amountCandidateActionKey(row, 'select')
  try {
    const result = await lifecycleApi.selectAmountCandidate(link.id, row.id)
    applyAmountCandidateOperation(link, result)
    ElMessage.success('已采用金额候选')
  } finally {
    amountCandidateActionLoadingKey.value = ''
  }
}

async function rejectAmountCandidate(row: AmountCandidateDto) {
  const link = activeLink.value
  if (!link) return

  let result: { value: string }
  try {
    result = await ElMessageBox.prompt('请填写排除原因。该候选仍会保留在证据链中。', '排除金额候选', {
      inputType: 'textarea',
      inputPlaceholder: '例如：这是预算金额，不是中标金额',
      confirmButtonText: '排除',
      inputValidator: (value) => !!String(value || '').trim() || '请填写排除原因',
    }) as { value: string }
  } catch {
    return
  }

  amountCandidateActionLoadingKey.value = amountCandidateActionKey(row, 'reject')
  try {
    const operation = await lifecycleApi.rejectAmountCandidate(link.id, row.id, { reason: result.value.trim() })
    applyAmountCandidateOperation(link, operation)
    ElMessage.success('金额候选已排除')
  } finally {
    amountCandidateActionLoadingKey.value = ''
  }
}

async function restoreAmountCandidate(row: AmountCandidateDto) {
  const link = activeLink.value
  if (!link) return

  amountCandidateActionLoadingKey.value = amountCandidateActionKey(row, 'restore')
  try {
    const result = await lifecycleApi.restoreAmountCandidate(link.id, row.id)
    applyAmountCandidateOperation(link, result)
    ElMessage.success('金额候选已恢复')
  } finally {
    amountCandidateActionLoadingKey.value = ''
  }
}

async function markAmountCandidateType(row: AmountCandidateDto, amountType: string) {
  const link = activeLink.value
  if (!link || !amountType || amountType === row.amountType) return

  amountCandidateActionLoadingKey.value = amountCandidateActionKey(row, `type:${amountType}`)
  try {
    const result = await lifecycleApi.markAmountCandidateType(link.id, row.id, { amountType })
    applyAmountCandidateOperation(link, result)
    ElMessage.success('金额类型已更新')
  } finally {
    amountCandidateActionLoadingKey.value = ''
  }
}

function handleAmountCandidateTypeCommand(row: AmountCandidateDto, command: unknown) {
  void markAmountCandidateType(row, String(command || ''))
}

function applyAmountCandidateOperation(link: LifecyclePackageLinkDto, result: AmountCandidateOperationResultDto) {
  link.amountCandidates = result.candidates || []
  link.finalAwardAmount = result.finalAwardAmount ?? null
  link.finalAwardAmountSource = result.finalAwardAmountSource || ''
  link.updatedAt = result.linkUpdatedAt || link.updatedAt
  if (activeLink.value?.id === link.id) activeLink.value = { ...link }
  replaceRow(link)
}

function amountCandidateActionKey(row: AmountCandidateDto, action: string) {
  return `${row.id}:${action}`
}

async function enqueueOutcomeSupplierReparse(item?: NoticeContextItem | null) {
  const rawNoticeId = firstDisplayText(item?.rawNoticeId, item?.notice?.rawNoticeId)
  if (!rawNoticeId) return

  await ElMessageBox.confirm(
    '将重新抽取该中标公告下的全部中标/候选厂家明细，并在任务完成后自动刷新闭环关联；这不是行级操作。',
    '确认重抽并刷新闭环',
    {
      type: 'warning',
      confirmButtonText: '提交任务',
    },
  )

  outcomeReparseLoadingKey.value = rawNoticeId
  try {
    const job = await lifecycleApi.enqueueOutcomeSupplierReparse(rawNoticeId)
    ElMessage.success(job.alreadyExists ? `重抽任务已存在：${job.jobId}` : `已提交重抽任务：${job.jobId}`)
    startPromptJobMonitor(job.jobId, 'award', rawNoticeId)
  } finally {
    outcomeReparseLoadingKey.value = ''
  }
}

async function enqueueProcurementNoticeReparse(item?: NoticeContextItem | null) {
  const rawNoticeId = noticePromptRawNoticeId(item)
  if (!rawNoticeId) {
    ElMessage.warning('当前没有可重新解析的前置公告 RawNotice')
    return
  }

  await ElMessageBox.confirm(
    '将重新抽取该前置公告的包件、需求和金额暂存证据，并在结构化解析完成后刷新闭环列表；已入库或已审核通过的公告会被系统拦截。',
    '确认重新解析前置公告',
    {
      type: 'warning',
      confirmButtonText: '提交任务',
    },
  )

  procurementReparseLoadingKey.value = rawNoticeId
  try {
    const job = await rawNoticesApi.reparse(rawNoticeId, { reason: 'Lifecycle closure procurement notice reparse' })
    ElMessage.success(job.alreadyExists ? `前置公告重解析任务已存在：${job.jobId}` : `已提交前置公告重解析任务：${job.jobId}`)
    startPromptJobMonitor(job.jobId, 'procurement', rawNoticeId)
  } finally {
    procurementReparseLoadingKey.value = ''
  }
}

function openNoticePrompt(kind: NoticePromptKind, item?: NoticeContextItem | null) {
  const rawNoticeId = noticePromptRawNoticeId(item)
  if (!rawNoticeId) {
    ElMessage.warning(kind === 'award' ? '当前没有可解析的中标公告 RawNotice' : '当前没有可解析的前置公告 RawNotice')
    return
  }

  noticePromptKind.value = kind
  noticePromptItem.value = item || null
  noticePromptForm.reviewerPrompt = ''
  noticePromptVisible.value = true
}

async function submitNoticePrompt() {
  const item = noticePromptItem.value
  const rawNoticeId = noticePromptRawNoticeId(item)
  const reviewerPrompt = noticePromptForm.reviewerPrompt.trim()
  if (!rawNoticeId) return
  if (!reviewerPrompt) {
    ElMessage.warning('请填写 AI 提示词')
    return
  }

  const kind = noticePromptKind.value
  const key = noticePromptTaskKey(kind, rawNoticeId)
  noticePromptLoadingKey.value = key
  try {
    const jobs = kind === 'award'
      ? [await lifecycleApi.enqueueOutcomeSupplierReparse(rawNoticeId, { reviewerPrompt })]
      : [await enqueueProcurementNoticePromptJob(rawNoticeId, reviewerPrompt)]
    if (!jobs.length) return

    const jobIds = jobs.map((job) => firstDisplayText(job.jobId)).filter(Boolean)
    const alreadyExistsCount = jobs.filter((job) => job.alreadyExists).length
    ElMessage.success(
      jobs.length === 1
        ? (alreadyExistsCount ? `提示词解析任务已存在：${jobIds[0]}` : `已提交提示词解析任务：${jobIds[0]}`)
        : `已提交 ${jobs.length} 个提示词解析任务${alreadyExistsCount ? `，其中 ${alreadyExistsCount} 个已在队列中` : ''}`,
    )
    noticePromptVisible.value = false
    noticePromptForm.reviewerPrompt = ''
    startPromptJobGroupMonitor(jobIds, kind, rawNoticeId)
  } finally {
    noticePromptLoadingKey.value = ''
  }
}

async function enqueueProcurementNoticePromptJob(rawNoticeId: string, reviewerPrompt: string) {
  return rawNoticesApi.reparse(rawNoticeId, {
    reason: 'Lifecycle closure procurement notice reviewer-prompt reparse',
    prompt: reviewerPrompt,
  })
}

function noticePromptRawNoticeId(item?: NoticeContextItem | null) {
  return firstDisplayText(item?.rawNoticeId, item?.notice?.rawNoticeId)
}

function noticePromptItemKey(kind: NoticePromptKind, item?: NoticeContextItem | null) {
  const rawNoticeId = noticePromptRawNoticeId(item)
  return rawNoticeId ? noticePromptTaskKey(kind, rawNoticeId) : ''
}

function noticePromptTaskKey(kind: NoticePromptKind, rawNoticeId: string) {
  return `${kind}:${rawNoticeId}`
}

function startPromptJobMonitor(jobId: unknown, kind: NoticePromptKind, rawNoticeId: string) {
  const id = firstDisplayText(jobId)
  if (!id) return

  startPromptJobGroupMonitor([id], kind, rawNoticeId)
}

function startPromptJobGroupMonitor(jobIds: string[], kind: NoticePromptKind, rawNoticeId: string) {
  const ids = Array.from(new Set(jobIds.map((jobId) => firstDisplayText(jobId)).filter(Boolean)))
  if (!ids.length) return

  const key = noticePromptTaskKey(kind, rawNoticeId)
  clearPromptJobMonitor(key)
  pollPromptJobs(key, ids, kind, 0)
}

function pollPromptJobs(key: string, jobIds: string[], kind: NoticePromptKind, attempt: number) {
  const delay = attempt === 0 ? 1200 : 3000
  const timer = window.setTimeout(async () => {
    try {
      const results = await Promise.all(jobIds.map(async (jobId) => {
        try {
          return { jobId, job: await backgroundJobsApi.get(jobId) }
        } catch {
          return { jobId, job: null }
        }
      }))
      const jobs = results.map((result) => result.job).filter((job): job is BackgroundJobDetailDto => !!job)
      if (jobs.length === jobIds.length && jobs.every(isSucceededJob)) {
        const childJobIds = kind === 'procurement' ? extractStructuredParseJobIds(jobs, jobIds) : []
        if (childJobIds.length) {
          pollPromptJobs(key, childJobIds, kind, 0)
          return
        }

        clearPromptJobMonitor(key)
        await table.loadData()
        ElMessage.success(`${noticePromptKindLabel(kind)}解析任务已完成，列表已刷新`)
        return
      }

      if (jobs.length === jobIds.length && jobs.every((job) => isSucceededJob(job) || isTerminalJob(job))) {
        clearPromptJobMonitor(key)
        await table.loadData()
        const succeeded = jobs.filter(isSucceededJob).length
        ElMessage.warning(`${noticePromptKindLabel(kind)}解析任务完成 ${succeeded}/${jobs.length} 个，列表已刷新，请查看失败任务`)
        return
      }
    } catch {
      if (attempt >= 120) {
        clearPromptJobMonitor(key)
        ElMessage.warning('无法确认解析任务状态，请查看后台任务')
        return
      }
    }

    if (attempt >= 120) {
      clearPromptJobMonitor(key)
      ElMessage.warning('解析任务仍未完成，请稍后查看后台任务')
      return
    }

    pollPromptJobs(key, jobIds, kind, attempt + 1)
  }, delay)
  promptTaskTimers.set(key, timer)
}

function clearPromptJobMonitor(key: string) {
  const timer = promptTaskTimers.get(key)
  if (timer !== undefined) window.clearTimeout(timer)
  promptTaskTimers.delete(key)
}

function isSucceededJob(job: BackgroundJobDetailDto) {
  const status = normalizeJobStatus(job)
  return status === '2' || status === 'succeeded'
}

function isTerminalJob(job: BackgroundJobDetailDto) {
  const status = normalizeJobStatus(job)
  if (status === '4' || status === '5' || status === 'dead' || status === 'canceled') return true
  if (status === '3' || status === 'failed') {
    const attempts = Number(job.attemptCount || 0)
    const maxAttempts = Number(job.maxAttempts || 0)
    return maxAttempts > 0 && attempts >= maxAttempts && !job.nextAttemptAtUtc
  }

  return false
}

function normalizeJobStatus(job: BackgroundJobDetailDto) {
  return firstDisplayText(job.statusName, job.status).toLowerCase()
}

function extractStructuredParseJobIds(jobs: BackgroundJobDetailDto[], currentJobIds: string[]) {
  const current = new Set(currentJobIds.map((id) => String(id)))
  const childIds = jobs
    .map((job) => extractStructuredParseJobId(job))
    .filter((id) => id && !current.has(id))
  return Array.from(new Set(childIds))
}

function extractStructuredParseJobId(job: BackgroundJobDetailDto) {
  const sources = [job.result, job.resultPreview]
  for (const source of sources) {
    const id = extractStructuredParseJobIdFromText(source)
    if (id) return id
  }

  return ''
}

function extractStructuredParseJobIdFromText(value?: string | null) {
  if (!value) return ''

  const patterns = [
    /"structuredParseJobId"\s*:\s*"?(\d+)"?/i,
    /\bstructuredParseJobId\s*=\s*(\d+)/i,
    /\bstructuredParseJobId\s*:\s*(\d+)/i,
  ]
  for (const pattern of patterns) {
    const normalized = normalizeNumericId(value.match(pattern)?.[1])
    if (normalized) return normalized
  }

  return ''
}

function noticePromptKindLabel(kind: NoticePromptKind) {
  return kind === 'award' ? '中标公告' : '前置公告'
}

async function openProcurementSearch(row?: LifecyclePackageLinkDto | null, applyToRelatedLinks = false) {
  const link = row || activeLink.value
  if (!link) return
  if (!link.projectCode) {
    ElMessage.warning('当前闭环记录没有采购编号，无法按项目编号搜索')
    return
  }

  procurementSearchLink.value = link
  procurementSearchApplyToRelatedLinks.value = applyToRelatedLinks
  procurementSearchVisible.value = true
  await searchProcurementCandidates()
}

function findProcurementRematchLink(item?: NoticeContextItem | null) {
  const links = item?.links || []
  return links.find((link) => !!link.projectCode && link.linkStatus !== 'Rejected') ||
    links.find((link) => !!link.projectCode) ||
    procurementSearchTarget.value
}

async function openProcurementRematch(item?: NoticeContextItem | null) {
  const link = findProcurementRematchLink(item)
  if (!link?.projectCode) {
    ElMessage.warning('当前前置公告没有可用于重新匹配的招标编号/采购编号')
    return
  }

  await openProcurementSearch(link, true)
}

async function searchProcurementCandidates() {
  if (!procurementSearchLink.value) return

  procurementCandidates.value = []
  procurementSearchLoading.value = true
  try {
    const candidates = await lifecycleApi.procurementCandidates(procurementSearchLink.value.id)
    const filteredCandidates = filterProcurementCandidatesForLink(candidates, procurementSearchLink.value)
    if (filteredCandidates.length < candidates.length) {
      ElMessage.warning(`已过滤 ${candidates.length - filteredCandidates.length} 条非当前招标/采购编号的候选公告`)
    }
    procurementCandidates.value = filteredCandidates
  } finally {
    procurementSearchLoading.value = false
  }
}

function filterProcurementCandidatesForLink(
  candidates: LifecycleProcurementNoticeCandidateDto[],
  link?: LifecyclePackageLinkDto | null,
) {
  const projectCode = normalizeProcurementSearchCode(firstDisplayText(link?.projectCode, link?.lotNo))
  if (!projectCode) return candidates

  return candidates.filter((candidate) => {
    const candidateCode = normalizeProcurementSearchCode(candidate.projectCode)
    return candidateCode === projectCode ||
      (!!candidate.title && candidate.title.toUpperCase().includes(projectCode))
  })
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
      applyToRelatedLinks: procurementSearchApplyToRelatedLinks.value,
    })

    if (result.message) {
      ElMessage.success(result.message)
    } else if (result.rawNoticeId) {
      ElMessage.success('前置公告已重新匹配到闭环记录')
    } else if (result.importJob) {
      ElMessage.success(`前置公告导入任务已提交：${result.importJob.jobId}`)
    } else {
      ElMessage.success('操作已提交')
    }

    await table.loadData()
    const updated = table.result.items.find((item) => item.id === link.id)
    if (updated) {
      procurementSearchLink.value = updated
      if (activeLink.value?.id === updated.id) activeLink.value = updated
    }
    if (activeLink.value) {
      const activeUpdated = table.result.items.find((item) => item.id === activeLink.value?.id)
      if (activeUpdated) activeLink.value = activeUpdated
    }
    procurementSearchVisible.value = false
  } finally {
    procurementImportingKey.value = ''
  }
}

function candidateKey(candidate: LifecycleProcurementNoticeCandidateDto) {
  return candidate.detailUrl || String(candidate.noticeId)
}

function normalizeProcurementSearchCode(value?: unknown) {
  let cleaned = firstDisplayText(value)
    .replace(/[－—–]/g, '-')
    .replace(/／/g, '/')
    .trim()
  if (!cleaned) return ''

  cleaned = cleaned.replace(/^(?:code|项目编号|项目编码|项目代码|采购编号|采购项目编号|采购项目编码|招标编号|招标项目编号|招标项目编码|批次编号)\s*[:：=]\s*/i, '')
  const lotPrefix = cleaned.match(/(^|[^A-Za-z0-9])([A-Za-z0-9]{6})(?=[-_/.][A-Za-z0-9])/)
  if (lotPrefix?.[2]) return lotPrefix[2].toUpperCase()

  const match = cleaned.match(/[A-Za-z0-9][A-Za-z0-9_.\-/]*/)
  return (match?.[0] || cleaned).replace(/[。.;；,，、）)]*$/g, '').toUpperCase()
}

function scorePercent(value: number) {
  return Math.max(0, Math.min(100, Math.round(Number(value || 0) * 100)))
}

function buildNoticeContext(kind: 'award' | 'procurement'): NoticeContextSummary {
  const map = new Map<string, NoticeContextItem>()
  let missingCount = 0

  for (const row of table.result.items) {
    const notice = kind === 'award' ? row.awardNotice : row.procurementNotice
    const rawNoticeId = firstDisplayText(notice?.rawNoticeId, kind === 'award' ? row.awardRawNoticeId : row.procurementRawNoticeId)
    const key = firstDisplayText(rawNoticeId, notice?.detailUrl, notice?.title)
    if (!key) {
      missingCount += 1
      continue
    }

    const existing = map.get(key)
    if (existing) {
      existing.links.push(row)
      if (!existing.notice && notice) existing.notice = notice
      if (!existing.rawNoticeId && rawNoticeId) existing.rawNoticeId = rawNoticeId
      continue
    }

    map.set(key, {
      key,
      notice: notice || null,
      rawNoticeId,
      links: [row],
    })
  }

  const items = Array.from(map.values())
  return {
    primary: items[0] || null,
    items,
    missingCount,
  }
}

function uniqueDisplayTexts(values: Array<unknown>) {
  return Array.from(new Set(values.map((value) => firstDisplayText(value)).filter(Boolean)))
}

function noticeContextTitle(item?: NoticeContextItem | null) {
  return firstDisplayText(item?.notice?.title, item?.rawNoticeId ? `RawNotice ${item.rawNoticeId}` : '')
}

function noticeContextMeta(item?: NoticeContextItem | null) {
  if (!item) return '-'
  const notice = item.notice
  return [
    item.rawNoticeId ? `RawNotice ${item.rawNoticeId}` : '',
    notice?.noticeType ? formatNoticeType(notice.noticeType) || notice.noticeType : '',
    notice?.matchSource ? noticeMatchLabel(notice.matchSource) : '',
    notice?.publishTime ? formatDateTime(notice.publishTime) : '',
  ].filter(Boolean).join(' · ') || '-'
}

function noticeContextUrl(item?: NoticeContextItem | null) {
  return item?.notice?.detailUrl || ''
}

function openNoticeContext(item?: NoticeContextItem | null) {
  if (!item) return
  if (item.notice) {
    openNoticeRef(item.notice)
    return
  }
  openRawNotice(item.rawNoticeId)
}

function openNoticeJobs(item?: NoticeContextItem | null) {
  const businessId = firstDisplayText(item?.rawNoticeId, item?.notice?.rawNoticeId)
  if (!businessId) return

  router.push({
    path: '/bidops/operations/jobs',
    query: {
      businessId,
      sortBy: 'CompletedAt',
      sortDescending: 'true',
    },
  })
}

function openActiveRawNoticeJobs() {
  const businessId = activeRawNoticeId.value
  if (!businessId) return

  router.push({
    path: '/bidops/operations/jobs',
    query: {
      businessId,
      sortBy: 'CompletedAt',
      sortDescending: 'true',
    },
  })
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

function lifecyclePackageNo(row: LifecyclePackageLinkDto) {
  return firstDisplayText(
    row.packageNo,
    evidenceString(row, ['packageNo']),
    evidenceString(row, ['award', 'packageNo']),
    evidenceString(row, ['matchedCandidate', 'packageNo']),
    evidenceString(row, ['tender', 'packageNo']),
  )
}

function lifecyclePackageName(row: LifecyclePackageLinkDto) {
  return firstDisplayText(
    row.packageName,
    evidenceString(row, ['packageName']),
    evidenceString(row, ['award', 'packageName']),
    evidenceString(row, ['matchedCandidate', 'packageName']),
    evidenceString(row, ['tender', 'packageName']),
  )
}

function shortTitle(value?: string | null, max = 34) {
  const text = firstDisplayText(value)
  if (!text) return '-'
  return text.length > max ? `${text.slice(0, max)}...` : text
}

function noticeMatchLabel(value?: string | null) {
  if (value === 'InferredByProjectCode') return '按编号推断'
  if (value === 'Linked') return '已关联'
  return value || '-'
}

function sourceNoticeTypeLabel(value?: string | null) {
  const labels: Record<string, string> = {
    tender_notice: '招标公告',
    bid_invitation: '投标邀请书',
    procurement_notice: '前置公告',
    procurement_invitation: '采购邀请',
    prequalification_notice: '资格预审公告',
    change_notice: '变更公告',
    unknown: '未知',
  }
  return labels[firstDisplayText(value)] || firstDisplayText(value) || '-'
}

function projectProcessTypeLabel(value?: string | null) {
  const labels: Record<string, string> = {
    bidding: '招标项目',
    non_bidding: '非招标项目',
    prequalification: '资格预审项目',
    unknown: '未知',
  }
  return labels[firstDisplayText(value)] || firstDisplayText(value) || '-'
}

function finalAwardAmountText(value?: number | null) {
  return value === null || value === undefined ? '未确认' : formatMoney(value)
}

function amountCandidateAmountText(row: AmountCandidateDto) {
  if (row.amountValue === null || row.amountValue === undefined) return row.amountRaw || '-'
  if (row.amountUnit === 'rate') return `${(Number(row.amountValue) * 100).toFixed(2)}%`
  if (row.amountUnit === 'discount') return `${(Number(row.amountValue) * 10).toFixed(2)}折`
  return formatMoney(row.amountValue)
}

function amountCandidateStatusType(status?: string | null) {
  if (status === 'Selected') return 'success'
  if (status === 'Recommended') return 'primary'
  if (status === 'Rejected') return 'danger'
  if (status === 'Unresolved') return 'warning'
  return 'info'
}

function amountCandidateSourceText(row: AmountCandidateDto) {
  return firstDisplayText(
    row.evidenceSource,
    row.sourceFileName,
    row.sourceTitle,
    row.sourceKind,
  ) || '-'
}

function amountCandidateLocationText(row: AmountCandidateDto) {
  return firstDisplayText(row.sourceLocation, row.rawAttachmentId ? `附件 ${row.rawAttachmentId}` : '', row.rawNoticeId ? `Raw ${row.rawNoticeId}` : '') || '-'
}

function amountCandidateEvidenceRowText(row: AmountCandidateDto) {
  return firstDisplayText(row.evidenceRowText, row.evidenceText, row.contextText, row.rejectReason) || '-'
}

function amountCandidateUnitBasisText(row: AmountCandidateDto) {
  const header = firstDisplayText(row.evidenceHeaderText)
  const unit = firstDisplayText(row.evidenceUnitText, row.amountUnit)
  const scale = row.evidenceUnitScale ? `换算系数 ${Number(row.evidenceUnitScale).toLocaleString('zh-CN')}` : ''
  const tenThousand = row.evidenceHasTenThousandYuanUnit ? '表头/上下文含万元' : '未见万元表头'
  return [header ? `表头：${header}` : '', unit ? `单位：${unit}` : '', scale, tenThousand].filter(Boolean).join('；') || '-'
}

function isPotentialFinalAmountType(amountType?: string | null) {
  return !!amountType && finalAmountCandidateTypes.has(amountType)
}

function canSelectAmountCandidate(row: AmountCandidateDto) {
  return !!row.amountValue &&
    row.amountUnit !== 'rate' &&
    row.amountUnit !== 'discount' &&
    row.status !== 'Selected' &&
    isPotentialFinalAmountType(row.amountType)
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

const lifecycleReviewTextTranslations: Record<string, string> = {
  'projectcode present on award evidence': '中标公告证据中有采购编号',
  'packageno normalized from award evidence': '已从中标公告证据识别包号',
  'awarded supplier matched candidate': '中标商家与候选公示厂家匹配',
  'awarded supplier matched candidate rank 1': '中标商家匹配第一候选人',
  'awarded supplier matched candidate rank 1 final quote': '中标商家匹配第一候选人的最终报价',
  'awarded supplier had a unique candidate final quote': '中标商家只有一条可用候选报价',
  'tender/procurement package evidence matched': '已匹配前置公告包件证据',
  'award notice disclosed a direct award amount': '中标公告直接披露中标金额',
  'award amount missing; defaulted final award amount to procurement package amount': '中标金额缺失，已默认采用采购包金额',
  'defaulted procurement amounts require manual review before formal supplier analytics': '采用采购金额默认值，需要人工复核后再进入正式供应商分析',
  'inferred amounts require manual review before formal supplier analytics': '推断金额需要人工复核后再进入正式供应商分析',
  'award amount missing': '中标金额缺失',
  'candidate final quote missing': '候选报价缺失',
  'no supported award rate evidence found': '未找到可支持金额推断的中标费率证据',
  'rate semantics are ambiguous; the source has a percentage but not discount/reduction/coefficient wording': '费率语义不明确：原文有百分比，但没有折扣率、下浮率或系数说明',
  'a rate was found, but no unambiguous package-level base amount was found': '已找到费率，但没有明确的包级基准金额',
  'project code missing': '采购编号缺失',
  'package number ambiguous': '包号不明确',
  'awarded supplier did not match candidate supplier': '中标商家未匹配到候选厂家',
  'candidate notice not found': '未找到候选公示',
  'tender amount missing': '前置公告金额缺失',
  'tender notice not found': '未找到前置公告',
  'manual review required': '需要人工复核',
  'manualreviewrequired': '需要人工复核',
  'amountcannotbeinferred': '金额无法推断',
  'baseamountmissing': '基准金额缺失',
  'ratesemanticsambiguous': '费率语义不明确',
}

function lifecycleReviewTextLabel(value: string) {
  const text = firstDisplayText(value)
  if (!text) return ''

  const normalized = normalizeLifecycleReviewText(text)
  const translated = lifecycleReviewTextTranslations[normalized]
  if (translated) return translated

  const awardRate = text.match(/^Award notice disclosed\s+(.+)\.?$/i)
  if (awardRate) return `中标公告披露${lifecycleAmountCodeLabel(awardRate[1])}`

  const tenderBase = text.match(/^Tender evidence provided\s+(.+)\.?$/i)
  if (tenderBase) return `前置公告提供${lifecycleAmountCodeLabel(tenderBase[1])}`

  return text
}

function normalizeLifecycleReviewText(value: string) {
  return value.trim().replace(/[.。]+$/, '').toLowerCase()
}

function lifecycleAmountCodeLabel(value: string) {
  const normalized = value.trim().replace(/[.。]+$/, '')
  const labels: Record<string, string> = {
    DiscountRate: '折扣率',
    ReductionRate: '下浮率',
    Coefficient: '系数',
    PackageGuidePrice: '采购包概算金额',
    PackageBudget: '采购包预算金额',
    PackageMaxPrice: '采购包最高限价',
    LotGuidePrice: '分标概算金额',
    ProjectGuidePrice: '项目概算金额',
    UnitPrice: '单价',
    FrameworkEstimatedAmount: '框架估算金额',
  }
  return labels[normalized] || normalized
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

function normalizeNumericId(value: unknown) {
  if (value === null || value === undefined) return ''
  const text = String(value).trim()
  return /^\d+$/.test(text) ? text : ''
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
    applyRouteRawNoticeQuery(rawNoticeId)
    await table.search()
    return
  }

  await table.loadData()
}

function applyRouteRawNoticeQuery(rawNoticeId: string) {
  table.query.rawNoticeId = rawNoticeId
  table.query.linkStatus = ''
  table.query.matchType = ''
  table.query.requiresManualReview = ''
  // 从公告入口进入闭环页时要看到本公告完整明细，避免沿用上次列表页的状态筛选漏掉“仅展示/流标”行。
  table.query.pageIndex = 1
}

onMounted(loadForRoute)

onUnmounted(() => {
  for (const key of Array.from(promptTaskTimers.keys())) {
    clearPromptJobMonitor(key)
  }
})

watch(
  () => route.query.rawNoticeId,
  async (value, oldValue) => {
    if (value === oldValue) return
    const rawNoticeId = String(value || '').trim()
    if (!rawNoticeId) return
    applyRouteRawNoticeQuery(rawNoticeId)
    await table.search()
  },
)
</script>

<template>
  <PageContainer title="闭环任务与审核中心" description="顶部展示本次闭环公告上下文，下方审核中标明细、证据、失败/缺失原因和人工确认结果。">
    <template #actions>
      <el-button :icon="Tickets" @click="router.push('/bidops/notices')">结果公告</el-button>
      <el-button :icon="Tickets" @click="router.push('/bidops/operations/jobs')">后台任务</el-button>
      <el-button :icon="Refresh" :loading="table.loading" @click="table.loadData">刷新</el-button>
    </template>

    <div ref="pageTopRef" class="page-scroll-anchor" aria-hidden="true" />

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

    <section v-if="!table.loading && !table.result.items.length && activeRawNoticeId" class="empty-closure-hint">
      <div>
        <strong>当前公告还没有闭环明细</strong>
        <span>可查看该 RawNotice 的后台任务，确认分析是否仍在执行、已失败或未生成可审核关联。</span>
      </div>
      <div class="empty-closure-actions">
        <el-button type="primary" plain :icon="Tickets" @click="openActiveRawNoticeJobs">查看该公告任务</el-button>
        <el-button :icon="Refresh" :loading="table.loading" @click="table.loadData">刷新列表</el-button>
      </div>
    </section>

    <section v-if="table.result.items.length" class="closure-context">
      <div class="closure-context-header">
        <div class="closure-title-block">
          <div class="closure-eyebrow-line">
            <span class="context-eyebrow">本次闭环公告</span>
            <el-tag size="small" effect="light">当前项目编号：{{ closureCurrentProjectCode }}</el-tag>
          </div>
          <strong>{{ closureProjectSummary }}</strong>
        </div>
        <div class="closure-context-actions">
          <el-button
            v-if="canApprove"
            size="small"
            type="primary"
            plain
            :icon="Edit"
            :disabled="!closureProjectCodeEditTarget || awardNoticeContext.items.length > 1"
            @click="openClosureProjectCodeEdit"
          >
            修改项目编号
          </el-button>
          <el-tag effect="plain">{{ table.result.total }} 条中标明细</el-tag>
        </div>
      </div>

      <div class="notice-context-grid">
        <div class="notice-summary">
          <div class="notice-summary-head">
            <span>中标公告</span>
            <el-tag v-if="awardNoticeContext.items.length > 1" type="warning" effect="light">
              当前筛选 {{ awardNoticeContext.items.length }} 条
            </el-tag>
          </div>
          <template v-if="awardNoticeContext.primary">
            <el-button link type="primary" :icon="LinkIcon" @click="openNoticeContext(awardNoticeContext.primary)">
              {{ shortTitle(noticeContextTitle(awardNoticeContext.primary), 48) }}
            </el-button>
            <div class="notice-meta">{{ noticeContextMeta(awardNoticeContext.primary) }}</div>
            <div class="notice-actions">
              <el-button
                link
                type="primary"
                :disabled="!noticeContextUrl(awardNoticeContext.primary)"
                @click="openExternalUrl(noticeContextUrl(awardNoticeContext.primary))"
              >
                打开原公告
              </el-button>
              <el-button link type="primary" @click="openNoticeJobs(awardNoticeContext.primary)">解析/AI任务</el-button>
              <el-button
                v-if="canApprove"
                link
                type="warning"
                :loading="noticePromptLoadingKey === noticePromptItemKey('award', awardNoticeContext.primary)"
                @click="openNoticePrompt('award', awardNoticeContext.primary)"
              >
                AI提示词辅助解析
              </el-button>
              <el-button
                v-if="canApprove"
                link
                type="warning"
                :loading="outcomeReparseLoadingKey === awardNoticeContext.primary.rawNoticeId"
                @click="enqueueOutcomeSupplierReparse(awardNoticeContext.primary)"
              >
                重抽并刷新闭环
              </el-button>
              <el-button
                v-if="canApprove"
                link
                type="success"
                :icon="Check"
                :loading="autoCollectLoading"
                @click="autoCollectAndReviewProcurementNotice"
              >
                自动补采集/审核
              </el-button>
            </div>
            <span v-if="awardNoticeContext.items.length > 1" class="muted-text">
              当前列表包含多个中标公告，可按 RawNoticeId 筛选到单次闭环。
            </span>
          </template>
          <div v-else class="notice-missing">
            <el-tag type="warning" effect="light">未匹配中标公告</el-tag>
          </div>
        </div>

        <div class="notice-summary">
          <div class="notice-summary-head">
            <span>对应前置公告</span>
            <el-tag v-if="procurementNoticeContext.items.length > 1" type="warning" effect="light">
              当前筛选 {{ procurementNoticeContext.items.length }} 条
            </el-tag>
          </div>
          <template v-if="procurementNoticeContext.primary">
            <el-button link type="primary" :icon="LinkIcon" @click="openNoticeContext(procurementNoticeContext.primary)">
              {{ shortTitle(noticeContextTitle(procurementNoticeContext.primary), 48) }}
            </el-button>
            <div class="notice-meta">{{ noticeContextMeta(procurementNoticeContext.primary) }}</div>
            <div class="notice-actions">
              <el-button
                link
                type="primary"
                :disabled="!noticeContextUrl(procurementNoticeContext.primary)"
                @click="openExternalUrl(noticeContextUrl(procurementNoticeContext.primary))"
              >
                打开原公告
              </el-button>
              <el-button link type="primary" @click="openNoticeJobs(procurementNoticeContext.primary)">解析/AI任务</el-button>
              <el-button
                v-if="canApprove"
                link
                type="primary"
                :icon="Search"
                @click="openProcurementRematch(procurementNoticeContext.primary)"
              >
                重新匹配前置公告
              </el-button>
              <el-button
                v-if="canApprove"
                link
                type="warning"
                :icon="Refresh"
                :loading="procurementReparseLoadingKey === procurementNoticeContext.primary.rawNoticeId"
                @click="enqueueProcurementNoticeReparse(procurementNoticeContext.primary)"
              >
                重新解析内容
              </el-button>
              <el-button
                v-if="canApprove"
                link
                type="warning"
                :loading="noticePromptLoadingKey === noticePromptItemKey('procurement', procurementNoticeContext.primary)"
                @click="openNoticePrompt('procurement', procurementNoticeContext.primary)"
              >
                AI提示词辅助解析
              </el-button>
            </div>
            <span v-if="procurementNoticeContext.missingCount > 0" class="muted-text">
              当前明细中仍有 {{ procurementNoticeContext.missingCount }} 条未关联前置公告。
            </span>
          </template>
          <div v-else class="notice-missing">
            <el-tag type="warning" effect="light">未匹配前置公告</el-tag>
            <span class="muted-text">
              {{ procurementSearchTarget?.procurementNoticeMissingReason || '可按招标编号/采购编号搜索并关联公开前置公告。' }}
            </span>
            <ul v-if="procurementSearchTarget?.sourceNoticeSearchColumns?.length" class="searched-columns">
              <li v-for="column in procurementSearchTarget.sourceNoticeSearchColumns" :key="column.sourceNoticeType">
                {{ column.columnName }}：{{ column.matched ? '已命中' : '未命中' }}，候选数量 {{ column.candidateCount }}
              </li>
            </ul>
            <el-button
              v-if="procurementSearchTarget"
              link
              type="primary"
              :icon="Search"
              @click="openProcurementSearch(procurementSearchTarget)"
            >
              按招标/采购编号搜索前置公告
            </el-button>
            <el-button
              v-if="canApprove && procurementSearchTarget"
              link
              type="success"
              :icon="Check"
              :loading="autoCollectLoading"
              @click="autoCollectAndReviewProcurementNotice"
            >
              自动补采集/审核
            </el-button>
          </div>
        </div>
      </div>
    </section>

    <div v-if="canApprove && table.result.items.length" class="batch-toolbar">
      <div class="batch-summary">
        <strong>已选 {{ selectedReviewRows.length }} 条</strong>
        <span>仅可批量审核待确认明细</span>
      </div>
      <div class="batch-actions">
        <el-button
          size="small"
          type="success"
          :icon="Check"
          :disabled="selectedReviewRows.length === 0"
          :loading="batchReviewLoading"
          @click="batchConfirmSelected"
        >
          批量确认
        </el-button>
        <el-button
          size="small"
          type="warning"
          plain
          :icon="Delete"
          :disabled="selectedRowsWithFinalAmount.length === 0"
          :loading="batchAmountClearLoading"
          @click="batchClearFinalAwardAmounts"
        >
          清空金额
        </el-button>
        <el-button
          size="small"
          type="danger"
          plain
          :icon="Close"
          :disabled="selectedReviewRows.length === 0"
          :loading="batchReviewLoading"
          @click="batchRejectSelected"
        >
          批量驳回
        </el-button>
      </div>
    </div>

    <DataTable
      :data="table.result.items"
      :loading="table.loading"
      empty-text="暂无生命周期关联建议"
      @sort-change="handleSortChange"
      @selection-change="handleSelectionChange"
    >
      <el-table-column
        v-if="canApprove"
        type="selection"
        width="48"
        fixed="left"
        :selectable="canSelectLifecycleRow"
      />
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
            <span>{{ row.projectCode || '未识别招标/采购编号' }}</span>
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
        label="包号"
        width="110"
        show-overflow-tooltip
        sortable="custom"
        :sort-order="sortOrder('PackageNoAsc', 'PackageNoDesc')"
      >
        <template #default="{ row }">{{ lifecyclePackageNo(row) || '-' }}</template>
      </el-table-column>
      <el-table-column
        prop="packageName"
        label="包名称"
        min-width="220"
        show-overflow-tooltip
      >
        <template #default="{ row }">{{ lifecyclePackageName(row) || '-' }}</template>
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
        prop="procurementPackageAmount"
        label="采购包金额"
        width="150"
        align="right"
      >
        <template #default="{ row }">{{ formatMoney(row.procurementPackageAmount) }}</template>
      </el-table-column>
      <el-table-column
        prop="finalAwardAmount"
        label="中标金额"
        width="150"
        align="right"
        sortable="custom"
        :sort-order="sortOrder('AmountAsc', 'AmountDesc')"
      >
        <template #default="{ row }">{{ finalAwardAmountText(row.finalAwardAmount) }}</template>
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
      <el-table-column label="操作" width="210" fixed="right">
        <template #default="{ row }">
          <el-button size="small" :icon="View" @click="openDetail(row)">详情</el-button>
          <el-button
            v-if="canApprove && canOperateLifecycleRow(row)"
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
            v-if="canApprove && canOperateLifecycleRow(row)"
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
    <div ref="pageBottomRef" class="page-scroll-anchor" aria-hidden="true" />

    <div v-if="table.result.items.length" class="page-jump-rail">
      <el-tooltip content="回到顶部" placement="left">
        <el-button circle :icon="Top" aria-label="回到顶部" @click="scrollToPageTop" />
      </el-tooltip>
      <el-tooltip content="回到底部" placement="left">
        <el-button circle :icon="Bottom" aria-label="回到底部" @click="scrollToPageBottom" />
      </el-tooltip>
    </div>

    <el-drawer v-model="detailVisible" size="920px" title="生命周期关联详情">
      <template v-if="activeLink">
        <el-descriptions :column="2" border>
          <el-descriptions-item label="招标编号 / 采购编号">{{ activeLink.projectCode || '-' }}</el-descriptions-item>
          <el-descriptions-item label="状态"><BidOpsStatusTag :value="activeLink.linkStatus" /></el-descriptions-item>
          <el-descriptions-item label="项目名称" :span="2">{{ activeLink.projectName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="项目流程类型">{{ projectProcessTypeLabel(activeLink.projectProcessType) }}</el-descriptions-item>
          <el-descriptions-item label="采购方式">{{ activeLink.procurementMethod || '-' }}</el-descriptions-item>
          <el-descriptions-item label="前置公告类型">{{ sourceNoticeTypeLabel(activeLink.sourceNoticeType) }}</el-descriptions-item>
          <el-descriptions-item label="命中栏目">{{ activeLink.sourceNoticeColumn || '-' }}</el-descriptions-item>
          <el-descriptions-item label="分标编号">{{ lifecycleLotNo(activeLink) || '-' }}</el-descriptions-item>
          <el-descriptions-item label="分标名称">{{ lifecycleLotName(activeLink) || '-' }}</el-descriptions-item>
          <el-descriptions-item label="包号">{{ lifecyclePackageNo(activeLink) || '-' }}</el-descriptions-item>
          <el-descriptions-item label="包名称">{{ lifecyclePackageName(activeLink) || '-' }}</el-descriptions-item>
          <el-descriptions-item label="中标商家">{{ activeLink.supplierName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="采购包金额">{{ formatMoney(activeLink.procurementPackageAmount) }}</el-descriptions-item>
          <el-descriptions-item label="中标金额">{{ finalAwardAmountText(activeLink.finalAwardAmount) }}</el-descriptions-item>
          <el-descriptions-item label="金额来源">{{ formatLifecycleAmountSource(activeLink.finalAwardAmountSource) }}</el-descriptions-item>
          <el-descriptions-item label="前置公告 Raw">
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

        <section class="detail-section">
          <div class="section-heading compact">
            <h2>金额候选池</h2>
            <el-button
              size="small"
              :icon="Refresh"
              :loading="amountCandidateLoading"
              :disabled="isStatusOnlyLifecycleRow(activeLink)"
              @click="refreshAmountCandidates(activeLink)"
            >
              刷新
            </el-button>
          </div>
          <el-empty v-if="!amountCandidateLoading && !activeLink.amountCandidates?.length" description="未识别到金额候选" />
          <template v-for="group in amountCandidateGroups" :key="group.status">
            <div class="candidate-group-heading">
              <h3>{{ formatCommonStatus(group.status) }}</h3>
              <el-tag effect="light">{{ group.rows.length }} 条</el-tag>
            </div>
            <el-table
              v-loading="amountCandidateLoading"
              :data="group.rows"
              border
              size="small"
              empty-text="未识别到金额候选"
            >
              <el-table-column label="金额" width="140" align="right">
                <template #default="{ row }">{{ amountCandidateAmountText(row) }}</template>
              </el-table-column>
              <el-table-column label="类型" width="120">
                <template #default="{ row }">{{ formatAmountCandidateType(row.amountType) }}</template>
              </el-table-column>
              <el-table-column label="状态" width="105">
                <template #default="{ row }">
                  <el-tag :type="amountCandidateStatusType(row.status)" effect="light">{{ formatCommonStatus(row.status) }}</el-tag>
                </template>
              </el-table-column>
              <el-table-column label="来源" min-width="170" show-overflow-tooltip>
                <template #default="{ row }">{{ amountCandidateSourceText(row) }}</template>
              </el-table-column>
              <el-table-column label="位置" min-width="190" show-overflow-tooltip>
                <template #default="{ row }">{{ amountCandidateLocationText(row) }}</template>
              </el-table-column>
              <el-table-column label="表头/单位" min-width="240" show-overflow-tooltip>
                <template #default="{ row }">{{ amountCandidateUnitBasisText(row) }}</template>
              </el-table-column>
              <el-table-column label="分标/包/供应商" min-width="210" show-overflow-tooltip>
                <template #default="{ row }">
                  {{ firstDisplayText(row.lotNo, '-') }} / {{ firstDisplayText(row.packageNo, '-') }} / {{ firstDisplayText(row.supplierName, '-') }}
                </template>
              </el-table-column>
              <el-table-column label="原始行" min-width="300" show-overflow-tooltip>
                <template #default="{ row }">{{ amountCandidateEvidenceRowText(row) }}</template>
              </el-table-column>
              <el-table-column label="操作" width="230" fixed="right">
                <template #default="{ row }">
                  <el-button
                    link
                    type="primary"
                    size="small"
                    :loading="amountCandidateActionLoadingKey === amountCandidateActionKey(row, 'select')"
                    :disabled="!canApprove || !canSelectAmountCandidate(row)"
                    @click="selectAmountCandidate(row)"
                  >
                    采用
                  </el-button>
                  <el-dropdown
                    trigger="click"
                    :disabled="!canApprove"
                    @command="handleAmountCandidateTypeCommand(row, $event)"
                  >
                    <el-button link type="primary" size="small">标记</el-button>
                    <template #dropdown>
                      <el-dropdown-menu>
                        <el-dropdown-item
                          v-for="item in amountCandidateTypeOptions"
                          :key="item.value"
                          :command="item.value"
                        >
                          {{ item.label }}
                        </el-dropdown-item>
                      </el-dropdown-menu>
                    </template>
                  </el-dropdown>
                  <el-button
                    v-if="row.status !== 'Rejected'"
                    link
                    type="danger"
                    size="small"
                    :loading="amountCandidateActionLoadingKey === amountCandidateActionKey(row, 'reject')"
                    :disabled="!canApprove"
                    @click="rejectAmountCandidate(row)"
                  >
                    排除
                  </el-button>
                  <el-button
                    v-else
                    link
                    type="primary"
                    size="small"
                    :loading="amountCandidateActionLoadingKey === amountCandidateActionKey(row, 'restore')"
                    :disabled="!canApprove"
                    @click="restoreAmountCandidate(row)"
                  >
                    恢复
                  </el-button>
                </template>
              </el-table-column>
            </el-table>
          </template>
        </section>

        <section class="detail-section">
          <h2>对应前置公告证据</h2>
          <div v-if="!activeLink.procurementNotice" class="missing-procurement">
            <el-alert
              :title="activeLink.procurementNoticeMissingReason || '未匹配到前置公告 RawNotice。'"
              type="warning"
              show-icon
              :closable="false"
            />
            <ul v-if="activeLink.sourceNoticeSearchColumns?.length" class="searched-columns">
              <li v-for="column in activeLink.sourceNoticeSearchColumns" :key="column.sourceNoticeType">
                {{ column.columnName }}：{{ column.matched ? '已命中' : '未命中' }}，候选数量 {{ column.candidateCount }}
              </li>
            </ul>
            <div class="notice-actions">
              <el-button
                type="primary"
                plain
                :icon="Search"
                :loading="procurementSearchLoading && procurementSearchLink?.id === activeLink.id"
                :disabled="!activeLink.projectCode"
                @click="openProcurementSearch(activeLink)"
              >
                按招标/采购编号搜索前置公告
              </el-button>
            </div>
          </div>
          <template v-else>
            <el-descriptions :column="2" border>
              <el-descriptions-item label="公告标题" :span="2">{{ activeLink.procurementNotice.title || '-' }}</el-descriptions-item>
              <el-descriptions-item label="前置公告类型">{{ sourceNoticeTypeLabel(activeLink.sourceNoticeType) }}</el-descriptions-item>
              <el-descriptions-item label="命中栏目">{{ activeLink.sourceNoticeColumn || '-' }}</el-descriptions-item>
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
            <div class="notice-actions">
              <el-button
                v-if="canApprove"
                type="primary"
                plain
                :icon="Search"
                :loading="procurementSearchLoading && procurementSearchLink?.id === activeLink.id"
                :disabled="!activeLink.projectCode"
                @click="openProcurementSearch(activeLink, true)"
              >
                重新匹配前置公告
              </el-button>
            </div>
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
              <el-table-column label="来源" width="120">
                <template #default="{ row }">{{ formatLifecycleAmountSource(row.sourceStage) }}</template>
              </el-table-column>
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
          <span v-else class="muted-text">暂无字段补全建议。AI提示词辅助解析已移至顶部公告区域。</span>
        </section>

        <section class="detail-section">
          <h2>匹配理由</h2>
          <div class="tag-list">
            <el-tag v-for="(item, index) in selectedReasons" :key="`${item}:${index}`" type="success" effect="light">{{ item }}</el-tag>
            <span v-if="!selectedReasons.length" class="muted-text">-</span>
          </div>
        </section>

        <section class="detail-section">
          <h2>缺失字段</h2>
          <div class="tag-list">
            <el-tag v-for="(item, index) in selectedMissingFields" :key="`${item}:${index}`" type="warning" effect="light">{{ item }}</el-tag>
            <span v-if="!selectedMissingFields.length" class="muted-text">-</span>
          </div>
        </section>

        <section class="detail-section">
          <h2>证据 JSON</h2>
          <pre>{{ selectedEvidenceJson || '-' }}</pre>
        </section>
      </template>
    </el-drawer>

    <el-dialog v-model="noticePromptVisible" :title="noticePromptTitle" width="680px">
      <el-alert
        class="dialog-tip"
        type="info"
        show-icon
        :closable="false"
        :title="noticePromptHelpText"
      />
      <el-descriptions v-if="noticePromptItem" :column="1" border class="dialog-tip">
        <el-descriptions-item label="公告">{{ noticeContextTitle(noticePromptItem) }}</el-descriptions-item>
        <el-descriptions-item label="RawNoticeId">{{ noticePromptRawNoticeId(noticePromptItem) || '-' }}</el-descriptions-item>
      </el-descriptions>
      <el-form label-width="96px">
        <el-form-item label="AI提示词">
          <el-input
            v-model="noticePromptForm.reviewerPrompt"
            type="textarea"
            :rows="6"
            maxlength="2000"
            show-word-limit
            :placeholder="noticePromptPlaceholder"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="noticePromptVisible = false">取消</el-button>
        <el-button
          type="primary"
          :loading="noticePromptSubmitLoading"
          @click="submitNoticePrompt"
        >
          提交解析任务
        </el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="procurementSearchVisible" title="搜索前置公告" width="960px">
      <el-alert
        v-if="procurementSearchLink"
        class="dialog-tip"
        type="info"
        :closable="false"
        show-icon
        :title="procurementSearchDialogTitle"
        :description="procurementSearchDialogDescription"
      />
      <el-table
        v-loading="procurementSearchLoading"
        :data="procurementCandidates"
        border
        empty-text="未搜索到前置公告候选"
      >
        <el-table-column label="公告" min-width="320" show-overflow-tooltip>
          <template #default="{ row }">
            <div class="candidate-title">
              <strong>{{ row.title || '-' }}</strong>
              <span>{{ row.detailUrl }}</span>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="招标/采购编号" width="130">
          <template #default="{ row }">
            <el-tag :type="row.isExactProjectCodeMatch ? 'success' : 'warning'" effect="light">
              {{ row.projectCode || '-' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="前置类型" width="130">
          <template #default="{ row }">{{ sourceNoticeTypeLabel(row.sourceNoticeType) }}</template>
        </el-table-column>
        <el-table-column label="命中栏目" width="130">
          <template #default="{ row }">{{ row.sourceNoticeColumn || formatNoticeType(row.noticeType) || row.doctype }}</template>
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
              {{ row.existingRawNoticeId ? (procurementSearchApplyToRelatedLinks ? '替换' : '关联') : '导入' }}
            </el-button>
          </template>
        </el-table-column>
      </el-table>
      <template #footer>
        <el-button :icon="Refresh" :loading="procurementSearchLoading" @click="searchProcurementCandidates">重新搜索</el-button>
        <el-button @click="procurementSearchVisible = false">关闭</el-button>
      </template>
    </el-dialog>

    <el-dialog
      v-model="projectCodeVisible"
      :title="projectCodeDialogTitle"
      width="560px"
      append-to-body
    >
      <el-form label-width="120px">
        <el-form-item label="项目编号" required>
          <el-input
            v-model="projectCodeForm.projectCode"
            clearable
            placeholder="例如：SD26-FWSQ-KJ-JN02"
            @keyup.enter="submitProjectCodeEdit"
          />
        </el-form-item>
        <el-form-item label="处理范围">
          <el-checkbox v-model="projectCodeForm.applyToRelatedLinks">
            应用到本次闭环公告列表
          </el-checkbox>
        </el-form-item>
        <el-form-item label="前置公告">
          <el-checkbox v-model="projectCodeForm.clearProcurementNotice">
            清空现有前置公告关联
          </el-checkbox>
        </el-form-item>
        <el-form-item label="备注">
          <el-input
            v-model="projectCodeForm.remark"
            type="textarea"
            :rows="3"
            maxlength="300"
            show-word-limit
            placeholder="说明本次修改依据，例如：成交公告正文写有“采购项目编号：SD26-FWSQ-KJ-JN02”"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="closeProjectCodeEdit">取消</el-button>
        <el-button type="primary" :loading="projectCodeLoading" @click="submitProjectCodeEdit">
          保存
        </el-button>
      </template>
    </el-dialog>

    <el-dialog
      v-model="decisionVisible"
      :title="decisionTitle"
      width="560px"
      append-to-body
      :close-on-click-modal="true"
      :close-on-press-escape="true"
      :before-close="handleDecisionBeforeClose"
    >
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
          <el-select
            v-model="decisionForm.finalAwardAmountSource"
            clearable
            filterable
            placeholder="请选择金额来源"
            style="width: 260px"
          >
            <el-option
              v-for="item in decisionAmountSourceOptions"
              :key="item.value"
              :label="item.label"
              :value="item.value"
            />
          </el-select>
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
        <el-button @click="closeDecision">取消</el-button>
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

.page-scroll-anchor {
  width: 1px;
  height: 1px;
}

.page-jump-rail {
  position: fixed;
  right: 28px;
  bottom: 28px;
  z-index: 20;
  display: grid;
  gap: 8px;
}

.page-jump-rail :deep(.el-button) {
  box-shadow: 0 8px 18px rgb(15 23 42 / 14%);
}

.closure-context {
  display: grid;
  gap: 12px;
  margin-bottom: 14px;
  padding: 14px;
  border: 1px solid #dcdfe6;
  border-radius: 6px;
  background: #fff;
}

.batch-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 10px;
  padding: 10px 12px;
  border: 1px solid #ebeef5;
  border-radius: 6px;
  background: #f8fafc;
}

.empty-closure-hint {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
  padding: 12px 14px;
  border: 1px solid #d9ecff;
  border-radius: 6px;
  background: #f4faff;
}

.empty-closure-hint div:first-child {
  display: grid;
  gap: 4px;
  min-width: 0;
}

.empty-closure-hint strong {
  color: #1f2d3d;
  font-size: 14px;
}

.empty-closure-hint span {
  color: #687385;
  font-size: 13px;
}

.empty-closure-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 8px;
}

.batch-summary {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 8px;
  min-width: 0;
}

.batch-summary strong {
  color: #1f2d3d;
  font-size: 14px;
}

.batch-summary span {
  color: #687385;
  font-size: 13px;
}

.batch-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 8px;
}

.closure-context-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.closure-title-block {
  display: grid;
  gap: 4px;
  min-width: 0;
}

.closure-eyebrow-line {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.closure-title-block strong {
  overflow: hidden;
  color: #1f2d3d;
  font-size: 16px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.closure-context-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
}

.context-eyebrow {
  color: #687385;
  font-size: 12px;
}

.notice-context-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.notice-summary {
  display: grid;
  align-content: start;
  gap: 8px;
  min-width: 0;
  padding: 12px;
  border: 1px solid #ebeef5;
  border-radius: 6px;
  background: #fafcff;
}

.notice-summary :deep(.el-button) {
  justify-content: flex-start;
  min-width: 0;
  padding-left: 0;
}

.notice-summary :deep(.el-button span) {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.notice-summary-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  color: #1f2d3d;
  font-weight: 600;
}

.notice-meta {
  overflow: hidden;
  color: #687385;
  font-size: 13px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.notice-missing {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
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

.detail-section {
  margin-top: 18px;
}

.section-heading.compact {
  margin-top: 0;
  margin-bottom: 10px;
}

.candidate-group-heading {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 14px 0 8px;
}

.candidate-group-heading h3 {
  margin: 0;
  color: #17202a;
  font-size: 14px;
}

.subsection-title {
  margin: 14px 0 8px;
  color: #344054;
  font-size: 13px;
  font-weight: 600;
}

.missing-procurement {
  display: grid;
  gap: 12px;
}

.searched-columns {
  display: grid;
  gap: 4px;
  margin: 0;
  padding-left: 18px;
  color: #687385;
  font-size: 13px;
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

@media (max-width: 900px) {
  .page-jump-rail {
    right: 16px;
    bottom: 16px;
  }

  .empty-closure-hint {
    align-items: flex-start;
    flex-direction: column;
  }

  .empty-closure-actions {
    justify-content: flex-start;
  }

  .batch-toolbar {
    align-items: flex-start;
    flex-direction: column;
  }

  .batch-actions {
    justify-content: flex-start;
  }

  .closure-context-header,
  .notice-context-grid {
    grid-template-columns: 1fr;
  }

  .closure-context-header {
    display: grid;
  }
}

</style>
