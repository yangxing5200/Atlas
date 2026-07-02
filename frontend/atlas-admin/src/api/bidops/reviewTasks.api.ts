import { http } from '@/api/http'
import type {
  BackgroundJobPagedResult,
  BackgroundJobSearchQuery,
} from '@/modules/operations/types'
import type {
  NoticeDto,
  PagedResult,
  EnqueueJobDto,
  BatchReviewTaskReparseRequest,
  BulkApproveReviewTasksRequest,
  BulkReviewTaskActionResultDto,
  OutcomeSupplierRecordDto,
  ReviewDecisionRequest,
  ReviewCorrectionAnalysisDto,
  ReviewCorrectionAnalysisQuery,
  ReviewEfficiencyMetricsDto,
  ReviewOutcomeAiReparseRequest,
  ReviewOutcomeSupplierRecordEditRequest,
  ReviewQualityBackfillRequest,
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

  correctionAnalysis(params?: ReviewCorrectionAnalysisQuery) {
    return http.get<ReviewCorrectionAnalysisDto>(`${base}/corrections/analysis`, { params })
  },

  efficiencyMetrics() {
    return http.get<ReviewEfficiencyMetricsDto>(`${base}/efficiency-metrics`)
  },

  get(id: BidOpsId) {
    return http.get<ReviewTaskDetailDto>(`${base}/${id}`)
  },

  jobs(id: BidOpsId, params?: BackgroundJobSearchQuery) {
    return http.get<BackgroundJobPagedResult>(`${base}/${id}/jobs`, { params })
  },

  bulkApprove(data: BulkApproveReviewTasksRequest) {
    return http.post<BulkReviewTaskActionResultDto>(`${base}/bulk-approve`, data)
  },

  bulkApproveJob(data: BulkApproveReviewTasksRequest) {
    return http.post<EnqueueJobDto>(`${base}/bulk-approve/job`, data)
  },

  batchReparse(data: BatchReviewTaskReparseRequest) {
    return http.post<BulkReviewTaskActionResultDto>(`${base}/batch-reparse`, data)
  },

  qualityBackfill(data: ReviewQualityBackfillRequest) {
    return http.post<EnqueueJobDto>(`${base}/quality-backfill`, data)
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
