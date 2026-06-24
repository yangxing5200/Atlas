import { http } from '@/api/http'
import type {
  BidOpsPagedQuery,
  CrawlChannelDto,
  ContinueCrawlCheckpointRequest,
  CreateCrawlChannelRequest,
  EnqueueJobDto,
  PagedResult,
  PauseCrawlCheckpointRequest,
  ResetCrawlCheckpointRequest,
  SetCrawlChannelEnabledRequest,
  StartCrawlBackfillRequest,
  UpdateCrawlChannelRequest,
  BidOpsId,
} from '@/modules/bidops/types'

const base = '/bidops/crawl-channels'

export const crawlChannelsApi = {
  search(params: BidOpsPagedQuery) {
    return http.get<PagedResult<CrawlChannelDto>>(base, { params })
  },

  create(data: CreateCrawlChannelRequest) {
    return http.post<CrawlChannelDto>(base, data)
  },

  update(id: BidOpsId, data: UpdateCrawlChannelRequest) {
    return http.put<void>(`${base}/${id}`, data)
  },

  scanNow(id: BidOpsId) {
    return http.post<EnqueueJobDto>(`${base}/${id}/scan-now`)
  },

  setEnabled(id: BidOpsId, data: SetCrawlChannelEnabledRequest) {
    return http.post<void>(`${base}/${id}/enabled`, data)
  },

  startBackfill(id: BidOpsId, data: StartCrawlBackfillRequest) {
    return http.post<EnqueueJobDto>(`${base}/${id}/backfill`, data)
  },

  continueCheckpoint(id: BidOpsId, data: ContinueCrawlCheckpointRequest) {
    return http.post<EnqueueJobDto>(`${base}/${id}/checkpoint/continue`, data)
  },

  pauseCheckpoint(id: BidOpsId, data: PauseCrawlCheckpointRequest) {
    return http.post<void>(`${base}/${id}/checkpoint/pause`, data)
  },

  resumeCheckpoint(id: BidOpsId, data: ContinueCrawlCheckpointRequest) {
    return http.post<EnqueueJobDto>(`${base}/${id}/checkpoint/resume`, data)
  },

  resetCheckpoint(id: BidOpsId, data: ResetCrawlCheckpointRequest) {
    return http.post<void>(`${base}/${id}/checkpoint/reset`, data)
  },
}
