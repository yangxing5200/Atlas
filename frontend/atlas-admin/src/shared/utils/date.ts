import dayjs from 'dayjs'

const isoDateTimeWithoutZonePattern = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?$/

export function formatDateTime(value?: string | null) {
  if (!value) return '-'

  const normalizedValue = normalizeDateTimeInput(value)
  const parsed = dayjs(normalizedValue)
  return parsed.isValid() ? parsed.format('YYYY-MM-DD HH:mm') : '-'
}

function normalizeDateTimeInput(value: string) {
  const trimmed = value.trim()
  return isoDateTimeWithoutZonePattern.test(trimmed) ? `${trimmed}Z` : trimmed
}
