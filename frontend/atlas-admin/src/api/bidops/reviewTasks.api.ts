import { http } from '@/api/http'
import type {
  NoticeDto,
  PagedResult,
  ReviewDecisionRequest,
  ReviewTaskDetailDto,
  ReviewTaskDto,
  ReviewTaskSearchQuery,
  BidOpsId,
} from '@/modules/bidops/types'

const base = '/bidops/review-tasks'

export const reviewTasksApi = {
  search(params: ReviewTaskSearchQuery) {
    return http.get<PagedResult<ReviewTaskDto>>(base, { params })
  },

  get(id: BidOpsId) {
    return http.get<ReviewTaskDetailDto>(`${base}/${id}`)
  },

  approve(id: BidOpsId, data: ReviewDecisionRequest) {
    return http.post<NoticeDto>(`${base}/${id}/approve`, data)
  },

  ignore(id: BidOpsId, data: ReviewDecisionRequest) {
    return http.post<void>(`${base}/${id}/ignore`, data)
  },
}
