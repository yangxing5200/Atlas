import { http } from '@/api/http'
import type {
  AssessOpportunityRequest,
  BidOpsId,
  ChangeOpportunityStageRequest,
  CreateOpportunityRequest,
  OpportunityDetailDto,
  OpportunityDto,
  OpportunitySearchQuery,
  PagedResult,
  UpdateOpportunityRequest,
  WatchOpportunityRequest,
} from '@/modules/bidops/types'

const base = '/bidops/opportunities'

export const opportunitiesApi = {
  search(params: OpportunitySearchQuery) {
    return http.get<PagedResult<OpportunityDto>>(base, { params })
  },

  get(id: BidOpsId) {
    return http.get<OpportunityDetailDto>(`${base}/${id}`)
  },

  create(request: CreateOpportunityRequest) {
    return http.post<OpportunityDto>(base, request)
  },

  update(id: BidOpsId, request: UpdateOpportunityRequest) {
    return http.put<OpportunityDto>(`${base}/${id}`, request)
  },

  watch(id: BidOpsId, request: WatchOpportunityRequest) {
    return http.post<OpportunityDto>(`${base}/${id}/watch`, request)
  },

  assess(id: BidOpsId, request: AssessOpportunityRequest) {
    return http.post<OpportunityDto>(`${base}/${id}/assess`, request)
  },

  changeStage(id: BidOpsId, request: ChangeOpportunityStageRequest) {
    return http.post<OpportunityDto>(`${base}/${id}/stage`, request)
  },
}
