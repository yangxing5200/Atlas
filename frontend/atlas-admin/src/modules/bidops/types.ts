export type BidOpsId = string

export interface BidOpsPagedQuery {
  keyword?: string
  pageIndex?: number
  pageSize?: number
}

export interface RawNoticeSearchQuery extends BidOpsPagedQuery {
  status?: RawNoticeStatus
}

export interface CrawlRunLogSearchQuery extends BidOpsPagedQuery {
  sourceId?: BidOpsId
  channelId?: BidOpsId
  backgroundJobId?: BidOpsId
  operation?: string
  status?: string
}

export interface ReviewTaskSearchQuery extends BidOpsPagedQuery {
  status?: ReviewTaskStatus
}

export type ProcessingFailureSearchQuery = BidOpsPagedQuery

export interface PackageSearchQuery extends BidOpsPagedQuery {
  noticeId?: BidOpsId
}

export interface OpportunitySearchQuery extends BidOpsPagedQuery {
  noticeId?: BidOpsId
  packageId?: BidOpsId
  stage?: string
  status?: string
  watchedByMe?: boolean
}

export interface PagedResult<T> {
  total: number
  items: T[]
  pageIndex: number
  pageSize: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}

export type RawNoticeStatus = number | string
export type ReviewTaskStatus = number | string
export type ReviewStatus = number | string
export type DownloadStatus = number | string
export type TextExtractStatus = number | string

export interface CrawlSourceDto {
  id: BidOpsId
  code: string
  name: string
  sourceType: string
  baseUrl: string
  enabled: boolean
  rateLimitPerMinute: number
  crawlIntervalMinutes: number
  maxRetryCount: number
  needLogin: boolean
  respectRobots: boolean
  robotsPolicyNote: string
  pauseReason: string
}

export interface CrawlChannelDto {
  id: BidOpsId
  sourceId: BidOpsId
  code: string
  name: string
  noticeType: string
  listUrl: string
  region: string
  industry: string
  enabled: boolean
  lastScanTime?: string | null
  lastSuccessTime?: string | null
  lastError: string
}

export interface CrawlRunLogDto {
  id: BidOpsId
  sourceId?: BidOpsId | null
  channelId?: BidOpsId | null
  backgroundJobId?: BidOpsId | null
  operation: string
  status: string
  message: string
  durationMs?: number | null
  createdAt: string
  updatedAt?: string | null
}

export interface RawNoticeDto {
  id: BidOpsId
  sourceId: BidOpsId
  channelId?: BidOpsId | null
  title: string
  detailUrl: string
  noticeType: string
  publishTime?: string | null
  fetchTime: string
  contentHash: string
  textPreview: string
  textContent?: string
  status: RawNoticeStatus
  lastError: string
}

export interface RawNoticePipelineDto {
  rawNoticeId: BidOpsId
  title: string
  rawStatus: RawNoticeStatus
  fetchTime: string
  detailUrl: string
  attachmentCount: number
  attachmentDownloadedCount: number
  attachmentTextExtractedCount: number
  reviewTaskId?: BidOpsId | null
  reviewTaskStatus?: ReviewTaskStatus | null
  noticeStagingId?: BidOpsId | null
  noticeStagingStatus?: ReviewStatus | null
  noticeId?: BidOpsId | null
  packageCount: number
  requirementCount: number
  steps: RawNoticePipelineStepDto[]
}

export interface RawNoticePipelineStepDto {
  code: string
  title: string
  status: string
  description: string
  occurredAt?: string | null
  totalCount: number
  succeededCount: number
  failedCount: number
  pendingCount: number
  error: string
}

export interface RawAttachmentDto {
  id: BidOpsId
  rawNoticeId: BidOpsId
  fileName: string
  fileUrl: string
  fileType: string
  fileSize?: number | null
  downloadStatus: DownloadStatus
  textExtractStatus: TextExtractStatus
  hasLocalFile: boolean
  hasExtractedText: boolean
  createdAt: string
}

export interface RawAttachmentTextDto {
  id: BidOpsId
  rawNoticeId: BidOpsId
  fileName: string
  textContent: string
}

export interface ReviewTaskDto {
  id: BidOpsId
  bizType: string
  bizId: BidOpsId
  rawNoticeId?: BidOpsId | null
  taskTitle: string
  priority: number
  status: ReviewTaskStatus
  decision: string
  remark: string
  projectName: string
  projectCode: string
  buyerName: string
  region: string
  noticeType: string
  publishTime?: string | null
  signupDeadline?: string | null
  bidDeadline?: string | null
  openBidTime?: string | null
  aiConfidence: number
  packageCount: number
  requirementCount: number
  rejectRiskCount: number
  createdAt: string
  reviewedAt?: string | null
}

export interface ProcessingFailureDto {
  rawNoticeId: BidOpsId
  title: string
  detailUrl: string
  noticeType: string
  publishTime?: string | null
  fetchTime: string
  rawStatus: RawNoticeStatus
  lastError: string
}

export interface NoticeStagingDto {
  id: BidOpsId
  rawNoticeId: BidOpsId
  noticeType: string
  projectName: string
  projectCode: string
  buyerName: string
  agencyName: string
  region: string
  budgetAmount?: number | null
  publishTime?: string | null
  signupDeadline?: string | null
  bidDeadline?: string | null
  openBidTime?: string | null
  aiConfidence: number
  reviewStatus: ReviewStatus
}

export interface RequirementStagingDto {
  id: BidOpsId
  packageStagingId: BidOpsId
  requirementType: string
  originalText: string
  isMandatory: boolean
  isRejectRisk: boolean
  requiredEvidenceType: string
  riskLevel: string
  aiExplanation: string
  aiConfidence: number
}

export interface PackageStagingDto {
  id: BidOpsId
  noticeStagingId: BidOpsId
  lotNo: string
  lotName: string
  packageNo: string
  packageName: string
  category: string
  budgetAmount?: number | null
  aiConfidence: number
  reviewStatus: ReviewStatus
  requirements: RequirementStagingDto[]
}

export interface ReviewTaskDetailDto {
  task: ReviewTaskDto
  rawNotice?: RawNoticeDto | null
  notice?: NoticeStagingDto | null
  packages: PackageStagingDto[]
  attachments: RawAttachmentDto[]
}

export interface NoticeDto {
  id: BidOpsId
  rawNoticeId: BidOpsId
  title: string
  noticeType: string
  projectName: string
  projectCode: string
  buyerName: string
  region: string
  budgetAmount?: number | null
  publishTime?: string | null
  bidDeadline?: string | null
  status: string
}

export interface TenderPackageDto {
  id: BidOpsId
  noticeId: BidOpsId
  noticeTitle?: string
  noticeType?: string
  projectName?: string
  projectCode?: string
  buyerName?: string
  region?: string
  publishTime?: string | null
  bidDeadline?: string | null
  lotNo?: string
  lotName?: string
  packageNo: string
  packageName: string
  category: string
  quantity?: number | null
  unit?: string
  budgetAmount?: number | null
  maxPrice?: number | null
  deliveryPlace: string
  deliveryPeriod: string
  status: string
  requirementCount?: number
  rejectRiskCount?: number
  createdAt?: string
  updatedAt?: string | null
}

export interface RequirementItemDto {
  id: BidOpsId
  packageId: BidOpsId
  requirementType: string
  originalText: string
  isMandatory: boolean
  isRejectRisk: boolean
  requiredEvidenceType: string
  riskLevel: string
}

export interface PackageTimelineItemDto {
  eventType: string
  title: string
  description: string
  occurredAt: string
  status: string
}

export interface OpportunityDto {
  id: BidOpsId
  noticeId: BidOpsId
  packageId: BidOpsId
  opportunityNo: string
  title: string
  noticeTitle: string
  packageName: string
  packageNo: string
  projectName: string
  projectCode: string
  buyerName: string
  region: string
  publishTime?: string | null
  bidDeadline?: string | null
  stage: string
  status: string
  priority: number
  estimatedAmount?: number | null
  valueScore?: number | null
  valueLevel: string
  decision: string
  ownerUserId?: BidOpsId | null
  nextActionAtUtc?: string | null
  lastStageChangedAtUtc: string
  assessmentSummary: string
  remark: string
  watchCount: number
  watchedByMe: boolean
  createdAt: string
  updatedAt?: string | null
}

export interface OpportunityStageHistoryDto {
  id: BidOpsId
  opportunityId: BidOpsId
  fromStage: string
  toStage: string
  reason: string
  operatorUserId?: BidOpsId | null
  occurredAtUtc: string
}

export interface OpportunityDetailDto {
  opportunity: OpportunityDto
  package?: TenderPackageDto | null
  requirements: RequirementItemDto[]
  stageHistory: OpportunityStageHistoryDto[]
}

export interface SupplierSearchQuery extends BidOpsPagedQuery {
  status?: string
  region?: string
  category?: string
  evidenceExpiringOnly?: boolean
}

export interface OutcomeSupplierSearchQuery extends BidOpsPagedQuery {
  rawNoticeId?: BidOpsId
  packageId?: BidOpsId
  supplierId?: BidOpsId
  outcomeType?: string
  supplierName?: string
  packageNo?: string
  category?: string
}

export interface SupplierMatchRunSearchQuery extends BidOpsPagedQuery {
  packageId?: BidOpsId
  status?: string
}

export interface PursuitSearchQuery extends BidOpsPagedQuery {
  packageId?: BidOpsId
  opportunityId?: BidOpsId
  stage?: string
  status?: string
  ownerUserId?: BidOpsId
  mineOnly?: boolean
  overdueOnly?: boolean
}

export interface SupplierDto {
  id: BidOpsId
  supplierNo: string
  name: string
  unifiedSocialCreditCode: string
  region: string
  address: string
  contactName: string
  contactPhone: string
  contactEmail: string
  status: string
  qualityScore?: number | null
  remark: string
  contactCount: number
  capabilityCount: number
  evidenceCount: number
  expiringEvidenceCount: number
  createdAt: string
  updatedAt?: string | null
}

export interface SupplierContactDto {
  id: BidOpsId
  supplierId: BidOpsId
  name: string
  role: string
  phone: string
  email: string
  isPrimary: boolean
  remark: string
}

export interface SupplierCapabilityDto {
  id: BidOpsId
  supplierId: BidOpsId
  category: string
  productLine: string
  capabilityTags: string
  regionScope: string
  qualificationLevel: string
  remark: string
}

export interface SupplierEvidenceDocumentDto {
  id: BidOpsId
  supplierId: BidOpsId
  documentName: string
  documentType: string
  evidenceNo: string
  issuedBy: string
  validFrom?: string | null
  validTo?: string | null
  fileName: string
  fileUrl: string
  status: string
  remark: string
}

export interface SupplierDetailDto {
  supplier: SupplierDto
  contacts: SupplierContactDto[]
  capabilities: SupplierCapabilityDto[]
  evidenceDocuments: SupplierEvidenceDocumentDto[]
}

export interface OutcomeSupplierRecordDto {
  id: BidOpsId
  rawNoticeId: BidOpsId
  noticeId?: BidOpsId | null
  tenderPackageId?: BidOpsId | null
  supplierId?: BidOpsId | null
  sourceUrl: string
  noticeTitle: string
  noticeType: string
  projectName: string
  projectCode: string
  buyerName: string
  region: string
  publishTime?: string | null
  lotNo: string
  lotName: string
  packageNo: string
  packageName: string
  category: string
  supplierName: string
  outcomeType: string
  rank?: number | null
  awardAmount?: number | null
  currency: string
  evidenceText: string
  extractionConfidence: number
  createdAt: string
}

export interface SupplierOutcomeStatDto {
  supplierName: string
  supplierId?: BidOpsId | null
  outcomeCount: number
  awardedCount: number
  candidateCount: number
  totalAwardAmount?: number | null
  lastPublishTime?: string | null
}

export interface SupplierOutcomeSummaryDto {
  generatedAtUtc: string
  recordCount: number
  supplierCount: number
  awardedCount: number
  candidateCount: number
  linkedPackageCount: number
  linkedSupplierCount: number
  topSuppliers: SupplierOutcomeStatDto[]
}

export interface PackageHistoricalSupplierLeadDto {
  outcomeRecordId: BidOpsId
  rawNoticeId: BidOpsId
  supplierId?: BidOpsId | null
  supplierName: string
  outcomeType: string
  rank?: number | null
  awardAmount?: number | null
  currency: string
  projectName: string
  projectCode: string
  noticeTitle: string
  sourceUrl: string
  publishTime?: string | null
  packageNo: string
  packageName: string
  category: string
  matchReason: string
  matchScore: number
  evidenceText: string
}

export interface OutcomeSupplierBackfillEnqueueDto {
  requestedMaxItems: number
  queuedCount: number
  jobs: EnqueueJobDto[]
}

export interface SupplierAnalysisBucketDto {
  code: string
  count: number
  supplierCount: number
}

export interface SupplierAnalysisItemDto {
  supplierId: BidOpsId
  supplierNo: string
  supplierName: string
  status: string
  region: string
  qualityScore?: number | null
  capabilityCount: number
  evidenceCount: number
  validEvidenceCount: number
  expiringEvidenceCount: number
  expiredEvidenceCount: number
  matchResultCount: number
  candidateMatchCount: number
  cautionMatchCount: number
  notRecommendedMatchCount: number
  goDecisionCount: number
  noGoDecisionCount: number
  holdDecisionCount: number
  pursuitCount: number
  lastMatchedAtUtc?: string | null
  lastDecisionAtUtc?: string | null
  lastPursuitCreatedAtUtc?: string | null
  riskFlags: string
}

export interface SupplierAnalysisSummaryDto {
  generatedAtUtc: string
  supplierSource: string
  supplierSourceDescription: string
  outcomeExtractionStatus: string
  totalSuppliers: number
  activeSuppliers: number
  inactiveSuppliers: number
  blockedSuppliers: number
  suppliersWithCapabilities: number
  suppliersWithEvidence: number
  expiringEvidenceDocuments: number
  expiredEvidenceDocuments: number
  averageQualityScore?: number | null
  matchedSupplierCount: number
  candidateSupplierCount: number
  goDecisionCount: number
  pursuitSupplierCount: number
  outcomeRecordCount: number
  outcomeSupplierCount: number
  awardedOutcomeCount: number
  candidateOutcomeCount: number
  linkedOutcomeSupplierCount: number
  topOutcomeSuppliers: SupplierOutcomeStatDto[]
  capabilityCategories: SupplierAnalysisBucketDto[]
  evidenceStatuses: SupplierAnalysisBucketDto[]
  topSuppliers: SupplierAnalysisItemDto[]
}

export interface SupplierMatchRunDto {
  id: BidOpsId
  packageId: BidOpsId
  backgroundJobId?: BidOpsId | null
  runNo: string
  status: string
  requestedByUserId: BidOpsId
  requestedByUserName: string
  criteriaSummary: string
  maxSuppliers: number
  supplierCount: number
  matchedCount: number
  missingEvidenceCount: number
  startedAtUtc?: string | null
  completedAtUtc?: string | null
  errorMessage: string
  createdAt: string
  updatedAt?: string | null
}

export interface MissingEvidenceCheckDto {
  id: BidOpsId
  runId: BidOpsId
  resultId: BidOpsId
  packageId: BidOpsId
  supplierId: BidOpsId
  requirementId?: BidOpsId | null
  matchedEvidenceDocumentId?: BidOpsId | null
  requiredEvidenceType: string
  requirementText: string
  status: string
  explanation: string
}

export interface SupplierMatchResultDto {
  id: BidOpsId
  runId: BidOpsId
  packageId: BidOpsId
  supplierId: BidOpsId
  supplierNameSnapshot: string
  rank: number
  score: number
  matchLevel: string
  recommendation: string
  categoryMatched: boolean
  regionMatched: boolean
  evidenceMatchedCount: number
  missingEvidenceCount: number
  riskFlags: string
  explanation: string
  missingEvidenceChecks: MissingEvidenceCheckDto[]
}

export interface SupplierMatchRunDetailDto {
  run: SupplierMatchRunDto
  package?: TenderPackageDto | null
  requirements: RequirementItemDto[]
  results: SupplierMatchResultDto[]
}

export interface StartSupplierMatchRunResponse {
  run: SupplierMatchRunDto
  job: EnqueueJobDto
}

export interface GoNoGoDecisionDto {
  id: BidOpsId
  packageId: BidOpsId
  opportunityId?: BidOpsId | null
  matchRunId?: BidOpsId | null
  supplierMatchResultId?: BidOpsId | null
  supplierId?: BidOpsId | null
  decision: string
  reason: string
  riskSummary: string
  decidedByUserId: BidOpsId
  decidedByUserName: string
  decidedAtUtc: string
}

export interface PursuitDto {
  id: BidOpsId
  noticeId: BidOpsId
  packageId: BidOpsId
  opportunityId?: BidOpsId | null
  goNoGoDecisionId?: BidOpsId | null
  supplierId?: BidOpsId | null
  supplierNameSnapshot: string
  pursuitNo: string
  title: string
  noticeTitle: string
  packageNo: string
  packageName: string
  projectName: string
  projectCode: string
  buyerName: string
  region: string
  stage: string
  status: string
  priority: number
  estimatedAmount?: number | null
  bidDeadlineAtUtc?: string | null
  ownerUserId?: BidOpsId | null
  progressPercent: number
  riskLevel: string
  taskCount: number
  openTaskCount: number
  overdueTaskCount: number
  lastStageChangedAtUtc: string
  remark: string
  createdAt: string
  updatedAt?: string | null
}

export interface PursuitTaskDto {
  id: BidOpsId
  pursuitId: BidOpsId
  title: string
  taskType: string
  status: string
  priority: number
  ownerUserId?: BidOpsId | null
  dueAtUtc?: string | null
  completedAtUtc?: string | null
  description: string
  resultNote: string
  createdAt: string
  updatedAt?: string | null
}

export interface PursuitFollowRecordDto {
  id: BidOpsId
  pursuitId: BidOpsId
  followType: string
  content: string
  nextActionAtUtc?: string | null
  createdByUserId?: BidOpsId | null
  createdByUserName: string
  createdAt: string
}

export interface PursuitDetailDto {
  pursuit: PursuitDto
  package?: TenderPackageDto | null
  opportunity?: OpportunityDto | null
  tasks: PursuitTaskDto[]
  followRecords: PursuitFollowRecordDto[]
}

export interface BidOpsMetricBucketDto {
  code: string
  label: string
  count: number
}

export interface BidOpsDashboardTodoDto {
  type: string
  title: string
  route: string
  priority: number
  dueAtUtc?: string | null
}

export interface BidOpsDashboardDeadlineRiskDto {
  opportunityId: BidOpsId
  noticeId: BidOpsId
  packageId: BidOpsId
  opportunityNo: string
  title: string
  stage: string
  valueLevel: string
  bidDeadline: string
  daysRemaining: number
  riskLevel: string
}

export interface BidOpsDashboardOpportunityDto {
  opportunityId: BidOpsId
  packageId: BidOpsId
  opportunityNo: string
  title: string
  stage: string
  decision: string
  valueLevel: string
  valueScore?: number | null
  estimatedAmount?: number | null
}

export interface BidOpsDashboardSummaryDto {
  generatedAtUtc: string
  rawNoticeCreatedToday: number
  reviewTaskCreatedToday: number
  pendingReviewTasks: number
  formalNoticeCreatedToday: number
  packageCreatedToday: number
  activePackageCount: number
  rejectRiskRequirementCount: number
  opportunityCreatedToday: number
  activeOpportunityCount: number
  highValueOpportunityCount: number
  opportunityTodoCount: number
  deadlineRiskCount: number
  opportunityStageFunnel: BidOpsMetricBucketDto[]
  opportunityValueDistribution: BidOpsMetricBucketDto[]
  todos: BidOpsDashboardTodoDto[]
  deadlineRisks: BidOpsDashboardDeadlineRiskDto[]
  highValueOpportunities: BidOpsDashboardOpportunityDto[]
}

export interface EnqueueJobDto {
  jobId: BidOpsId
  jobType: string
  queue: string
  alreadyExists: boolean
}

export interface CreateCrawlSourceRequest {
  code: string
  name: string
  sourceType: string
  baseUrl: string
  enabled: boolean
  rateLimitPerMinute: number
  crawlIntervalMinutes: number
  maxRetryCount: number
  needJsRender: boolean
  needLogin: boolean
  respectRobots: boolean
  robotsPolicyNote: string
  remark: string
}

export type UpdateCrawlSourceRequest = CreateCrawlSourceRequest

export interface CreateCrawlChannelRequest {
  sourceId: BidOpsId | number
  code: string
  name: string
  noticeType: string
  listUrl: string
  region: string
  industry: string
  enabled: boolean
}

export type UpdateCrawlChannelRequest = CreateCrawlChannelRequest

export interface ImportPublicUrlRequest {
  sourceId?: BidOpsId | number | null
  channelId?: BidOpsId | number | null
  detailUrl: string
  title?: string | null
  noticeType?: string | null
  textContent?: string | null
}

export interface ReparseRawNoticeRequest {
  reason?: string | null
}

export interface ReviewDecisionRequest {
  remark?: string | null
}

export interface CreateOpportunityRequest {
  packageId: BidOpsId | number
  title?: string | null
  priority?: number
  estimatedAmount?: number | null
  ownerUserId?: BidOpsId | number | null
  nextActionAtUtc?: string | null
  remark?: string | null
}

export interface UpdateOpportunityRequest {
  title?: string | null
  priority?: number | null
  estimatedAmount?: number | null
  ownerUserId?: BidOpsId | number | null
  nextActionAtUtc?: string | null
  remark?: string | null
}

export interface WatchOpportunityRequest {
  enabled: boolean
  remark?: string | null
}

export interface AssessOpportunityRequest {
  valueScore?: number | null
  valueLevel?: string | null
  decision?: string | null
  assessmentSummary?: string | null
}

export interface ChangeOpportunityStageRequest {
  stage: string
  status?: string | null
  reason?: string | null
}

export interface CreateSupplierRequest {
  name: string
  unifiedSocialCreditCode?: string | null
  region?: string | null
  address?: string | null
  contactName?: string | null
  contactPhone?: string | null
  contactEmail?: string | null
  qualityScore?: number | null
  remark?: string | null
}

export interface UpdateSupplierRequest extends CreateSupplierRequest {
  status?: string | null
}

export interface CreateSupplierContactRequest {
  name: string
  role?: string | null
  phone?: string | null
  email?: string | null
  isPrimary: boolean
  remark?: string | null
}

export interface CreateSupplierCapabilityRequest {
  category: string
  productLine?: string | null
  capabilityTags?: string | null
  regionScope?: string | null
  qualificationLevel?: string | null
  remark?: string | null
}

export interface CreateSupplierEvidenceDocumentRequest {
  documentName: string
  documentType: string
  evidenceNo?: string | null
  issuedBy?: string | null
  validFrom?: string | null
  validTo?: string | null
  fileName?: string | null
  fileUrl?: string | null
  storageProvider?: string | null
  storageKey?: string | null
  remark?: string | null
}

export interface StartSupplierMatchRunRequest {
  maxSuppliers?: number
  criteriaSummary?: string | null
}

export interface CreateGoNoGoDecisionRequest {
  opportunityId?: BidOpsId | number | null
  matchRunId?: BidOpsId | number | null
  supplierMatchResultId?: BidOpsId | number | null
  supplierId?: BidOpsId | number | null
  decision: string
  reason?: string | null
  riskSummary?: string | null
}

export interface CreatePursuitRequest {
  packageId: BidOpsId | number
  opportunityId?: BidOpsId | number | null
  goNoGoDecisionId?: BidOpsId | number | null
  supplierId?: BidOpsId | number | null
  supplierNameSnapshot?: string | null
  title?: string | null
  priority?: number | null
  estimatedAmount?: number | null
  bidDeadlineAtUtc?: string | null
  ownerUserId?: BidOpsId | number | null
  remark?: string | null
}

export interface UpdatePursuitRequest {
  title?: string | null
  priority?: number | null
  estimatedAmount?: number | null
  bidDeadlineAtUtc?: string | null
  ownerUserId?: BidOpsId | number | null
  progressPercent?: number | null
  riskLevel?: string | null
  remark?: string | null
}

export interface ChangePursuitStatusRequest {
  stage: string
  status?: string | null
  reason?: string | null
}

export interface CreatePursuitTaskRequest {
  title: string
  taskType?: string | null
  priority?: number | null
  ownerUserId?: BidOpsId | number | null
  dueAtUtc?: string | null
  description?: string | null
}

export interface UpdatePursuitTaskRequest {
  title?: string | null
  taskType?: string | null
  status?: string | null
  priority?: number | null
  ownerUserId?: BidOpsId | number | null
  dueAtUtc?: string | null
  description?: string | null
  resultNote?: string | null
}

export interface CreatePursuitFollowRecordRequest {
  followType?: string | null
  content: string
  nextActionAtUtc?: string | null
}
