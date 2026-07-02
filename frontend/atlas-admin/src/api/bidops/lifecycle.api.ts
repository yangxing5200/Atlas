import { http } from '@/api/http'
import type {
  AmountCandidateDto,
  AmountCandidateOperationResultDto,
  BidOpsId,
  BidOpsReverseCloseJobRequest,
  EnqueueJobDto,
  LifecycleAmountCandidateDebugDto,
  LifecycleFinalAwardAmountClearRequest,
  LifecycleFinalAwardAmountClearResultDto,
  LifecyclePackageLinkBatchReviewRequest,
  LifecyclePackageLinkBatchReviewResultDto,
  LifecyclePackageLinkDecisionRequest,
  LifecyclePackageLinkDto,
  LifecyclePackageLinkSearchQuery,
  LifecycleFieldEnrichmentRequest,
  LifecycleOutcomeSupplierReparseRequest,
  LifecycleProcurementAutoCollectRequest,
  LifecycleProcurementAutoCollectResultDto,
  LifecycleProcurementNoticeCandidateDto,
  LifecycleProcurementNoticeImportRequest,
  LifecycleProcurementNoticeImportResultDto,
  LifecycleProjectCodeUpdateRequest,
  LifecycleProjectCodeUpdateResultDto,
  PagedResult,
} from '@/modules/bidops/types'

const base = '/bidops/lifecycle/debug'

export const lifecycleApi = {
  links(params: LifecyclePackageLinkSearchQuery) {
    return http.get<PagedResult<LifecyclePackageLinkDto>>(`${base}/links`, { params })
  },

  procurementCandidates(linkId: BidOpsId) {
    return http.get<LifecycleProcurementNoticeCandidateDto[]>(`${base}/links/${linkId}/procurement-candidates`, {
      headers: { 'Cache-Control': 'no-cache' },
      params: { _: Date.now() },
    })
  },

  amountCandidates(linkId: BidOpsId) {
    return http.get<AmountCandidateDto[]>(`${base}/links/${linkId}/amount-candidates`)
  },

  amountCandidateDebug(linkId: BidOpsId) {
    return http.get<LifecycleAmountCandidateDebugDto>(`${base}/links/${linkId}/amount-candidates/debug`)
  },

  selectAmountCandidate(linkId: BidOpsId, candidateId: BidOpsId, request: { remark?: string | null } = {}) {
    return http.post<AmountCandidateOperationResultDto>(`${base}/links/${linkId}/amount-candidates/${candidateId}/select`, request)
  },

  markAmountCandidateType(linkId: BidOpsId, candidateId: BidOpsId, request: { amountType: string; remark?: string | null }) {
    return http.post<AmountCandidateOperationResultDto>(`${base}/links/${linkId}/amount-candidates/${candidateId}/mark-type`, request)
  },

  rejectAmountCandidate(linkId: BidOpsId, candidateId: BidOpsId, request: { reason: string }) {
    return http.post<AmountCandidateOperationResultDto>(`${base}/links/${linkId}/amount-candidates/${candidateId}/reject`, request)
  },

  restoreAmountCandidate(linkId: BidOpsId, candidateId: BidOpsId, request: { remark?: string | null } = {}) {
    return http.post<AmountCandidateOperationResultDto>(`${base}/links/${linkId}/amount-candidates/${candidateId}/restore`, request)
  },

  clearFinalAwardAmounts(request: LifecycleFinalAwardAmountClearRequest) {
    return http.post<LifecycleFinalAwardAmountClearResultDto>(`${base}/links/final-award-amount/clear`, request)
  },

  importProcurementCandidate(linkId: BidOpsId, request: LifecycleProcurementNoticeImportRequest) {
    return http.post<LifecycleProcurementNoticeImportResultDto>(`${base}/links/${linkId}/procurement-candidates/import`, request)
  },

  autoCollectProcurementNotice(rawNoticeId: BidOpsId, request: LifecycleProcurementAutoCollectRequest = {}) {
    return http.post<LifecycleProcurementAutoCollectResultDto>(`${base}/award-notices/${rawNoticeId}/procurement-auto-collect`, request)
  },

  updateProjectCode(linkId: BidOpsId, request: LifecycleProjectCodeUpdateRequest) {
    return http.post<LifecycleProjectCodeUpdateResultDto>(`${base}/links/${linkId}/project-code`, request)
  },

  enqueueFieldEnrichment(linkId: BidOpsId, request: LifecycleFieldEnrichmentRequest) {
    return http.post<EnqueueJobDto>(`${base}/links/${linkId}/field-enrichment/enqueue`, request)
  },

  enqueueOutcomeSupplierReparse(rawNoticeId: BidOpsId, request: LifecycleOutcomeSupplierReparseRequest = {}) {
    return http.post<EnqueueJobDto>(`${base}/award-notices/${rawNoticeId}/outcome-supplier-reparse/enqueue`, request)
  },

  enqueueReverseClose(request: BidOpsReverseCloseJobRequest) {
    return http.post<EnqueueJobDto>(`${base}/reverse-close/enqueue`, request)
  },

  persistRawNotice(rawNoticeId: BidOpsId) {
    return http.post(`${base}/reverse-close-raw-notice/${rawNoticeId}/persist`)
  },

  confirmLink(id: BidOpsId, request: LifecyclePackageLinkDecisionRequest) {
    return http.post<LifecyclePackageLinkDto>(`${base}/links/${id}/confirm`, request)
  },

  batchReviewLinks(request: LifecyclePackageLinkBatchReviewRequest) {
    return http.post<LifecyclePackageLinkBatchReviewResultDto>(`${base}/links/batch-review`, request)
  },

  autoReviewAward(rawNoticeId: BidOpsId) {
    return http.post<LifecyclePackageLinkBatchReviewResultDto>(`${base}/award-notices/${rawNoticeId}/auto-review`)
  },

  rejectLink(id: BidOpsId, request: LifecyclePackageLinkDecisionRequest) {
    return http.post<LifecyclePackageLinkDto>(`${base}/links/${id}/reject`, request)
  },
}
