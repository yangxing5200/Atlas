import { http } from '@/api/http'
import type { BidOpsDashboardSummaryDto } from '@/modules/bidops/types'

const base = '/bidops/dashboard'

export const bidOpsDashboardApi = {
  summary() {
    return http.get<BidOpsDashboardSummaryDto>(`${base}/summary`)
  },
}
