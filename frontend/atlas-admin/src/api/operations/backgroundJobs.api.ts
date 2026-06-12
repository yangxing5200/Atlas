import { http } from '@/api/http'
import type {
  BackgroundJobCancelResultDto,
  BackgroundJobDetailDto,
  BackgroundJobPagedResult,
  BackgroundJobRetryResultDto,
  BackgroundJobSearchQuery,
  BackgroundJobSummaryDto,
} from '@/modules/operations/types'

const base = '/ops/background-jobs'

export const backgroundJobsApi = {
  search(params: BackgroundJobSearchQuery) {
    return http.get<BackgroundJobPagedResult>(base, { params })
  },

  summary(params: BackgroundJobSearchQuery = {}) {
    return http.get<BackgroundJobSummaryDto>(`${base}/summary`, { params })
  },

  get(id: string) {
    return http.get<BackgroundJobDetailDto>(`${base}/${id}`)
  },

  retry(id: string) {
    return http.post<BackgroundJobRetryResultDto>(`${base}/${id}/retry`)
  },

  cancel(id: string, reason?: string) {
    return http.post<BackgroundJobCancelResultDto>(`${base}/${id}/cancel`, { reason })
  },
}
