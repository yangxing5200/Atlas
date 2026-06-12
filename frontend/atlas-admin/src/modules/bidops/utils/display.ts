export interface DisplayOption {
  label: string
  value: string
}

const noticeTypeLabels: Record<string, string> = {
  TenderAnnouncement: '招标公告',
  ProcurementAnnouncement: '采购公告',
  CandidateAnnouncement: '中标候选人公示',
  AwardAnnouncement: '中标/成交结果公告',
  ResultAnnouncement: '中标结果公告',
  CorrectionAnnouncement: '更正公告',
  ClarificationAnnouncement: '澄清公告',
  QualificationPreAnnouncement: '资格预审公告',
  Other: '其他公告',
}

const sourceTypeLabels: Record<string, string> = {
  StateGridEcp: '国家电网 ECP',
  Manual: '手动导入',
  Mock: '模拟源',
}

const categoryLabels: Record<string, string> = {
  Service: '服务',
  Goods: '物资',
  Material: '物资',
  Construction: '工程',
  Engineering: '工程',
  Equipment: '设备',
  Other: '其他',
  UNSPECIFIED: '未识别',
}

const requirementTypeLabels: Record<string, string> = {
  Qualification: '资格要求',
  Deadline: '时间要求',
  Business: '商务要求',
  Commercial: '商务要求',
  Technical: '技术要求',
  Financial: '财务要求',
  Delivery: '交付要求',
  Performance: '业绩要求',
  Evidence: '材料要求',
  RejectionRisk: '废标风险',
  General: '一般要求',
  Other: '其他要求',
}

const evidenceTypeLabels: Record<string, string> = {
  QualificationDocument: '资格文件',
  BidDocument: '投标文件',
  CommitmentLetter: '承诺函',
  License: '资质证书',
  PerformanceContract: '业绩合同',
  FinancialStatement: '财务报表',
  TechnicalProposal: '技术方案',
  Other: '其他材料',
}

const riskLevelLabels: Record<string, string> = {
  High: '高',
  Medium: '中',
  Low: '低',
  None: '无',
}

const commonStatusLabels: Record<string, string> = {
  New: '新建',
  Preparing: '标书准备',
  Review: '内部评审',
  Submitted: '已提交',
  Awarded: '已中标',
  Shortlisted: '入围',
  Assessing: '评估中',
  Decided: '已决策',
  Pursuing: '投标作业中',
  Closed: '已关闭',
  Archived: '已归档',
  Enabled: '启用',
  Disabled: '停用',
  Active: '有效',
  Inactive: '无效',
  Blocked: '停用',
  Valid: '有效',
  ExpiringSoon: '即将到期',
  Expired: '已过期',
  Queued: '已排队',
  Pending: '待处理',
  Todo: '待办',
  InProgress: '进行中',
  Running: '处理中',
  Succeeded: '成功',
  Failed: '失败',
  Completed: '已完成',
  Done: '已完成',
  Canceled: '已取消',
  Overdue: '已逾期',
  Skipped: '已跳过',
  Changed: '内容变更',
  RateLimited: '限流跳过',
  NoticeFailed: '公告失败',
  AttachmentListFailed: '附件列表失败',
  Approved: '已通过',
  Ignored: '已忽略',
  ReparseRequired: '需重解析',
  Go: '立项',
  NoGo: '放弃',
  Hold: '暂缓',
  Undecided: '未决策',
  Unknown: '未评估',
  High: '高',
  Medium: '中',
  Low: '低',
  Candidate: '候选',
  Caution: '需确认',
  NotRecommended: '不建议',
  Missing: '缺失',
  Qualification: '资格材料',
  Technical: '技术文件',
  Commercial: '商务文件',
  Pricing: '报价文件',
  Submission: '提交动作',
  Other: '其他',
  Note: '备注',
  Call: '电话',
  Meeting: '会议',
  StatusChange: '状态变更',
  Risk: '风险',
}

const explanationLabels: Record<string, string> = {
  'Qualification requirement was extracted from public notice text.': '根据公告正文识别为资格要求。',
  'Deadline requirement was extracted from public notice text.': '根据公告正文识别为时间要求。',
  'Rejection risk was extracted from public notice text.': '根据公告正文识别为废标风险。',
}

export const noticeTypeOptions: DisplayOption[] = [
  { label: '招标公告', value: 'TenderAnnouncement' },
  { label: '采购公告', value: 'ProcurementAnnouncement' },
  { label: '中标候选人公示', value: 'CandidateAnnouncement' },
  { label: '中标/成交结果公告', value: 'AwardAnnouncement' },
  { label: '更正公告', value: 'CorrectionAnnouncement' },
  { label: '澄清公告', value: 'ClarificationAnnouncement' },
  { label: '其他公告', value: 'Other' },
]

export const rawNoticeStatusOptions: DisplayOption[] = [
  { label: '新建', value: 'New' },
  { label: '待解析', value: 'ParseQueued' },
  { label: '待审核', value: 'ReviewPending' },
  { label: '已入库', value: 'Approved' },
  { label: '已忽略', value: 'Ignored' },
  { label: '失败', value: 'Failed' },
]

export const reviewTaskStatusOptions: DisplayOption[] = [
  { label: '待审核', value: 'Pending' },
  { label: '审核中', value: 'InReview' },
  { label: '已通过', value: 'Approved' },
  { label: '已忽略', value: 'Ignored' },
  { label: '已合并', value: 'Merged' },
  { label: '需重解析', value: 'ReparseRequired' },
]

export const opportunityStageOptions: DisplayOption[] = [
  { label: '新建', value: 'New' },
  { label: '评估中', value: 'Assessing' },
  { label: '已决策', value: 'Decided' },
  { label: '投标作业中', value: 'Pursuing' },
]

export const opportunityStatusOptions: DisplayOption[] = [
  { label: '有效', value: 'Active' },
  { label: '已关闭', value: 'Closed' },
  { label: '已归档', value: 'Archived' },
]

export const opportunityDecisionOptions: DisplayOption[] = [
  { label: '未决策', value: 'Undecided' },
  { label: '立项', value: 'Go' },
  { label: '放弃', value: 'NoGo' },
  { label: '暂缓', value: 'Hold' },
]

export const opportunityValueLevelOptions: DisplayOption[] = [
  { label: '未评估', value: 'Unknown' },
  { label: '低价值', value: 'Low' },
  { label: '中价值', value: 'Medium' },
  { label: '高价值', value: 'High' },
]

export const supplierStatusOptions: DisplayOption[] = [
  { label: '有效', value: 'Active' },
  { label: '无效', value: 'Inactive' },
  { label: '停用', value: 'Blocked' },
]

export const supplierDocumentTypeOptions: DisplayOption[] = [
  { label: '营业执照', value: 'BusinessLicense' },
  { label: '资质证书', value: 'QualificationCertificate' },
  { label: '业绩证明', value: 'PerformanceEvidence' },
  { label: '授权文件', value: 'AuthorizationDocument' },
  { label: '财务材料', value: 'FinancialDocument' },
  { label: '其他材料', value: 'Other' },
]

export const supplierMatchRunStatusOptions: DisplayOption[] = [
  { label: '已排队', value: 'Queued' },
  { label: '处理中', value: 'Running' },
  { label: '成功', value: 'Succeeded' },
  { label: '失败', value: 'Failed' },
]

export const goNoGoDecisionOptions: DisplayOption[] = [
  { label: '立项', value: 'Go' },
  { label: '放弃', value: 'NoGo' },
  { label: '暂缓', value: 'Hold' },
]

export const pursuitStageOptions: DisplayOption[] = [
  { label: '新建', value: 'New' },
  { label: '标书准备', value: 'Preparing' },
  { label: '内部评审', value: 'Review' },
  { label: '已提交', value: 'Submitted' },
  { label: '已中标', value: 'Awarded' },
  { label: '已关闭', value: 'Closed' },
]

export const pursuitStatusOptions: DisplayOption[] = [
  { label: '有效', value: 'Active' },
  { label: '已关闭', value: 'Closed' },
  { label: '已归档', value: 'Archived' },
]

export const pursuitRiskLevelOptions: DisplayOption[] = [
  { label: '无', value: 'None' },
  { label: '低', value: 'Low' },
  { label: '中', value: 'Medium' },
  { label: '高', value: 'High' },
]

export const pursuitTaskTypeOptions: DisplayOption[] = [
  { label: '资格材料', value: 'Qualification' },
  { label: '技术文件', value: 'Technical' },
  { label: '商务文件', value: 'Commercial' },
  { label: '报价文件', value: 'Pricing' },
  { label: '内部评审', value: 'Review' },
  { label: '提交动作', value: 'Submission' },
  { label: '其他', value: 'Other' },
]

export const pursuitTaskStatusOptions: DisplayOption[] = [
  { label: '待办', value: 'Todo' },
  { label: '进行中', value: 'InProgress' },
  { label: '已完成', value: 'Done' },
  { label: '阻塞', value: 'Blocked' },
  { label: '已取消', value: 'Canceled' },
  { label: '已逾期', value: 'Overdue' },
]

export const pursuitFollowTypeOptions: DisplayOption[] = [
  { label: '备注', value: 'Note' },
  { label: '电话', value: 'Call' },
  { label: '会议', value: 'Meeting' },
  { label: '状态变更', value: 'StatusChange' },
  { label: '风险', value: 'Risk' },
  { label: '其他', value: 'Other' },
]

export function formatOpportunityStage(value?: string | null) {
  return formatCommonStatus(value)
}

export function formatOpportunityDecision(value?: string | null) {
  return formatCommonStatus(value)
}

export function formatOpportunityValueLevel(value?: string | null) {
  const labels: Record<string, string> = {
    Unknown: '未评估',
    Low: '低价值',
    Medium: '中价值',
    High: '高价值',
  }
  return formatByMap(value, labels)
}

export function formatSupplierStatus(value?: string | null) {
  return formatCommonStatus(value)
}

export function formatSupplierDocumentType(value?: string | null) {
  const labels = Object.fromEntries(supplierDocumentTypeOptions.map((item) => [item.value, item.label]))
  return formatByMap(value, labels)
}

export function formatSupplierMatchRunStatus(value?: string | null) {
  return formatCommonStatus(value)
}

export function formatSupplierMatchLevel(value?: string | null) {
  return formatCommonStatus(value)
}

export function formatSupplierMatchRecommendation(value?: string | null) {
  return formatCommonStatus(value)
}

export function formatMissingEvidenceStatus(value?: string | null) {
  return formatCommonStatus(value)
}

export function formatGoNoGoDecision(value?: string | null) {
  return formatCommonStatus(value)
}

export function formatPursuitStage(value?: string | null) {
  return formatCommonStatus(value)
}

export function formatPursuitStatus(value?: string | null) {
  return formatCommonStatus(value)
}

export function formatPursuitTaskType(value?: string | null) {
  return formatCommonStatus(value)
}

export function formatPursuitTaskStatus(value?: string | null) {
  return formatCommonStatus(value)
}

export function formatPursuitFollowType(value?: string | null) {
  return formatCommonStatus(value)
}

export function formatNoticeType(value?: string | null) {
  return formatByMap(value, noticeTypeLabels)
}

export function formatSourceType(value?: string | null) {
  return formatByMap(value, sourceTypeLabels)
}

export function formatCategory(value?: string | null) {
  return formatByMap(value, categoryLabels)
}

export function formatPackageNo(value?: string | null) {
  return formatKnownText(value, '待补录')
}

export function formatSupplierName(value?: string | null) {
  return formatKnownText(value, '待补录厂家')
}

export function formatRequirementType(value?: string | null) {
  return formatByMap(value, requirementTypeLabels)
}

export function formatEvidenceType(value?: string | null) {
  return formatByMap(value, evidenceTypeLabels)
}

export function formatRiskLevel(value?: string | null) {
  return formatByMap(value, riskLevelLabels)
}

export function formatCommonStatus(value: unknown) {
  if (typeof value === 'boolean') return value ? '是' : '否'
  return formatByMap(value, commonStatusLabels)
}

export function formatExplanation(value?: string | null) {
  return formatByMap(value, explanationLabels)
}

function formatByMap(value: unknown, map: Record<string, string>) {
  if (value === null || value === undefined || value === '') return '-'
  const key = String(value)
  return map[key] || key
}

function formatKnownText(value: unknown, fallback: string) {
  if (value === null || value === undefined) return fallback
  const text = String(value).trim()
  if (!text || isUnknownMarker(text) || looksUnreadablePlaceholder(text)) return fallback
  return text
}

function isUnknownMarker(value: string) {
  const normalized = value.trim().toUpperCase()
  return ['UNSPECIFIED', 'UNKNOWN', 'N/A', 'NA', 'NULL', '未识别'].includes(normalized)
}

function looksUnreadablePlaceholder(value: string) {
  const signal = value.replace(/[\s_\-./\\|,，。:：;；()[\]{}<>《》【】]/g, '')
  return signal.length > 0 && (/^[?？\uFFFD]+$/.test(signal) || /^[?？\uFFFD]{2,}\d*$/.test(signal))
}
