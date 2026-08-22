import { apiGet } from '../lib/httpClient'
import type { DashboardSummary } from '../types/api'

export const dashboardApi = {
  async getSummary(): Promise<DashboardSummary> {
    return apiGet<DashboardSummary>('/Dashboard/summary')
  },
}
