import { http } from '@/api/http'
import type { NoticeDto, NoticeSearchQuery, PagedResult } from '@/modules/bidops/types'

const base = '/bidops/notices'

export const noticesApi = {
  search(params: NoticeSearchQuery) {
    return http.get<PagedResult<NoticeDto>>(base, { params })
  },
}
