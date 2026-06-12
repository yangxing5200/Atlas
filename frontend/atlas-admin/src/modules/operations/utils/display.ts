import type { BackgroundJobStatus } from '../types'

const statusLabels: Record<string, string> = {
  '0': '待执行',
  '1': '执行中',
  '2': '成功',
  '3': '失败待重试',
  '4': '死亡',
  '5': '已取消',
  Pending: '待执行',
  Running: '执行中',
  Succeeded: '成功',
  Failed: '失败待重试',
  Dead: '死亡',
  Canceled: '已取消',
}

const healthLabels: Record<string, string> = {
  Healthy: '健康',
  Due: '待扫描',
  Stale: '超期未成功',
  Failed: '最近失败',
  Disabled: '栏目停用',
  SourceDisabled: '来源停用',
  SkippedNeedLogin: '需登录已跳过',
  NeverSucceeded: '从未成功',
}

export const jobStatusOptions = [
  { label: '待执行', value: 'Pending' },
  { label: '执行中', value: 'Running' },
  { label: '成功', value: 'Succeeded' },
  { label: '失败待重试', value: 'Failed' },
  { label: '死亡', value: 'Dead' },
  { label: '已取消', value: 'Canceled' },
]

export function formatJobStatus(value?: BackgroundJobStatus | null, statusName?: string | null) {
  const key = statusName || (value === null || value === undefined ? '' : String(value))
  return statusLabels[key] || key || '-'
}

export function jobStatusTagType(value?: BackgroundJobStatus | null, statusName?: string | null) {
  const key = statusName || (value === null || value === undefined ? '' : String(value))
  if (key === '2' || key === 'Succeeded') return 'success'
  if (key === '1' || key === 'Running') return 'primary'
  if (key === '3' || key === 'Failed') return 'warning'
  if (key === '4' || key === 'Dead') return 'danger'
  if (key === '5' || key === 'Canceled') return 'info'
  return 'info'
}

export function formatSeconds(value?: number | null) {
  if (value === null || value === undefined) return '-'
  if (value < 60) return `${value}s`
  const minutes = Math.floor(value / 60)
  const seconds = value % 60
  if (minutes < 60) return seconds ? `${minutes}m ${seconds}s` : `${minutes}m`
  const hours = Math.floor(minutes / 60)
  const restMinutes = minutes % 60
  return restMinutes ? `${hours}h ${restMinutes}m` : `${hours}h`
}

export function severityType(severity?: string) {
  if (severity === 'Error') return 'error'
  if (severity === 'Warning') return 'warning'
  return 'info'
}

export function formatHealthStatus(value?: string | null) {
  if (!value) return '-'
  return healthLabels[value] || value
}

export function healthStatusTagType(value?: string | null) {
  if (value === 'Healthy') return 'success'
  if (value === 'Due' || value === 'NeverSucceeded') return 'warning'
  if (value === 'Failed' || value === 'Stale') return 'danger'
  return 'info'
}
