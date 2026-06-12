import type { BidOpsId, PagedResult } from '@/modules/bidops/types'

export type BackgroundJobStatus = number | string

export interface BackgroundJobSearchQuery {
  keyword?: string
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
  createdFromUtc?: string | null
  createdToUtc?: string | null
  pageIndex?: number
  pageSize?: number
}

export interface BackgroundJobListItemDto {
  id: BidOpsId
  jobType: string
  queue: string
  jobName: string
  deduplicationKey?: string | null
  tenantId?: BidOpsId | null
  storeId?: BidOpsId | null
  status: BackgroundJobStatus
  statusName: string
  priority: number
  createdAt: string
  availableAtUtc: string
  startedAtUtc?: string | null
  lockedAtUtc?: string | null
  lockedBy?: string | null
  completedAtUtc?: string | null
  attemptCount: number
  maxAttempts: number
  nextAttemptAtUtc?: string | null
  lastErrorPreview: string
  resultPreview: string
  payloadPreview: string
  isStaleRunning: boolean
  waitSeconds?: number | null
  runSeconds?: number | null
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
  queue: string
  message: string
}

export interface BackgroundJobCancelResultDto {
  jobId: BidOpsId
  status: BackgroundJobStatus
  statusName: string
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
  currentQueue?: string | null
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

export interface BidOpsOperationsDashboardDto {
  backgroundJobWorkerEnabled: boolean
  recurringTaskRunnerEnabled: boolean
  bidOpsQueueConfigured: boolean
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
}

export type BackgroundJobPagedResult = PagedResult<BackgroundJobListItemDto>
