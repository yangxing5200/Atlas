import { http } from '@/api/http'
import type {
  PagedResult,
  ProcessingFailureDto,
  ProcessingFailureSearchQuery,
} from '@/modules/bidops/types'

const base = '/bidops/processing'

export const processingApi = {
  failures(params: ProcessingFailureSearchQuery) {
    return http.get<PagedResult<ProcessingFailureDto>>(`${base}/failures`, { params })
  },
}
