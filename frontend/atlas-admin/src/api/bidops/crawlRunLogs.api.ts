import { http } from '@/api/http'
import type {
  BidOpsId,
  CrawlRunLogDto,
  CrawlRunLogSearchQuery,
  PagedResult,
} from '@/modules/bidops/types'

const base = '/bidops/crawl-run-logs'

export const crawlRunLogsApi = {
  search(params: CrawlRunLogSearchQuery) {
    return http.get<PagedResult<CrawlRunLogDto>>(base, { params })
  },

  get(id: BidOpsId) {
    return http.get<CrawlRunLogDto>(`${base}/${id}`)
  },
}
