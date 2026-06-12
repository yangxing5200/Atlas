import { http } from '@/api/http'
import type {
  BidOpsId,
  PagedResult,
  SupplierMatchResultDto,
  SupplierMatchRunDetailDto,
  SupplierMatchRunDto,
  SupplierMatchRunSearchQuery,
} from '@/modules/bidops/types'

const base = '/bidops/matching/runs'

export const matchingApi = {
  searchRuns(params: SupplierMatchRunSearchQuery) {
    return http.get<PagedResult<SupplierMatchRunDto>>(base, { params })
  },

  getRun(id: BidOpsId) {
    return http.get<SupplierMatchRunDetailDto>(`${base}/${id}`)
  },

  results(id: BidOpsId) {
    return http.get<SupplierMatchResultDto[]>(`${base}/${id}/results`)
  },
}
