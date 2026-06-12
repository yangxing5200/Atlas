import { http } from '@/api/http'
import type {
  BidOpsId,
  CreateSupplierCapabilityRequest,
  CreateSupplierContactRequest,
  CreateSupplierEvidenceDocumentRequest,
  CreateSupplierRequest,
  OutcomeSupplierBackfillEnqueueDto,
  OutcomeSupplierRecordDto,
  OutcomeSupplierSearchQuery,
  PagedResult,
  SupplierAnalysisSummaryDto,
  SupplierOutcomeSummaryDto,
  SupplierCapabilityDto,
  SupplierContactDto,
  SupplierDetailDto,
  SupplierDto,
  SupplierEvidenceDocumentDto,
  SupplierSearchQuery,
  UpdateSupplierRequest,
} from '@/modules/bidops/types'

const base = '/bidops/suppliers'

export const suppliersApi = {
  search(params: SupplierSearchQuery) {
    return http.get<PagedResult<SupplierDto>>(base, { params })
  },

  get(id: BidOpsId) {
    return http.get<SupplierDetailDto>(`${base}/${id}`)
  },

  analysisSummary() {
    return http.get<SupplierAnalysisSummaryDto>(`${base}/analysis/summary`)
  },

  outcomeRecords(params: OutcomeSupplierSearchQuery) {
    return http.get<PagedResult<OutcomeSupplierRecordDto>>(`${base}/outcome-records`, { params })
  },

  outcomeSummary() {
    return http.get<SupplierOutcomeSummaryDto>(`${base}/outcome-summary`)
  },

  backfillOutcomeRecords(maxItems = 200) {
    return http.post<OutcomeSupplierBackfillEnqueueDto>(`${base}/outcome-records/backfill`, undefined, {
      params: { maxItems },
    })
  },

  create(request: CreateSupplierRequest) {
    return http.post<SupplierDto>(base, request)
  },

  update(id: BidOpsId, request: UpdateSupplierRequest) {
    return http.put<SupplierDto>(`${base}/${id}`, request)
  },

  addContact(id: BidOpsId, request: CreateSupplierContactRequest) {
    return http.post<SupplierContactDto>(`${base}/${id}/contacts`, request)
  },

  addCapability(id: BidOpsId, request: CreateSupplierCapabilityRequest) {
    return http.post<SupplierCapabilityDto>(`${base}/${id}/capabilities`, request)
  },

  addEvidenceDocument(id: BidOpsId, request: CreateSupplierEvidenceDocumentRequest) {
    return http.post<SupplierEvidenceDocumentDto>(`${base}/${id}/evidence-documents`, request)
  },
}
