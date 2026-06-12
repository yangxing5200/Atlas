import { http } from '@/api/http'
import type {
  BidOpsPagedQuery,
  CrawlChannelDto,
  CreateCrawlChannelRequest,
  EnqueueJobDto,
  PagedResult,
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
}
