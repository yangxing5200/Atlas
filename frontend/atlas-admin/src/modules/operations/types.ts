import type { BidOpsId, PagedResult } from '@/modules/bidops/types'

export type BackgroundJobStatus = number | string
export type BackgroundJobDurationValue = number | string

export interface BackgroundJobSearchQuery {
  keyword?: string
  projectCode?: string | null
  tenantId?: BidOpsId | number | null
  queue?: string | null
  jobType?: string | null
  status?: BackgroundJobStatus | '' | null
  sourceModule?: string | null
  businessType?: string | null
  businessId?: BidOpsId | number | null
  correlationId?: string | null
  deadOnly?: boolean | null
  staleRunningOnly?: boolean | null
  waitingRetryOnly?: boolean | null
  createdFrom?: string | null
  createdTo?: string | null
  createdFromUtc?: string | null
  createdToUtc?: string | null
  sortBy?: 'CompletedAt' | 'CompletedAtUtc' | string | null
  sortDescending?: boolean | null
  pageIndex?: number
  pageSize?: number
}

export interface BackgroundJobListItemDto {
  id: BidOpsId
  jobType: string
  jobTypeName: string
  queue: string
  jobName: string
  projectCode: string
  deduplicationKey?: string | null
  tenantId?: BidOpsId | null
  storeId?: BidOpsId | null
  status: BackgroundJobStatus
  statusName: string
  priority: number
  createdAt: string
  availableAt: string
  startedAt?: string | null
  lockedAt?: string | null
  completedAt?: string | null
  nextAttemptAt?: string | null
  availableAtUtc: string
  startedAtUtc?: string | null
  lockedAtUtc?: string | null
  lockedBy?: string | null
  completedAtUtc?: string | null
  attemptCount: number
  maxAttempts: number
  nextAttemptAtUtc?: string | null
  isCancellationRequested: boolean
  cancellationRequestedAt?: string | null
  cancellationRequestedBy?: string | null
  cancellationReason?: string | null
  lastErrorPreview: string
  resultPreview: string
  payloadPreview: string
  isStaleRunning: boolean
  waitMilliseconds?: BackgroundJobDurationValue | null
  runMilliseconds?: BackgroundJobDurationValue | null
  waitSeconds?: BackgroundJobDurationValue | null
  runSeconds?: BackgroundJobDurationValue | null
}

export interface BackgroundJobDetailDto extends BackgroundJobListItemDto {
  payload: string
  lastError: string
  result: string
}

export interface BackgroundJobStatusCountDto {
  status: BackgroundJobStatus
  statusName: string
  count: number
}

export interface BackgroundJobDimensionCountDto {
  name: string
  displayName?: string | null
  count: number
}

export interface BackgroundJobSummaryDto {
  total: number
  pending: number
  running: number
  succeeded: number
  failed: number
  dead: number
  canceled: number
  staleRunning: number
  waitingRetry: number
  oldestPendingAt?: string | null
  recentErrorAt?: string | null
  oldestPendingAtUtc?: string | null
  recentErrorAtUtc?: string | null
  statusCounts: BackgroundJobStatusCountDto[]
  queueCounts: BackgroundJobDimensionCountDto[]
  jobTypeFailureCounts: BackgroundJobDimensionCountDto[]
}

export interface BackgroundJobRetryResultDto {
  originalJobId: BidOpsId
  newJobId: BidOpsId
  jobType: string
  jobTypeName: string
  queue: string
  message: string
}

export interface BackgroundJobCancelResultDto {
  jobId: BidOpsId
  status: BackgroundJobStatus
  statusName: string
  isCancellationRequested: boolean
  message: string
}

export interface BackgroundWorkerSearchQuery {
  keyword?: string
  onlineOnly?: boolean | null
  queue?: string | null
  pageIndex?: number
  pageSize?: number
}

export interface BackgroundWorkerHeartbeatDto {
  id: BidOpsId
  workerId: string
  hostName: string
  processId: number
  runtimeMode: string
  queues: string[]
  oneTimeJobWorkerEnabled: boolean
  recurringTaskRunnerEnabled: boolean
  currentJobId?: BidOpsId | null
  currentJobType?: string | null
  currentJobTypeName?: string | null
  currentQueue?: string | null
  startedAt: string
  lastSeenAt: string
  startedAtUtc: string
  lastSeenAtUtc: string
  isOnline: boolean
  secondsSinceLastSeen: number
}

export type BackgroundWorkerPagedResult = PagedResult<BackgroundWorkerHeartbeatDto>

export interface BidOpsConfigCheckItemDto {
  severity: 'Error' | 'Warning' | 'Info' | string
  code: string
  title: string
  message: string
}

export interface BidOpsConfigCheckDto {
  hasError: boolean
  hasWarning: boolean
  items: BidOpsConfigCheckItemDto[]
}

export interface BidOpsAiProviderOptionDto {
  provider: string
  label: string
  description: string
  model: string
  reasoningEffort: string
  available: boolean
  availabilityMessage: string
}

export interface BidOpsAiProviderSettingsDto {
  enabled: boolean
  noticeStagingEnabled: boolean
  outcomeSuppliersEnabled: boolean
  configuredProvider: string
  runtimeProvider: string
  effectiveProvider: string
  providerSource: string
  effectiveModel: string
  reasoningEffort: string
  deepSeekModel: string
  deepSeekTokenConfigured: boolean
  deepSeekTokenSource: string
  deepSeekTokenMasked: string
  deepSeekTokenUpdatedAt?: string | null
  deepSeekTokenUpdatedByUserName: string
  mimoModel: string
  mimoTokenConfigured: boolean
  mimoTokenSource: string
  mimoTokenMasked: string
  mimoTokenUpdatedAt?: string | null
  mimoTokenUpdatedByUserName: string
  codexCliModel: string
  codexCliReasoningEffort: string
  codexCliModelSource: string
  codexCliReasoningEffortSource: string
  codexCliScenarios: BidOpsCodexCliScenarioSettingsDto[]
  updatedAt?: string | null
  updatedByUserName: string
  options: BidOpsAiProviderOptionDto[]
}

export interface BidOpsCodexCliScenarioSettingsDto {
  scenario: string
  label: string
  description: string
  model: string
  reasoningEffort: string
  modelSource: string
  reasoningEffortSource: string
}

export interface UpdateBidOpsAiProviderRequest {
  provider: string
}

export interface UpdateBidOpsCodexCliSettingsRequest {
  model: string
  reasoningEffort: string
}

export interface UpdateBidOpsCodexCliScenarioSettingsRequest {
  scenario: string
  model: string
  reasoningEffort: string
}

export interface UpdateBidOpsDeepSeekTokenRequest {
  apiKey: string
}

export interface TestBidOpsDeepSeekTokenRequest {
  apiKey?: string | null
}

export interface BidOpsDeepSeekTokenTestResultDto {
  succeeded: boolean
  statusCode: number
  elapsedMilliseconds: number
  provider: string
  model: string
  endpoint: string
  message: string
  responsePreview: string
}

export interface UpdateBidOpsMimoTokenRequest {
  apiKey: string
}

export interface TestBidOpsMimoTokenRequest {
  apiKey?: string | null
}

export interface BidOpsMimoTokenTestResultDto {
  succeeded: boolean
  statusCode: number
  elapsedMilliseconds: number
  provider: string
  model: string
  endpoint: string
  message: string
  responsePreview: string
}

export interface BidOpsRuntimeStatusDto {
  taskPaused: boolean
  pauseReason: string
  pauseUpdatedAt?: string | null
  pauseUpdatedByUserName: string
  deferredUntil?: string | null
}

export interface UpdateBidOpsTaskPauseRequest {
  paused: boolean
  reason?: string | null
}

export interface BidOpsOperationsDashboardDto {
  backgroundJobWorkerEnabled: boolean
  recurringTaskRunnerEnabled: boolean
  bidOpsQueueConfigured: boolean
  aiSettings: BidOpsAiProviderSettingsDto
  runtimeStatus: BidOpsRuntimeStatusDto
  jobs: BackgroundJobSummaryDto
  rawNoticeCreatedToday: number
  reviewTaskCreatedToday: number
  parseQueuedRawNotices: number
  failedRawNotices: number
  pendingAttachments: number
  failedAttachments: number
  configWarnings: BidOpsConfigCheckItemDto[]
  recentFailedJobs: BackgroundJobListItemDto[]
}

export interface BidOpsChannelHealthDto {
  channelId: BidOpsId
  sourceId: BidOpsId
  sourceName: string
  sourceType: string
  channelName: string
  noticeType: string
  sourceEnabled: boolean
  channelEnabled: boolean
  enabled: boolean
  needLogin: boolean
  scheduleMode: string
  scanIntervalMinutes?: number | null
  dailyScanTime: string
  crawlIntervalMinutes: number
  lastScanTime?: string | null
  lastSuccessTime?: string | null
  lastError: string
  healthStatus: string
  nextDueAtUtc?: string | null
  minutesSinceLastSuccess?: number | null
  pendingJobs: number
  runningJobs: number
  failedJobs24h: number
  succeededJobs24h: number
  backfillStatus: string
  backfillNextCursor: string
  backfillScannedItemCount: number
  backfillCreatedCount: number
  backfillChangedCount: number
  backfillDuplicateCount: number
  backfillFailedItemCount: number
  backfillRemainingEstimate?: number | null
  alertLevel: string
  alertMessage: string
}

export interface BidOpsCrawlProgressDto {
  channelId: BidOpsId
  sourceId: BidOpsId
  sourceName: string
  sourceType: string
  channelName: string
  noticeType: string
  sourceEnabled: boolean
  channelEnabled: boolean
  mode: string
  status: string
  nextCursor: string
  lastSuccessfulCursor: string
  rangeStartPublishTime?: string | null
  rangeEndPublishTime?: string | null
  highWatermarkPublishTime?: string | null
  lowWatermarkPublishTime?: string | null
  totalRemoteCount?: number | null
  scannedItemCount: number
  createdCount: number
  changedCount: number
  duplicateCount: number
  failedItemCount: number
  remainingEstimate?: number | null
  startedAt?: string | null
  lastRunAt?: string | null
  completedAt?: string | null
  pausedAt?: string | null
  pauseReason: string
  lastError: string
  alertLevel: string
  alertMessage: string
}

export type BackgroundJobPagedResult = PagedResult<BackgroundJobListItemDto>
