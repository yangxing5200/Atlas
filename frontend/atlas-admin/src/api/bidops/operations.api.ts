import { http } from '@/api/http'
import type {
  BackgroundJobCancelResultDto,
  BackgroundJobPagedResult,
  BackgroundJobRetryResultDto,
  BackgroundJobSearchQuery,
  BidOpsChannelHealthDto,
  BidOpsConfigCheckDto,
  BidOpsOperationsDashboardDto,
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

  channelsHealth() {
    return http.get<BidOpsChannelHealthDto[]>(`${base}/channels/health`)
  },

  rawNoticePipeline(id: BidOpsId) {
    return http.get<RawNoticePipelineDto>(`${base}/raw-notices/${id}/pipeline`)
  },

  retryJob(id: string) {
    return http.post<BackgroundJobRetryResultDto>(`${base}/jobs/${id}/retry`)
  },

  cancelJob(id: string, reason?: string) {
    return http.post<BackgroundJobCancelResultDto>(`${base}/jobs/${id}/cancel`, { reason })
  },
}
