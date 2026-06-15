import { http } from '@/api/http'
import type {
  NoticeDto,
  PagedResult,
  EnqueueJobDto,
  OutcomeSupplierRecordDto,
  ReviewDecisionRequest,
  ReviewOutcomeAiReparseRequest,
  ReviewOutcomeSupplierRecordEditRequest,
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

  outcomeAiReparse(id: BidOpsId, data: ReviewOutcomeAiReparseRequest) {
    return http.post<EnqueueJobDto>(`${base}/${id}/outcome-ai-reparse`, data)
  },

  addOutcomeSupplier(id: BidOpsId, data: ReviewOutcomeSupplierRecordEditRequest) {
    return http.post<OutcomeSupplierRecordDto>(`${base}/${id}/outcome-suppliers`, data)
  },

  updateOutcomeSupplier(id: BidOpsId, recordId: BidOpsId, data: ReviewOutcomeSupplierRecordEditRequest) {
    return http.put<OutcomeSupplierRecordDto>(`${base}/${id}/outcome-suppliers/${recordId}`, data)
  },

  deleteOutcomeSupplier(id: BidOpsId, recordId: BidOpsId) {
    return http.delete<void>(`${base}/${id}/outcome-suppliers/${recordId}`)
  },
}
