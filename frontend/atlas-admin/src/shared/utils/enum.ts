export type ElementTagType = 'primary' | 'success' | 'info' | 'warning' | 'danger'

export function formatEnumValue(value: unknown) {
  if (value === null || value === undefined || value === '') return '-'
  return String(value)
}

export function statusTagType(value: unknown): ElementTagType {
  const normalized = String(value ?? '').toLowerCase()

  if (['succeeded', 'approved', 'enabled', 'parsed', 'true', 'completed', 'go', 'high'].includes(normalized)) {
    return 'success'
  }

  if (['failed', 'ignored', 'disabled', 'false', 'error', 'rejected', 'noticefailed', 'attachmentlistfailed', 'nogo', 'closed', 'archived'].includes(normalized)) {
    return 'danger'
  }

  if (['pending', 'running', 'new', 'queued', 'parsequeued', 'inreview', 'ratelimited', 'assessing', 'hold', 'medium'].includes(normalized)) {
    return 'warning'
  }

  return 'info'
}
