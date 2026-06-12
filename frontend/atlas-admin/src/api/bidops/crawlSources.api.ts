import { http } from '@/api/http'
import type {
  BidOpsPagedQuery,
  CrawlSourceDto,
  CreateCrawlSourceRequest,
  PagedResult,
  ReviewDecisionRequest,
  UpdateCrawlSourceRequest,
  BidOpsId,
} from '@/modules/bidops/types'

const base = '/bidops/crawl-sources'

export const crawlSourcesApi = {
  search(params: BidOpsPagedQuery) {
    return http.get<PagedResult<CrawlSourceDto>>(base, { params })
  },

  create(data: CreateCrawlSourceRequest) {
    return http.post<CrawlSourceDto>(base, data)
  },

  update(id: BidOpsId, data: UpdateCrawlSourceRequest) {
    return http.put<void>(`${base}/${id}`, data)
  },

  enable(id: BidOpsId) {
    return http.post<void>(`${base}/${id}/enable`)
  },

  disable(id: BidOpsId, data?: ReviewDecisionRequest) {
    return http.post<void>(`${base}/${id}/disable`, data || {})
  },
}
