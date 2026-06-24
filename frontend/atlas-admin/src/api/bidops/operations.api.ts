import { http } from '@/api/http'
import type {
  BackgroundJobCancelResultDto,
  BackgroundJobPagedResult,
  BackgroundJobRetryResultDto,
  BackgroundJobSearchQuery,
  BidOpsAiProviderSettingsDto,
  BidOpsChannelHealthDto,
  BidOpsConfigCheckDto,
  BidOpsCrawlProgressDto,
  BidOpsOperationsDashboardDto,
  BidOpsRuntimeStatusDto,
  UpdateBidOpsAiProviderRequest,
  UpdateBidOpsCodexCliSettingsRequest,
  UpdateBidOpsCodexCliScenarioSettingsRequest,
  UpdateBidOpsTaskPauseRequest,
} from '@/modules/operations/types'
import type { BidOpsId, RawNoticePipelineDto } from '@/modules/bidops/types'

const base = '/bidops/operations'

export const bidOpsOperationsApi = {
  dashboard() {
    return http.get<BidOpsOperationsDashboardDto>(`${base}/dashboard`)
  },

  jobs(params: BackgroundJobSearchQuery) {
    return http.get<BackgroundJobPagedResult>(`${base}/jobs`, { params })
  },

  configCheck() {
    return http.get<BidOpsConfigCheckDto>(`${base}/config-check`)
  },

  aiSettings() {
    return http.get<BidOpsAiProviderSettingsDto>(`${base}/ai-settings`)
  },

  updateAiProvider(request: UpdateBidOpsAiProviderRequest) {
    return http.put<BidOpsAiProviderSettingsDto>(`${base}/ai-settings/provider`, request)
  },

  updateCodexCliSettings(request: UpdateBidOpsCodexCliSettingsRequest) {
    return http.put<BidOpsAiProviderSettingsDto>(`${base}/ai-settings/codex-cli`, request)
  },

  updateCodexCliScenarioSettings(request: UpdateBidOpsCodexCliScenarioSettingsRequest) {
    return http.put<BidOpsAiProviderSettingsDto>(`${base}/ai-settings/codex-cli/scenario`, request)
  },

  updateTaskPause(request: UpdateBidOpsTaskPauseRequest) {
    return http.put<BidOpsRuntimeStatusDto>(`${base}/runtime/task-pause`, request)
  },

  channelsHealth() {
    return http.get<BidOpsChannelHealthDto[]>(`${base}/channels/health`)
  },

  crawlProgress() {
    return http.get<BidOpsCrawlProgressDto[]>(`${base}/crawl-progress`)
  },

  rawNoticePipeline(id: BidOpsId) {
    return http.get<RawNoticePipelineDto>(`${base}/raw-notices/${id}/pipeline`)
  },

  retryJob(id: string) {
    return http.post<BackgroundJobRetryResultDto>(`${base}/jobs/${id}/retry`)
  },

  cancelJob(id: string, reason?: string, force = false) {
    return http.post<BackgroundJobCancelResultDto>(`${base}/jobs/${id}/cancel`, { reason, force })
  },
}
