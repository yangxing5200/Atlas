import { http } from '@/api/http'
import type {
  BackgroundWorkerPagedResult,
  BackgroundWorkerSearchQuery,
} from '@/modules/operations/types'

const base = '/ops/workers'

export const workersApi = {
  search(params: BackgroundWorkerSearchQuery) {
    return http.get<BackgroundWorkerPagedResult>(base, { params })
  },
}
