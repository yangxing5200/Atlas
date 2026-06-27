import { http } from '@/api/http'
import type {
  BidOpsId,
  BidOpsReverseCloseJobRequest,
  EnqueueJobDto,
  LifecyclePackageLinkDecisionRequest,
  LifecyclePackageLinkDto,
  LifecyclePackageLinkSearchQuery,
  LifecycleFieldEnrichmentRequest,
  LifecycleProcurementNoticeCandidateDto,
  LifecycleProcurementNoticeImportRequest,
  LifecycleProcurementNoticeImportResultDto,
  PagedResult,
} from '@/modules/bidops/types'

const base = '/bidops/lifecycle/debug'

export const lifecycleApi = {
  links(params: LifecyclePackageLinkSearchQuery) {
    return http.get<PagedResult<LifecyclePackageLinkDto>>(`${base}/links`, { params })
  },

  procurementCandidates(linkId: BidOpsId) {
    return http.get<LifecycleProcurementNoticeCandidateDto[]>(`${base}/links/${linkId}/procurement-candidates`)
  },

  importProcurementCandidate(linkId: BidOpsId, request: LifecycleProcurementNoticeImportRequest) {
    return http.post<LifecycleProcurementNoticeImportResultDto>(`${base}/links/${linkId}/procurement-candidates/import`, request)
  },

  enqueueFieldEnrichment(linkId: BidOpsId, request: LifecycleFieldEnrichmentRequest) {
    return http.post<EnqueueJobDto>(`${base}/links/${linkId}/field-enrichment/enqueue`, request)
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

  rejectLink(id: BidOpsId, request: LifecyclePackageLinkDecisionRequest) {
    return http.post<LifecyclePackageLinkDto>(`${base}/links/${id}/reject`, request)
  },
}
