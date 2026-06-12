import { http } from '@/api/http'
import type { BidOpsPagedQuery, NoticeDto, PagedResult } from '@/modules/bidops/types'

const base = '/bidops/notices'

export const noticesApi = {
  search(params: BidOpsPagedQuery) {
    return http.get<PagedResult<NoticeDto>>(base, { params })
  },
}
