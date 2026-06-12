import { http } from '@/api/http'
import type {
  PackageSearchQuery,
  PackageHistoricalSupplierLeadDto,
  PackageTimelineItemDto,
  PagedResult,
  RequirementItemDto,
  StartSupplierMatchRunRequest,
  StartSupplierMatchRunResponse,
  CreateGoNoGoDecisionRequest,
  GoNoGoDecisionDto,
  TenderPackageDto,
  BidOpsId,
} from '@/modules/bidops/types'

const base = '/bidops/packages'

export const packagesApi = {
  search(params: PackageSearchQuery) {
    return http.get<PagedResult<TenderPackageDto>>(base, { params })
  },

  get(id: BidOpsId) {
    return http.get<TenderPackageDto>(`${base}/${id}`)
  },

  timeline(id: BidOpsId) {
    return http.get<PackageTimelineItemDto[]>(`${base}/${id}/timeline`)
  },

  requirements(id: BidOpsId) {
    return http.get<RequirementItemDto[]>(`${base}/${id}/requirements`)
  },

  historicalSuppliers(id: BidOpsId) {
    return http.get<PackageHistoricalSupplierLeadDto[]>(`${base}/${id}/historical-suppliers`)
  },

  matchSuppliers(id: BidOpsId, request: StartSupplierMatchRunRequest) {
    return http.post<StartSupplierMatchRunResponse>(`${base}/${id}/match-suppliers`, request)
  },

  decisions(id: BidOpsId) {
    return http.get<GoNoGoDecisionDto[]>(`${base}/${id}/decisions`)
  },

  createDecision(id: BidOpsId, request: CreateGoNoGoDecisionRequest) {
    return http.post<GoNoGoDecisionDto>(`${base}/${id}/decisions`, request)
  },
}
