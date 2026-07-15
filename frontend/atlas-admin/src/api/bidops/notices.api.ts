import { http } from '@/api/http'
import type { NoticeDto, NoticeSearchQuery, PagedResult, UpdateNoticeRequest } from '@/modules/bidops/types'

const base = '/bidops/notices'

export const noticesApi = {
  search(params: NoticeSearchQuery) {
    return http.get<PagedResult<NoticeDto>>(base, { params })
  },

  get(id: string) {
    return http.get<NoticeDto>(`${base}/${id}`)
  },

  update(id: string, request: UpdateNoticeRequest) {
    return http.put<NoticeDto>(`${base}/${id}`, request)
  },
}
