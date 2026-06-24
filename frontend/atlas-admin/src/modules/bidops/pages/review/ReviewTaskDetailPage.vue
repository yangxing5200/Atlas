<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Delete, Edit, Plus, RefreshRight, View } from '@element-plus/icons-vue'
import { useRoute, useRouter } from 'vue-router'
import { rawNoticesApi } from '@/api/bidops/rawNotices.api'
import { reviewTasksApi } from '@/api/bidops/reviewTasks.api'
import PageContainer from '@/shared/components/PageContainer.vue'
import { useRequest } from '@/shared/composables/useRequest'
import { formatDateTime } from '@/shared/utils/date'
import { formatMoney } from '@/shared/utils/money'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import RawAttachmentTable from '../../components/RawAttachmentTable.vue'
import RawNoticePreview from '../../components/RawNoticePreview.vue'
import PermissionButton from '../../components/PermissionButton.vue'
import RequirementTable from '../../components/RequirementTable.vue'
import ReviewDecisionPanel from '../../components/ReviewDecisionPanel.vue'
import RiskLevelTag from '../../components/RiskLevelTag.vue'
import { BIDOPS_PERMISSIONS } from '../../constants'
import JobStatusTag from '@/modules/operations/components/JobStatusTag.vue'
import type { BackgroundJobListItemDto } from '@/modules/operations/types'
import { formatDuration, formatJobType } from '@/modules/operations/utils/display'
import type {
  OutcomeSupplierRecordDto,
  PackageStagingDto,
  RequirementStagingDto,
  ReviewQualityIssueDto,
  ReviewOutcomeSupplierRecordEditRequest,
  ReviewTaskDetailDto,
} from '../../types'
import {
  formatCategory,
  formatCommonStatus,
  formatNoticeType,
  formatPackageNo,
  formatQualityIssueType,
  formatReviewRecommendation,
} from '../../utils/display'

interface OutcomeEditForm {
  projectName: string
  projectCode: string
  buyerName: string
  lotNo: string
  lotName: string
  packageNo: string
  packageName: string
  category: string
  supplierName: string
  outcomeType: string
  rank: number | null
  awardAmount: number | null
  procurementAgencyServiceFeeAmount: number | null
  evidenceText: string
}

const route = useRoute()
const router = useRouter()
const detail = ref<ReviewTaskDetailDto | null>(null)
const loading = ref(false)
const decisionRequest = useRequest()
const rawReparseRequest = useRequest()
const outcomeAiReparseRequest = useRequest()
const outcomeEditRequest = useRequest()
const backgroundJobsRequest = useRequest()
const procurementAiPrompt = ref('')
const outcomeAiPrompt = ref('')
const outcomeAiJobId = ref('')
const rawReparseJobId = ref('')
const backgroundJobs = ref<BackgroundJobListItemDto[]>([])
const backgroundJobTotal = ref(0)
const outcomeEditVisible = ref(false)
const outcomeEditMode = ref<'create' | 'edit'>('create')
const editingOutcomeId = ref('')
const outcomeEditForm = ref<OutcomeEditForm>(createEmptyOutcomeForm())
const detailRefreshAttempts = ref(0)
let detailRefreshTimer: number | undefined
const taskId = computed(() => String(route.params.id || ''))
const task = computed(() => detail.value?.task)
const notice = computed(() => detail.value?.notice)
const rawNotice = computed(() => detail.value?.rawNotice)
const rawText = computed(() => rawNotice.value?.textContent || rawNotice.value?.textPreview || '')
const buyer = computed(() => detail.value?.buyer)
const outcomeSuppliers = computed(() => detail.value?.outcomeSuppliers || [])
const packages = computed(() => detail.value?.packages || [])
const attachments = computed(() => detail.value?.attachments || [])
const qualityIssues = computed(() => detail.value?.qualityIssues || [])
const activeQualityIssues = computed(() => qualityIssues.value.filter((item) => !item.isResolved))
const allRequirements = computed(() => packages.value.flatMap((pkg) => pkg.requirements || []))
const rejectRiskCount = computed(() => allRequirements.value.filter((item) => item.isRejectRisk).length)
const mandatoryCount = computed(() => allRequirements.value.filter((item) => item.isMandatory).length)
const noticeKind = computed(() => detectNoticeKind())
const awardRows = computed(() => outcomeSuppliers.value.slice())
const candidateRows = computed(() => outcomeSuppliers.value.slice())
const awardColumnVisible = computed(() => ({
  projectCode: hasAnyOutcomeColumnValue(awardRows.value, outcomeProjectCode),
  projectName: hasAnyOutcomeColumnValue(awardRows.value, outcomeProjectName),
  lotNo: hasAnyOutcomeColumnValue(awardRows.value, outcomeLotNo),
  lotName: hasAnyOutcomeColumnValue(awardRows.value, outcomeLotName),
  packageNo: hasAnyOutcomeColumnValue(awardRows.value, outcomePackageNo),
  outcomeType: hasAnyOutcomeColumnValue(awardRows.value, (row) => formatCommonStatus(row.outcomeType)),
}))
const candidateColumnVisible = computed(() => ({
  projectCode: hasAnyOutcomeColumnValue(candidateRows.value, outcomeProjectCode),
  projectName: hasAnyOutcomeColumnValue(candidateRows.value, outcomeProjectName),
  lotNo: hasAnyOutcomeColumnValue(candidateRows.value, outcomeLotNo),
  lotName: hasAnyOutcomeColumnValue(candidateRows.value, outcomeLotName),
  packageNo: hasAnyOutcomeColumnValue(candidateRows.value, outcomePackageNo),
  packageName: hasAnyOutcomeColumnValue(candidateRows.value, outcomePackageName),
  rank: hasAnyOutcomeColumnValue(candidateRows.value, (row) => formatRank(row.rank)),
  awardAmount: hasAnyOutcomeColumnValue(candidateRows.value, (row) => formatMoney(row.awardAmount)),
  evidenceText: hasAnyOutcomeColumnValue(candidateRows.value, outcomeReviewSummary),
}))
const procurementPackages = computed(() =>
  packages.value
    .slice()
    .sort((left, right) => compareText(packageSortKey(left), packageSortKey(right))),
)
const canReparseRawNotice = computed(() =>
  Boolean(rawNotice.value && !isApprovedRawNoticeStatus(rawNotice.value.status) && isEditableReviewTaskStatus(task.value?.status)),
)
const canAdjustProcurementAi = computed(() => canReparseRawNotice.value && noticeKind.value === 'procurement')
const canAdjustOutcomeAi = computed(() => Boolean(notice.value) && (noticeKind.value === 'award' || noticeKind.value === 'candidate'))
const canEditOutcomeRows = computed(() =>
  Boolean(notice.value && rawNotice.value && !isApprovedRawNoticeStatus(rawNotice.value.status) && isEditableReviewTaskStatus(task.value?.status)),
)
const showGenericOutcomeLeads = computed(() => noticeKind.value === 'other' && outcomeSuppliers.value.length > 0)
const outcomeTypeOptions = [
  { label: '中标/成交', value: 'Awarded' },
  { label: '候选', value: 'Candidate' },
  { label: '入围', value: 'Shortlisted' },
]

async function loadData() {
  loading.value = true
  try {
    detail.value = await reviewTasksApi.get(taskId.value)
  } catch {
    detail.value = null
    backgroundJobs.value = []
    backgroundJobTotal.value = 0
  } finally {
    loading.value = false
  }

  if (detail.value) {
    await loadBackgroundJobs()
  }
}

async function loadBackgroundJobs() {
  if (!taskId.value) return

  try {
    const result = await backgroundJobsRequest.run(() =>
      reviewTasksApi.jobs(taskId.value, {
        pageIndex: 1,
        pageSize: 20,
      }),
    )
    backgroundJobs.value = result.items || []
    backgroundJobTotal.value = Number(result.total || 0)
  } catch {
    backgroundJobs.value = []
    backgroundJobTotal.value = 0
  }
}

function confidencePercent(value?: number | null) {
  return `${Math.round(Number(value || 0) * 100)}%`
}

function qualityScoreValue() {
  return Number.isFinite(Number(task.value?.qualityScore)) ? Number(task.value?.qualityScore) : 100
}

function projectTitle() {
  return notice.value?.projectName || task.value?.projectName || task.value?.taskTitle || '-'
}

function keyDeadline() {
  return notice.value?.bidDeadline || notice.value?.openBidTime || notice.value?.signupDeadline || notice.value?.publishTime
}

function keyDeadlineLabel() {
  if (notice.value?.bidDeadline) return '投标截止'
  if (notice.value?.openBidTime) return '开标时间'
  if (notice.value?.signupDeadline) return '报名截止'
  return '发布时间'
}

function packageTitle(pkg: PackageStagingDto) {
  return pkg.packageName || formatPackageNo(pkg.packageNo) || pkg.lotName || '未命名包件'
}

function packageSubtitle(pkg: PackageStagingDto) {
  return [pkg.lotName || '未分标段', formatCategory(pkg.category)]
    .filter((item) => item && item !== '-')
    .join(' · ')
}

function packageRequirements(pkg: PackageStagingDto): RequirementStagingDto[] {
  return pkg.requirements || []
}

function issueTarget(issue: ReviewQualityIssueDto) {
  if (issue.packageStagingId) {
    const pkg = packages.value.find((item) => String(item.id) === String(issue.packageStagingId))
    if (pkg) {
      return [pkg.lotNo, formatPackageNo(pkg.packageNo), pkg.packageName]
        .filter((item) => item && item !== '-')
        .join(' / ')
    }
  }

  if (issue.procurementDetailStagingId) return `采购明细 ${issue.procurementDetailStagingId}`
  if (issue.outcomeSupplierRecordId) return `中标线索 ${issue.outcomeSupplierRecordId}`
  return issue.fieldName || '-'
}

function organizationState(exists?: boolean, willCreate?: boolean) {
  if (exists) return '已存在'
  if (willCreate) return '审核通过后创建'
  return '待补录'
}

function supplierState(row: OutcomeSupplierRecordDto) {
  return row.supplierId ? '已关联厂家' : '审核通过后创建/关联'
}

function isEditableReviewTaskStatus(status?: unknown) {
  const value = String(status ?? '').trim()
  return !['2', '3', '4', 'Approved', 'Ignored', 'Merged'].includes(value)
}

function isApprovedRawNoticeStatus(status?: unknown) {
  return status === 3 || status === '3' || status === 'Approved'
}

function detectNoticeKind() {
  const type = String(notice.value?.noticeType || task.value?.noticeType || rawNotice.value?.noticeType || '')
  const signal = [
    type,
    notice.value?.projectName,
    task.value?.taskTitle,
    rawNotice.value?.title,
    rawText.value.slice(0, 600),
  ].join(' ')

  if (type === 'CandidateAnnouncement' || /推荐.*(?:中标|成交)候选人|(?:中标|成交)候选人/.test(signal)) {
    return 'candidate'
  }

  if (
    type === 'AwardAnnouncement' ||
    type === 'ResultAnnouncement' ||
    /(?:中标|成交)(?:结果)?公告|(?:中标|成交)结果/.test(signal)
  ) {
    return 'award'
  }

  if (
    type === 'ProcurementAnnouncement' ||
    type === 'TenderAnnouncement' ||
    /(?:采购|招标)公告|竞争性谈判|询价采购|公开招标/.test(signal)
  ) {
    return 'procurement'
  }

  return 'other'
}

function formatRank(value?: number | null) {
  return value ? String(value) : '-'
}

function outcomeProjectCode(row: OutcomeSupplierRecordDto) {
  return displayText(row.projectCode, notice.value?.projectCode, task.value?.projectCode)
}

function outcomeProjectName(row: OutcomeSupplierRecordDto) {
  return displayText(row.projectName)
}

function outcomeLotNo(row: OutcomeSupplierRecordDto) {
  const pkg = matchedPackageForOutcome(row)
  return displayText(row.lotNo, pkg?.lotNo)
}

function outcomeLotName(row: OutcomeSupplierRecordDto) {
  const pkg = matchedPackageForOutcome(row)
  return displayText(row.lotName, pkg?.lotName)
}

function outcomePackageNo(row: OutcomeSupplierRecordDto) {
  const pkg = matchedPackageForOutcome(row)
  return formatPackageNo(displayText(row.packageNo, pkg?.packageNo))
}

function outcomePackageName(row: OutcomeSupplierRecordDto) {
  const pkg = matchedPackageForOutcome(row)
  return displayText(row.packageName, pkg?.packageName)
}

function outcomeReviewSummary(row: OutcomeSupplierRecordDto) {
  return displayText(row.evidenceText)
}

function hasAnyOutcomeColumnValue(
  rows: OutcomeSupplierRecordDto[],
  getValue: (row: OutcomeSupplierRecordDto) => unknown,
) {
  if (rows.length === 0) return true
  return rows.some((row) => hasDisplayValue(getValue(row)))
}

function hasDisplayValue(value: unknown) {
  if (value === null || value === undefined) return false
  const text = normalizeText(value)
  return Boolean(text && text !== '-' && text !== '待补录')
}

function matchedPackageForOutcome(row: OutcomeSupplierRecordDto) {
  const packageNo = normalizeCode(row.packageNo)
  const lotNo = normalizeCode(row.lotNo)
  const lotName = normalizeText(row.lotName)
  const packageName = normalizeText(row.packageName)

  if (packageNo && lotNo) {
    return packages.value.find((pkg) => normalizeCode(pkg.packageNo) === packageNo && normalizeCode(pkg.lotNo) === lotNo)
  }

  if (packageNo && lotName) {
    return uniquePackageMatch((pkg) => normalizeCode(pkg.packageNo) === packageNo && normalizeText(pkg.lotName) === lotName)
  }

  if (lotNo) {
    return uniquePackageMatch((pkg) => normalizeCode(pkg.lotNo) === lotNo)
  }

  if (packageName) {
    return uniquePackageMatch((pkg) => {
      const candidate = normalizeText(pkg.packageName)
      return Boolean(candidate && (candidate.includes(packageName) || packageName.includes(candidate)))
    })
  }

  return undefined
}

function uniquePackageMatch(predicate: (pkg: PackageStagingDto) => boolean) {
  const matches = packages.value.filter(predicate)
  return matches.length === 1 ? matches[0] : undefined
}

function packageSortKey(pkg: PackageStagingDto) {
  return [normalizeCode(pkg.lotNo), normalizeCode(pkg.packageNo), normalizeText(pkg.packageName)].join('|')
}

function compareText(left: string, right: string) {
  return left.localeCompare(right, 'zh-Hans-CN', { numeric: true, sensitivity: 'base' })
}

function normalizeCode(value?: string | null) {
  return normalizeText(value).replace(/[\s:：,，;；]/g, '').toUpperCase()
}

function normalizeText(value?: unknown) {
  return String(value || '').trim()
}

function displayText(...values: Array<string | null | undefined>) {
  for (const value of values) {
    const text = normalizeText(value)
    if (text && text !== '-' && text !== '待补录') return text
  }

  return '-'
}

function createEmptyOutcomeForm(): OutcomeEditForm {
  return {
    projectName: '',
    projectCode: '',
    buyerName: '',
    lotNo: '',
    lotName: '',
    packageNo: '',
    packageName: '',
    category: '',
    supplierName: '',
    outcomeType: 'Candidate',
    rank: null,
    awardAmount: null,
    procurementAgencyServiceFeeAmount: null,
    evidenceText: '',
  }
}

function openJob(jobId: string) {
  if (!jobId) return
  void router.push(`/bidops/operations/jobs/${jobId}`)
}

function jobDiagnosticPreview(row: BackgroundJobListItemDto) {
  return row.lastErrorPreview || row.resultPreview || '-'
}

function defaultOutcomeType() {
  return noticeKind.value === 'award' ? 'Awarded' : 'Candidate'
}

function openCreateOutcome() {
  if (!canEditOutcomeRows.value) return

  outcomeEditMode.value = 'create'
  editingOutcomeId.value = ''
  outcomeEditForm.value = {
    ...createEmptyOutcomeForm(),
    projectName: '',
    projectCode: notice.value?.projectCode || task.value?.projectCode || '',
    buyerName: notice.value?.buyerName || task.value?.buyerName || '',
    outcomeType: defaultOutcomeType(),
  }
  outcomeEditVisible.value = true
}

function openEditOutcome(row: OutcomeSupplierRecordDto) {
  if (!canEditOutcomeRows.value) return

  const id = String(row.id || '')
  editingOutcomeId.value = id && id !== '0' ? id : ''
  outcomeEditMode.value = editingOutcomeId.value ? 'edit' : 'create'
  outcomeEditForm.value = {
    projectName: row.projectName || '',
    projectCode: row.projectCode || notice.value?.projectCode || task.value?.projectCode || '',
    buyerName: row.buyerName || notice.value?.buyerName || task.value?.buyerName || '',
    lotNo: row.lotNo || '',
    lotName: row.lotName || '',
    packageNo: row.packageNo || '',
    packageName: row.packageName || '',
    category: row.category || '',
    supplierName: row.supplierName || '',
    outcomeType: row.outcomeType || defaultOutcomeType(),
    rank: row.rank ?? null,
    awardAmount: row.awardAmount ?? null,
    procurementAgencyServiceFeeAmount: row.procurementAgencyServiceFeeAmount ?? null,
    evidenceText: row.evidenceText || '',
  }
  outcomeEditVisible.value = true
}

async function submitOutcomeEdit() {
  const form = outcomeEditForm.value
  if (!form.supplierName.trim()) {
    ElMessage.warning('请填写厂家名称')
    return
  }

  const payload = buildOutcomeEditPayload(form)
  await outcomeEditRequest.run(() =>
    editingOutcomeId.value
      ? reviewTasksApi.updateOutcomeSupplier(taskId.value, editingOutcomeId.value, payload)
      : reviewTasksApi.addOutcomeSupplier(taskId.value, payload),
  )
  ElMessage.success(editingOutcomeId.value ? '明细已更新' : '明细已新增')
  outcomeEditVisible.value = false
  await loadData()
}

async function deleteOutcome(row: OutcomeSupplierRecordDto) {
  const id = String(row.id || '')
  if (!canEditOutcomeRows.value || !id || id === '0') return

  await ElMessageBox.confirm(`确认删除“${row.supplierName || '该厂家'}”这条解析明细？`, '删除解析明细', {
    type: 'warning',
    confirmButtonText: '删除',
    cancelButtonText: '取消',
  })
  await outcomeEditRequest.run(() => reviewTasksApi.deleteOutcomeSupplier(taskId.value, id))
  ElMessage.success('明细已删除')
  await loadData()
}

function buildOutcomeEditPayload(form: OutcomeEditForm): ReviewOutcomeSupplierRecordEditRequest {
  return {
    projectName: form.projectName.trim(),
    projectCode: form.projectCode.trim(),
    buyerName: form.buyerName.trim(),
    lotNo: form.lotNo.trim(),
    lotName: form.lotName.trim(),
    packageNo: form.packageNo.trim(),
    packageName: form.packageName.trim(),
    category: form.category.trim(),
    supplierName: form.supplierName.trim(),
    outcomeType: form.outcomeType,
    rank: form.rank,
    awardAmount: form.awardAmount,
    procurementAgencyServiceFeeAmount: form.procurementAgencyServiceFeeAmount,
    evidenceText: form.evidenceText.trim(),
  }
}

async function submitOutcomeAiReparse() {
  const prompt = outcomeAiPrompt.value.trim()
  if (!prompt) {
    ElMessage.warning('请输入给 AI 的调整提示词')
    return
  }

  const job = await outcomeAiReparseRequest.run(() =>
    reviewTasksApi.outcomeAiReparse(taskId.value, { prompt }),
  )
  outcomeAiJobId.value = String(job.jobId || '')
  ElMessage.success(`已提交 AI 重新解析任务：${outcomeAiJobId.value}`)
  await loadBackgroundJobs()
  scheduleDetailRefresh()
}

async function submitProcurementAiReparse() {
  if (!rawNotice.value || !canReparseRawNotice.value) {
    ElMessage.warning('已入库或已完成审核的公告不能重新解析')
    return
  }

  const prompt = procurementAiPrompt.value.trim()
  if (!prompt) {
    ElMessage.warning('请输入给 AI 的调整提示词')
    return
  }

  const job = await rawReparseRequest.run(() =>
    rawNoticesApi.reparse(rawNotice.value!.id, {
      reason: 'Review task procurement AI prompt',
      prompt,
    }),
  )
  rawReparseJobId.value = String(job.jobId || '')
  ElMessage.success(job.alreadyExists ? `重解析任务已存在：${rawReparseJobId.value}` : `已提交 AI 重新解析任务：${rawReparseJobId.value}`)
  await loadBackgroundJobs()
  scheduleDetailRefresh()
}

async function reparseRawNotice() {
  if (!rawNotice.value || !canReparseRawNotice.value) {
    ElMessage.warning('已入库或已完成审核的公告不能重新解析')
    return
  }

  const job = await rawReparseRequest.run(() =>
    rawNoticesApi.reparse(rawNotice.value!.id, { reason: 'Review task detail page' }),
  )
  rawReparseJobId.value = String(job.jobId || '')
  ElMessage.success(job.alreadyExists ? `重解析任务已存在：${rawReparseJobId.value}` : `已提交重解析任务：${rawReparseJobId.value}`)
  await loadBackgroundJobs()
  scheduleDetailRefresh()
}

function scheduleDetailRefresh() {
  detailRefreshAttempts.value = 0
  queueDetailRefresh()
}

function queueDetailRefresh() {
  if (detailRefreshTimer) {
    window.clearTimeout(detailRefreshTimer)
  }

  detailRefreshTimer = window.setTimeout(async () => {
    detailRefreshAttempts.value += 1
    await loadData()
    if (detailRefreshAttempts.value < 4) {
      queueDetailRefresh()
    }
  }, 4000)
}

async function approve(remark: string) {
  try {
    await ElMessageBox.confirm('审核通过后将创建正式公告、包件、要求项，并同步采购方/厂家主数据与中标关联。', '审核通过', {
      confirmButtonText: '通过',
      cancelButtonText: '取消',
      type: 'warning',
    })
    const notice = await decisionRequest.run(() => reviewTasksApi.approve(taskId.value, { remark }))
    ElMessage.success(`已生成正式公告：${notice?.id ?? '-'}`)
    await router.push('/bidops/notices')
  } catch {
    return
  }
}

async function ignore(remark: string) {
  try {
    await ElMessageBox.confirm('忽略后该任务不会进入正式业务表。', '忽略审核任务', {
      confirmButtonText: '忽略',
      cancelButtonText: '取消',
      type: 'warning',
    })
    await decisionRequest.run(() => reviewTasksApi.ignore(taskId.value, { remark }))
    ElMessage.success('审核任务已忽略')
    await router.push('/bidops/review/tasks')
  } catch {
    return
  }
}

onMounted(loadData)

onUnmounted(() => {
  if (detailRefreshTimer) {
    window.clearTimeout(detailRefreshTimer)
  }
})
</script>

<template>
  <PageContainer title="审核详情" description="核对公告原文、解析字段、包件和要求项；确认无误后才写入正式业务表。">
    <template #actions>
      <PermissionButton
        v-if="canReparseRawNotice"
        :icon="RefreshRight"
        :loading="rawReparseRequest.loading"
        :permission="BIDOPS_PERMISSIONS.REVIEW_APPROVE"
        @click="reparseRawNotice"
      >
        重新解析
      </PermissionButton>
    </template>

    <el-skeleton v-if="loading" :rows="12" animated />
    <el-empty v-else-if="!detail" description="未找到审核任务" />
    <template v-else>
      <section class="review-summary">
        <div class="summary-main">
          <h2>{{ projectTitle() }}</h2>
          <div class="summary-tags">
            <BidOpsStatusTag :value="task?.status" kind="reviewTask" />
            <el-tag effect="light">{{ formatNoticeType(notice?.noticeType || task?.noticeType) }}</el-tag>
            <el-tag v-if="notice?.region || task?.region" type="success" effect="light">{{ notice?.region || task?.region }}</el-tag>
            <el-tag v-if="rejectRiskCount > 0" type="danger" effect="light">{{ rejectRiskCount }} 条废标风险</el-tag>
          </div>
        </div>
        <div class="summary-grid">
          <div>
            <span>采购编号</span>
            <strong>{{ notice?.projectCode || task?.projectCode || '-' }}</strong>
          </div>
          <div>
            <span>采购人</span>
            <strong>{{ notice?.buyerName || task?.buyerName || '-' }}</strong>
          </div>
          <div>
            <span>{{ keyDeadlineLabel() }}</span>
            <strong>{{ formatDateTime(keyDeadline()) }}</strong>
          </div>
          <div>
            <span>包件 / 要求</span>
            <strong>{{ packages.length }} / {{ allRequirements.length }}</strong>
          </div>
          <div>
            <span>强制 / 风险</span>
            <strong>{{ mandatoryCount }} / {{ rejectRiskCount }}</strong>
          </div>
          <div>
            <span>解析置信度</span>
            <strong>{{ confidencePercent(notice?.aiConfidence || task?.aiConfidence) }}</strong>
          </div>
        </div>
      </section>

      <section class="quality-review-panel">
        <div class="panel-heading">
          <h2>异常复核</h2>
          <span>{{ activeQualityIssues.length > 0 ? '按风险优先处理异常项' : '未发现未解决异常' }}</span>
        </div>
        <div class="quality-summary-grid">
          <div>
            <span>质量分</span>
            <strong>{{ qualityScoreValue() }}</strong>
          </div>
          <div>
            <span>风险等级</span>
            <RiskLevelTag :value="task?.riskLevel" />
          </div>
          <div>
            <span>异常项</span>
            <strong>{{ task?.qualityIssueCount || 0 }} / 高风险 {{ task?.highRiskIssueCount || 0 }}</strong>
          </div>
          <div>
            <span>推荐动作</span>
            <strong>{{ formatReviewRecommendation(task?.reviewRecommendation) }}</strong>
          </div>
        </div>
        <el-alert
          v-if="activeQualityIssues.length === 0"
          type="success"
          show-icon
          :closable="false"
          title="当前解析结果是低风险复核候选；仍需人工确认后才能写入正式业务表。"
        />
        <el-table v-else :data="activeQualityIssues" border size="small" empty-text="没有未解决异常">
          <el-table-column label="等级" width="90">
            <template #default="{ row }"><RiskLevelTag :value="row.severity" /></template>
          </el-table-column>
          <el-table-column label="异常类型" min-width="150" show-overflow-tooltip>
            <template #default="{ row }">{{ formatQualityIssueType(row.issueType) }}</template>
          </el-table-column>
          <el-table-column prop="fieldName" label="字段" min-width="120" show-overflow-tooltip />
          <el-table-column label="对象" min-width="220" show-overflow-tooltip>
            <template #default="{ row }">{{ issueTarget(row) }}</template>
          </el-table-column>
          <el-table-column prop="message" label="复核说明" min-width="320" show-overflow-tooltip />
        </el-table>
      </section>

      <section class="background-jobs-panel">
        <div class="panel-heading">
          <h2>本审核发起的后台任务</h2>
          <div class="background-job-actions">
            <span>共 {{ backgroundJobTotal }} 个</span>
            <el-button size="small" :icon="RefreshRight" :loading="backgroundJobsRequest.loading" @click="loadBackgroundJobs">
              刷新任务
            </el-button>
          </div>
        </div>
        <el-table
          v-loading="backgroundJobsRequest.loading"
          :data="backgroundJobs"
          border
          size="small"
          empty-text="当前审核页还没有发起过后台任务"
        >
          <el-table-column prop="id" label="任务ID" width="150" />
          <el-table-column label="任务类型" min-width="190" show-overflow-tooltip>
            <template #default="{ row }">{{ formatJobType(row.jobType, row.jobTypeName) }}</template>
          </el-table-column>
          <el-table-column label="状态" width="120">
            <template #default="{ row }">
              <JobStatusTag
                :status="row.status"
                :status-name="row.statusName"
                :cancellation-requested="row.isCancellationRequested"
              />
            </template>
          </el-table-column>
          <el-table-column label="创建时间" width="170">
            <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
          </el-table-column>
          <el-table-column label="完成时间" width="170">
            <template #default="{ row }">{{ formatDateTime(row.completedAt) }}</template>
          </el-table-column>
          <el-table-column label="耗时" width="100">
            <template #default="{ row }">{{ formatDuration(row.runMilliseconds, row.runSeconds) }}</template>
          </el-table-column>
          <el-table-column label="诊断摘要" min-width="260" show-overflow-tooltip>
            <template #default="{ row }">{{ jobDiagnosticPreview(row) }}</template>
          </el-table-column>
          <el-table-column label="操作" width="100" fixed="right">
            <template #default="{ row }">
              <el-button link type="primary" size="small" :icon="View" @click="openJob(String(row.id || ''))">
                详情
              </el-button>
            </template>
          </el-table-column>
        </el-table>
      </section>

      <div class="split-grid">
        <section class="content-panel">
          <div class="panel-heading">
            <h2>公告证据</h2>
            <el-link v-if="rawNotice?.detailUrl" :href="rawNotice.detailUrl" target="_blank" type="primary">打开原公告</el-link>
          </div>
          <el-descriptions :column="1" border>
            <el-descriptions-item label="原始标题">{{ rawNotice?.title || '-' }}</el-descriptions-item>
            <el-descriptions-item label="公告类型">{{ formatNoticeType(rawNotice?.noticeType) }}</el-descriptions-item>
            <el-descriptions-item label="发布时间">{{ formatDateTime(rawNotice?.publishTime) }}</el-descriptions-item>
            <el-descriptions-item label="采集时间">{{ formatDateTime(rawNotice?.fetchTime) }}</el-descriptions-item>
            <el-descriptions-item label="最后更新时间">{{ formatDateTime(rawNotice?.updatedAt || rawNotice?.createdAt) }}</el-descriptions-item>
            <el-descriptions-item label="采集状态"><BidOpsStatusTag :value="rawNotice?.status" kind="rawNotice" /></el-descriptions-item>
            <el-descriptions-item label="错误信息">{{ rawNotice?.lastError || '-' }}</el-descriptions-item>
          </el-descriptions>

          <h2 class="section-title">公开附件</h2>
          <RawAttachmentTable :attachments="attachments" :raw-notice-id="rawNotice?.id" />

          <h2 class="section-title">公告全文</h2>
          <RawNoticePreview :text="rawText" />
        </section>

        <section class="content-panel">
          <div class="panel-heading">
            <h2>解析结果</h2>
            <el-link v-if="rawReparseJobId" type="primary" @click="openJob(rawReparseJobId)">
              重解析任务 {{ rawReparseJobId }}
            </el-link>
            <span v-else>审核后写入正式公告、包件、要求项，并同步采购方/厂家关联</span>
          </div>
          <el-alert
            v-if="!notice"
            type="warning"
            show-icon
            :closable="false"
            title="该任务没有关联到暂存公告，不能直接审核入库。"
          />
          <el-descriptions v-else :column="2" border>
            <el-descriptions-item label="项目名称" :span="2">{{ notice.projectName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="采购编号">{{ notice.projectCode || '-' }}</el-descriptions-item>
            <el-descriptions-item label="地区">{{ notice.region || '-' }}</el-descriptions-item>
            <el-descriptions-item label="采购人">{{ notice.buyerName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="代理机构">{{ notice.agencyName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="预算金额">{{ formatMoney(notice.budgetAmount) }}</el-descriptions-item>
            <el-descriptions-item label="发布时间">{{ formatDateTime(notice.publishTime) }}</el-descriptions-item>
            <el-descriptions-item label="报名截止">{{ formatDateTime(notice.signupDeadline) }}</el-descriptions-item>
            <el-descriptions-item label="投标截止">{{ formatDateTime(notice.bidDeadline) }}</el-descriptions-item>
            <el-descriptions-item label="开标时间">{{ formatDateTime(notice.openBidTime) }}</el-descriptions-item>
            <el-descriptions-item label="置信度">{{ confidencePercent(notice.aiConfidence) }}</el-descriptions-item>
            <el-descriptions-item label="复核状态"><BidOpsStatusTag :value="notice.reviewStatus" kind="review" /></el-descriptions-item>
          </el-descriptions>

          <section v-if="canAdjustOutcomeAi" class="ai-reparse-panel">
            <div class="ai-reparse-heading">
              <div>
                <h2>AI 解析调整</h2>
                <p>通过补充提示词重跑当前公告的中标/候选明细。</p>
              </div>
              <el-link v-if="outcomeAiJobId" type="primary" @click="openJob(outcomeAiJobId)">
                任务 {{ outcomeAiJobId }}
              </el-link>
            </div>
            <el-input
              v-model="outcomeAiPrompt"
              type="textarea"
              :rows="4"
              maxlength="4000"
              show-word-limit
              placeholder="例如：采购编号在正文“采购编号：XXXX”中；表格第一列是包号，第二列是推荐的成交候选人；最终报价只有表头明确写万元才换算，未标单位按元；不要把分标编号当采购编号。"
            />
            <div class="ai-reparse-actions">
              <el-button type="primary" :loading="outcomeAiReparseRequest.loading" @click="submitOutcomeAiReparse">
                让 AI 重新解析
              </el-button>
              <el-button :disabled="loading" @click="loadData">刷新数据</el-button>
            </div>
          </section>

          <section v-if="canAdjustProcurementAi" class="ai-reparse-panel">
            <div class="ai-reparse-heading">
              <div>
                <h2>AI 解析调整</h2>
                <p>通过补充提示词重跑当前采购公告的公告字段、包件、金额和资格要求。</p>
              </div>
              <el-link v-if="rawReparseJobId" type="primary" @click="openJob(rawReparseJobId)">
                任务 {{ rawReparseJobId }}
              </el-link>
            </div>
            <el-input
              v-model="procurementAiPrompt"
              type="textarea"
              :rows="4"
              maxlength="4000"
              show-word-limit
              placeholder="例如：以附件《采购一览表》为准；行报价最高限价列名含万元，需要乘以10000；资质、业绩、人员要求来自响应供应商专用资格要求表。"
            />
            <div class="ai-reparse-actions">
              <el-button type="primary" :loading="rawReparseRequest.loading" @click="submitProcurementAiReparse">
                让 AI 重新解析
              </el-button>
              <el-button :disabled="loading" @click="loadData">刷新数据</el-button>
            </div>
          </section>

          <template v-if="noticeKind === 'award'">
            <div class="section-heading">
              <h2 class="section-title">中标/成交明细</h2>
              <PermissionButton
                v-if="canEditOutcomeRows"
                size="small"
                type="primary"
                plain
                :icon="Plus"
                :permission="BIDOPS_PERMISSIONS.REVIEW_APPROVE"
                @click="openCreateOutcome"
              >
                新增明细
              </PermissionButton>
            </div>
            <el-table :data="awardRows" border size="small" empty-text="未识别到中标/成交明细">
              <el-table-column v-if="awardColumnVisible.projectCode" label="采购编号" min-width="150" show-overflow-tooltip>
                <template #default="{ row }">{{ outcomeProjectCode(row) }}</template>
              </el-table-column>
              <el-table-column v-if="awardColumnVisible.projectName" label="项目名称" min-width="220" show-overflow-tooltip>
                <template #default="{ row }">{{ outcomeProjectName(row) }}</template>
              </el-table-column>
              <el-table-column v-if="awardColumnVisible.lotNo" label="分标编号" min-width="150" show-overflow-tooltip>
                <template #default="{ row }">{{ outcomeLotNo(row) }}</template>
              </el-table-column>
              <el-table-column v-if="awardColumnVisible.lotName" label="分标名称" min-width="150" show-overflow-tooltip>
                <template #default="{ row }">{{ outcomeLotName(row) }}</template>
              </el-table-column>
              <el-table-column v-if="awardColumnVisible.packageNo" label="包号" width="110" show-overflow-tooltip>
                <template #default="{ row }">{{ outcomePackageNo(row) }}</template>
              </el-table-column>
              <el-table-column v-if="awardColumnVisible.outcomeType" label="中标状态" width="110">
                <template #default="{ row }">{{ formatCommonStatus(row.outcomeType) }}</template>
              </el-table-column>
              <el-table-column prop="supplierName" label="成交供应商" min-width="210" show-overflow-tooltip />
              <el-table-column v-if="canEditOutcomeRows" label="操作" width="140" fixed="right">
                <template #default="{ row }">
                  <PermissionButton
                    text
                    size="small"
                    :icon="Edit"
                    :permission="BIDOPS_PERMISSIONS.REVIEW_APPROVE"
                    @click="openEditOutcome(row)"
                  >
                    编辑
                  </PermissionButton>
                  <PermissionButton
                    v-if="String(row.id || '') !== '0'"
                    text
                    size="small"
                    type="danger"
                    :icon="Delete"
                    :permission="BIDOPS_PERMISSIONS.REVIEW_APPROVE"
                    @click="deleteOutcome(row)"
                  >
                    删除
                  </PermissionButton>
                </template>
              </el-table-column>
            </el-table>
          </template>

          <template v-else-if="noticeKind === 'candidate'">
            <div class="section-heading">
              <h2 class="section-title">候选人明细</h2>
              <PermissionButton
                v-if="canEditOutcomeRows"
                size="small"
                type="primary"
                plain
                :icon="Plus"
                :permission="BIDOPS_PERMISSIONS.REVIEW_APPROVE"
                @click="openCreateOutcome"
              >
                新增明细
              </PermissionButton>
            </div>
            <el-table :data="candidateRows" border size="small" empty-text="未识别到候选人明细">
              <el-table-column v-if="candidateColumnVisible.projectCode" label="采购编号" min-width="150" show-overflow-tooltip>
                <template #default="{ row }">{{ outcomeProjectCode(row) }}</template>
              </el-table-column>
              <el-table-column v-if="candidateColumnVisible.projectName" label="项目名称" min-width="220" show-overflow-tooltip>
                <template #default="{ row }">{{ outcomeProjectName(row) }}</template>
              </el-table-column>
              <el-table-column v-if="candidateColumnVisible.lotNo" label="分标编号" min-width="150" show-overflow-tooltip>
                <template #default="{ row }">{{ outcomeLotNo(row) }}</template>
              </el-table-column>
              <el-table-column v-if="candidateColumnVisible.lotName" label="分标名称" min-width="150" show-overflow-tooltip>
                <template #default="{ row }">{{ outcomeLotName(row) }}</template>
              </el-table-column>
              <el-table-column v-if="candidateColumnVisible.packageNo" label="包号" width="110" show-overflow-tooltip>
                <template #default="{ row }">{{ outcomePackageNo(row) }}</template>
              </el-table-column>
              <el-table-column v-if="candidateColumnVisible.packageName" label="包名称" min-width="190" show-overflow-tooltip>
                <template #default="{ row }">{{ outcomePackageName(row) }}</template>
              </el-table-column>
              <el-table-column v-if="candidateColumnVisible.rank" label="排名" width="90">
                <template #default="{ row }">{{ formatRank(row.rank) }}</template>
              </el-table-column>
              <el-table-column prop="supplierName" label="推荐的成交候选人" min-width="210" show-overflow-tooltip />
              <el-table-column v-if="candidateColumnVisible.awardAmount" label="最终报价（元）" width="150" align="right">
                <template #default="{ row }">{{ formatMoney(row.awardAmount) }}</template>
              </el-table-column>
              <el-table-column v-if="candidateColumnVisible.evidenceText" label="评审情况" min-width="260" show-overflow-tooltip>
                <template #default="{ row }">{{ outcomeReviewSummary(row) }}</template>
              </el-table-column>
              <el-table-column v-if="canEditOutcomeRows" label="操作" width="140" fixed="right">
                <template #default="{ row }">
                  <PermissionButton
                    text
                    size="small"
                    :icon="Edit"
                    :permission="BIDOPS_PERMISSIONS.REVIEW_APPROVE"
                    @click="openEditOutcome(row)"
                  >
                    编辑
                  </PermissionButton>
                  <PermissionButton
                    v-if="String(row.id || '') !== '0'"
                    text
                    size="small"
                    type="danger"
                    :icon="Delete"
                    :permission="BIDOPS_PERMISSIONS.REVIEW_APPROVE"
                    @click="deleteOutcome(row)"
                  >
                    删除
                  </PermissionButton>
                </template>
              </el-table-column>
            </el-table>

            <h2 class="section-title">对应包件明细</h2>
            <el-empty v-if="procurementPackages.length === 0" description="没有解析到对应包件" />
            <template v-else>
              <el-table :data="procurementPackages" border size="small" empty-text="没有解析到对应包件">
                <el-table-column label="分标编号" min-width="130" show-overflow-tooltip>
                  <template #default="{ row }">{{ row.lotNo || '-' }}</template>
                </el-table-column>
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
              <section v-for="pkg in procurementPackages" :key="pkg.id" class="detail-module">
                <div class="module-heading">
                  <div>
                    <h3>{{ packageTitle(pkg) }}</h3>
                    <p>{{ packageSubtitle(pkg) }}</p>
                  </div>
                  <el-tag effect="light">置信度 {{ confidencePercent(pkg.aiConfidence) }}</el-tag>
                </div>
                <el-descriptions :column="2" border>
                  <el-descriptions-item label="分标编号">{{ pkg.lotNo || '-' }}</el-descriptions-item>
                  <el-descriptions-item label="分标名称">{{ pkg.lotName || '-' }}</el-descriptions-item>
                  <el-descriptions-item label="包号">{{ formatPackageNo(pkg.packageNo) }}</el-descriptions-item>
                  <el-descriptions-item label="包名称">{{ pkg.packageName || '-' }}</el-descriptions-item>
                  <el-descriptions-item label="品类">{{ formatCategory(pkg.category) }}</el-descriptions-item>
                  <el-descriptions-item label="预算">{{ formatMoney(pkg.budgetAmount) }}</el-descriptions-item>
                  <el-descriptions-item label="复核状态"><BidOpsStatusTag :value="pkg.reviewStatus" kind="review" /></el-descriptions-item>
                </el-descriptions>
                <h3 class="requirements-title">资格 / 商务 / 风险要求</h3>
                <RequirementTable :requirements="packageRequirements(pkg)" class="requirements" />
              </section>
            </template>
          </template>

          <template v-else-if="noticeKind === 'procurement'">
            <h2 class="section-title">采购公告明细</h2>
            <el-empty v-if="procurementPackages.length === 0" description="没有解析到采购包件" />
            <template v-else>
              <el-table :data="procurementPackages" border size="small" empty-text="没有解析到采购包件">
                <el-table-column label="分标编号" min-width="130" show-overflow-tooltip>
                  <template #default="{ row }">{{ row.lotNo || '-' }}</template>
                </el-table-column>
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
              <section v-for="pkg in procurementPackages" :key="pkg.id" class="detail-module">
                <div class="module-heading">
                  <div>
                    <h3>{{ packageTitle(pkg) }}</h3>
                    <p>{{ packageSubtitle(pkg) }}</p>
                  </div>
                  <el-tag effect="light">置信度 {{ confidencePercent(pkg.aiConfidence) }}</el-tag>
                </div>
                <el-descriptions :column="2" border>
                  <el-descriptions-item label="分标编号">{{ pkg.lotNo || '-' }}</el-descriptions-item>
                  <el-descriptions-item label="分标名称">{{ pkg.lotName || '-' }}</el-descriptions-item>
                  <el-descriptions-item label="包号">{{ formatPackageNo(pkg.packageNo) }}</el-descriptions-item>
                  <el-descriptions-item label="包名称">{{ pkg.packageName || '-' }}</el-descriptions-item>
                  <el-descriptions-item label="品类">{{ formatCategory(pkg.category) }}</el-descriptions-item>
                  <el-descriptions-item label="预算">{{ formatMoney(pkg.budgetAmount) }}</el-descriptions-item>
                  <el-descriptions-item label="复核状态"><BidOpsStatusTag :value="pkg.reviewStatus" kind="review" /></el-descriptions-item>
                </el-descriptions>
                <h3 class="requirements-title">资格 / 商务 / 风险要求</h3>
                <RequirementTable :requirements="packageRequirements(pkg)" class="requirements" />
              </section>
            </template>
          </template>

          <h2 class="section-title">采购方</h2>
          <div class="org-review">
            <section class="org-block">
              <div class="org-block-heading">
                <h3>采购方</h3>
                <el-tag :type="buyer?.exists ? 'success' : buyer?.willCreateOnApproval ? 'warning' : 'info'" effect="light">
                  {{ organizationState(buyer?.exists, buyer?.willCreateOnApproval) }}
                </el-tag>
              </div>
              <el-empty v-if="!buyer" description="未识别到采购方" />
              <el-descriptions v-else :column="1" border>
                <el-descriptions-item label="名称">{{ buyer.buyerName || '-' }}</el-descriptions-item>
                <el-descriptions-item label="系统ID">{{ buyer.buyerId || '-' }}</el-descriptions-item>
                <el-descriptions-item label="项目">{{ buyer.projectName || '-' }}</el-descriptions-item>
                <el-descriptions-item label="采购编号">{{ buyer.projectCode || '-' }}</el-descriptions-item>
                <el-descriptions-item label="预算">{{ formatMoney(buyer.budgetAmount) }}</el-descriptions-item>
                <el-descriptions-item label="采购包数">{{ buyer.packageCount }}</el-descriptions-item>
              </el-descriptions>
            </section>

            <section v-if="showGenericOutcomeLeads" class="org-block">
              <div class="org-block-heading">
                <h3>厂家中标线索</h3>
                <div class="org-actions">
                  <el-tag effect="light">{{ outcomeSuppliers.length }} 条</el-tag>
                  <PermissionButton
                    v-if="canEditOutcomeRows"
                    size="small"
                    type="primary"
                    plain
                    :icon="Plus"
                    :permission="BIDOPS_PERMISSIONS.REVIEW_APPROVE"
                    @click="openCreateOutcome"
                  >
                    新增明细
                  </PermissionButton>
                </div>
              </div>
              <el-table :data="outcomeSuppliers" border size="small" empty-text="未识别到中标/候选厂家">
                <el-table-column prop="supplierName" label="厂家" min-width="180" show-overflow-tooltip />
                <el-table-column label="关联状态" width="150">
                  <template #default="{ row }">
                    <el-tag :type="row.supplierId ? 'success' : 'warning'" effect="light">{{ supplierState(row) }}</el-tag>
                  </template>
                </el-table-column>
                <el-table-column label="结果" width="110">
                  <template #default="{ row }">{{ formatCommonStatus(row.outcomeType) }}</template>
                </el-table-column>
                <el-table-column label="分标/包号" min-width="150" show-overflow-tooltip>
                  <template #default="{ row }">{{ row.lotNo || '-' }} / {{ formatPackageNo(row.packageNo) }}</template>
                </el-table-column>
                <el-table-column prop="packageName" label="包名称" min-width="180" show-overflow-tooltip />
                <el-table-column label="金额" width="130" align="right">
                  <template #default="{ row }">{{ formatMoney(row.awardAmount) }}</template>
                </el-table-column>
                <el-table-column label="代理服务费" width="130" align="right">
                  <template #default="{ row }">{{ formatMoney(row.procurementAgencyServiceFeeAmount) }}</template>
                </el-table-column>
                <el-table-column v-if="canEditOutcomeRows" label="操作" width="140" fixed="right">
                  <template #default="{ row }">
                    <PermissionButton
                      text
                      size="small"
                      :icon="Edit"
                      :permission="BIDOPS_PERMISSIONS.REVIEW_APPROVE"
                      @click="openEditOutcome(row)"
                    >
                      编辑
                    </PermissionButton>
                    <PermissionButton
                      v-if="String(row.id || '') !== '0'"
                      text
                      size="small"
                      type="danger"
                      :icon="Delete"
                      :permission="BIDOPS_PERMISSIONS.REVIEW_APPROVE"
                      @click="deleteOutcome(row)"
                    >
                      删除
                    </PermissionButton>
                  </template>
                </el-table-column>
              </el-table>
            </section>
          </div>

          <div v-if="noticeKind === 'other'" class="package-list">
            <el-empty v-if="packages.length === 0" description="没有解析到包件" />
            <section v-for="pkg in packages" :key="pkg.id" class="package-block">
              <div class="package-heading">
                <div>
                  <h3>{{ packageTitle(pkg) }}</h3>
                  <p>{{ packageSubtitle(pkg) }}</p>
                </div>
                <el-tag effect="light">置信度 {{ confidencePercent(pkg.aiConfidence) }}</el-tag>
              </div>
              <el-descriptions :column="2" border>
                <el-descriptions-item label="标段号">{{ pkg.lotNo || '-' }}</el-descriptions-item>
                <el-descriptions-item label="包件号">{{ formatPackageNo(pkg.packageNo) }}</el-descriptions-item>
                <el-descriptions-item label="包件名称">{{ pkg.packageName || '-' }}</el-descriptions-item>
                <el-descriptions-item label="预算金额">{{ formatMoney(pkg.budgetAmount) }}</el-descriptions-item>
                <el-descriptions-item label="复核状态"><BidOpsStatusTag :value="pkg.reviewStatus" kind="review" /></el-descriptions-item>
              </el-descriptions>
              <h3 class="requirements-title">资格 / 商务 / 风险要求</h3>
              <RequirementTable :requirements="packageRequirements(pkg)" class="requirements" />
            </section>
          </div>
        </section>
      </div>

      <el-dialog
        v-model="outcomeEditVisible"
        :title="outcomeEditMode === 'edit' ? '编辑中标/候选明细' : '新增中标/候选明细'"
        width="760px"
        destroy-on-close
      >
        <el-form label-position="top" class="outcome-edit-form">
          <div class="outcome-form-grid">
            <el-form-item label="采购编号">
              <el-input v-model="outcomeEditForm.projectCode" placeholder="正文或表格中的采购编号" />
            </el-form-item>
            <el-form-item label="采购方">
              <el-input v-model="outcomeEditForm.buyerName" placeholder="采购方/项目单位" />
            </el-form-item>
            <el-form-item label="分标编号">
              <el-input v-model="outcomeEditForm.lotNo" placeholder="分标编号" />
            </el-form-item>
            <el-form-item label="分标名称">
              <el-input v-model="outcomeEditForm.lotName" placeholder="分标名称" />
            </el-form-item>
            <el-form-item label="包号">
              <el-input v-model="outcomeEditForm.packageNo" placeholder="包1 / 包 01" />
            </el-form-item>
            <el-form-item label="包名称">
              <el-input v-model="outcomeEditForm.packageName" placeholder="包名称" />
            </el-form-item>
            <el-form-item label="结果类型">
              <el-select v-model="outcomeEditForm.outcomeType" style="width: 100%">
                <el-option
                  v-for="item in outcomeTypeOptions"
                  :key="item.value"
                  :label="item.label"
                  :value="item.value"
                />
              </el-select>
            </el-form-item>
            <el-form-item label="排名">
              <el-input-number v-model="outcomeEditForm.rank" :min="1" :max="99" controls-position="right" style="width: 100%" />
            </el-form-item>
            <el-form-item label="厂家名称" class="wide">
              <el-input v-model="outcomeEditForm.supplierName" placeholder="中标人 / 成交供应商 / 推荐候选人" />
            </el-form-item>
            <el-form-item label="最终报价 / 成交金额（元）">
              <el-input-number
                v-model="outcomeEditForm.awardAmount"
                :min="0"
                :precision="2"
                controls-position="right"
                style="width: 100%"
              />
            </el-form-item>
            <el-form-item label="代理服务费（元）">
              <el-input-number
                v-model="outcomeEditForm.procurementAgencyServiceFeeAmount"
                :min="0"
                :precision="2"
                controls-position="right"
                style="width: 100%"
              />
            </el-form-item>
            <el-form-item label="项目名称" class="wide">
              <el-input v-model="outcomeEditForm.projectName" placeholder="项目名称" />
            </el-form-item>
            <el-form-item label="评审情况 / 证据" class="wide">
              <el-input
                v-model="outcomeEditForm.evidenceText"
                type="textarea"
                :rows="4"
                maxlength="2000"
                show-word-limit
                placeholder="填写公告原文中的评审情况、排名依据或表格片段"
              />
            </el-form-item>
          </div>
        </el-form>
        <template #footer>
          <el-button @click="outcomeEditVisible = false">取消</el-button>
          <el-button type="primary" :loading="outcomeEditRequest.loading" @click="submitOutcomeEdit">保存</el-button>
        </template>
      </el-dialog>

      <ReviewDecisionPanel class="decision-panel" :submitting="decisionRequest.loading" @approve="approve" @ignore="ignore" />
    </template>
  </PageContainer>
</template>

<style scoped>
.review-summary {
  display: grid;
  gap: 14px;
  padding: 16px;
  margin-bottom: 16px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.summary-main {
  display: grid;
  gap: 10px;
}

.summary-main h2 {
  margin: 0;
  color: #17202a;
  font-size: 18px;
  line-height: 1.45;
}

.summary-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(5, minmax(120px, 1fr));
  gap: 10px;
}

.summary-grid div {
  display: grid;
  gap: 4px;
  min-width: 0;
  padding: 10px 12px;
  border: 1px solid #e7edf5;
  border-radius: 8px;
  background: #f8fafc;
}

.summary-grid span,
.panel-heading span,
.package-heading p,
.module-heading p {
  color: #687385;
  font-size: 12px;
}

.summary-grid strong {
  overflow: hidden;
  color: #17202a;
  font-size: 14px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.split-grid {
  display: grid;
  grid-template-columns: minmax(360px, 0.95fr) minmax(460px, 1.05fr);
  gap: 16px;
  align-items: start;
}

.content-panel {
  min-width: 0;
  padding: 16px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.quality-review-panel,
.background-jobs-panel {
  display: grid;
  gap: 12px;
  min-width: 0;
  padding: 16px;
  margin-bottom: 16px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.background-job-actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  color: #687385;
  font-size: 12px;
}

.quality-summary-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(120px, 1fr));
  gap: 10px;
}

.quality-summary-grid div {
  display: grid;
  gap: 6px;
  min-width: 0;
  padding: 10px 12px;
  border: 1px solid #e7edf5;
  border-radius: 8px;
  background: #f8fafc;
}

.quality-summary-grid span {
  color: #687385;
  font-size: 12px;
}

.quality-summary-grid strong {
  overflow: hidden;
  color: #17202a;
  font-size: 14px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.panel-heading,
.package-heading,
.module-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
}

.panel-heading h2,
.section-title,
.package-heading h3,
.module-heading h3,
.requirements-title {
  margin: 0;
  color: #17202a;
}

.panel-heading h2,
.section-title {
  font-size: 16px;
}

.section-title {
  margin-top: 18px;
  margin-bottom: 10px;
}

.package-list {
  display: grid;
  gap: 16px;
  margin-top: 16px;
}

.detail-module {
  min-width: 0;
  padding-top: 16px;
  margin-top: 16px;
  border-top: 1px solid #e7edf5;
}

.detail-table {
  margin-top: 12px;
}

.ai-reparse-panel {
  display: grid;
  gap: 12px;
  padding: 14px 0;
  margin-top: 16px;
  border-top: 1px solid #e7edf5;
  border-bottom: 1px solid #e7edf5;
}

.ai-reparse-heading,
.ai-reparse-actions {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.ai-reparse-heading h2 {
  margin: 0;
  color: #17202a;
  font-size: 15px;
}

.ai-reparse-heading p {
  margin: 4px 0 0;
  color: #687385;
  font-size: 12px;
}

.ai-reparse-actions {
  align-items: center;
  justify-content: flex-start;
  flex-wrap: wrap;
}

.section-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-top: 18px;
  margin-bottom: 10px;
}

.section-heading .section-title {
  margin: 0;
}

.org-review {
  display: grid;
  gap: 16px;
}

.org-block {
  min-width: 0;
}

.org-block + .org-block {
  padding-top: 14px;
  border-top: 1px solid #e7edf5;
}

.org-block-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 10px;
}

.org-block-heading h3 {
  margin: 0;
  color: #17202a;
  font-size: 15px;
}

.org-actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.outcome-edit-form {
  min-width: 0;
}

.outcome-form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 4px 14px;
}

.outcome-form-grid .wide {
  grid-column: 1 / -1;
}

.package-block {
  min-width: 0;
  padding-top: 16px;
  border-top: 1px solid #e7edf5;
}

.package-heading h3,
.module-heading h3,
.requirements-title {
  font-size: 15px;
}

.package-heading p,
.module-heading p {
  margin: 4px 0 0;
}

.requirements-title {
  margin-top: 14px;
  margin-bottom: 8px;
}

.requirements {
  margin-top: 8px;
}

.decision-panel {
  margin-top: 16px;
}

@media (max-width: 1180px) {
  .summary-grid {
    grid-template-columns: repeat(2, minmax(160px, 1fr));
  }

  .quality-summary-grid {
    grid-template-columns: repeat(2, minmax(160px, 1fr));
  }

  .split-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 720px) {
  .summary-grid {
    grid-template-columns: 1fr;
  }

  .quality-summary-grid {
    grid-template-columns: 1fr;
  }

  .content-panel {
    padding: 12px;
  }

  .section-heading {
    align-items: flex-start;
    flex-direction: column;
  }

  .outcome-form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
