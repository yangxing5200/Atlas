import { onMounted, reactive, ref, toRaw } from 'vue'
import type { PagedResult } from '@/modules/bidops/types'

const emptyResult = <T>(): PagedResult<T> => ({
  total: 0,
  items: [],
  pageIndex: 1,
  pageSize: 20,
  totalPages: 0,
  hasPrevious: false,
  hasNext: false,
})

export function useTableQuery<T, Q extends { pageIndex: number; pageSize: number }>(
  fetcher: (query: Q) => Promise<PagedResult<T>>,
  initialQuery: Q,
  options: { immediate?: boolean; storageKey?: string; storageVersion?: number } = {},
) {
  const query = reactive({ ...initialQuery, ...loadCachedQuery(initialQuery, options) }) as Q
  const result = reactive<PagedResult<T>>(emptyResult<T>())
  const loading = ref(false)

  async function loadData() {
    loading.value = true
    try {
      Object.assign(result, normalizePagedResult(await fetcher({ ...(toRaw(query) as Q) })))
    } catch {
      Object.assign(result, emptyResult<T>(), { pageIndex: query.pageIndex, pageSize: query.pageSize })
    } finally {
      loading.value = false
    }
  }

  async function search() {
    query.pageIndex = 1
    await loadData()
    saveCachedQuery(query, options)
  }

  async function reset(partial?: Partial<Q>) {
    Object.assign(query, initialQuery, partial || {})
    await search()
  }

  if (options.immediate !== false) {
    onMounted(loadData)
  }

  return {
    query,
    result,
    get loading() {
      return loading.value
    },
    loadData,
    search,
    reset,
  }
}

function normalizePagedResult<T>(value: PagedResult<T>): PagedResult<T> {
  return {
    ...value,
    total: Number(value.total || 0),
    pageIndex: Number(value.pageIndex || 1),
    pageSize: Number(value.pageSize || 20),
    totalPages: Number(value.totalPages || 0),
  }
}

function loadCachedQuery<Q extends { pageIndex: number; pageSize: number }>(
  initialQuery: Q,
  options: { storageKey?: string; storageVersion?: number },
): Partial<Q> {
  if (!options.storageKey || typeof window === 'undefined')
    return {}

  try {
    const raw = window.localStorage.getItem(options.storageKey)
    if (!raw)
      return {}

    const parsed = JSON.parse(raw) as { version?: number; query?: Record<string, unknown> }
    if (parsed.version !== (options.storageVersion ?? 1) || !parsed.query)
      return {}

    const cached: Partial<Q> = {}
    for (const key of Object.keys(initialQuery) as Array<keyof Q>) {
      if (Object.prototype.hasOwnProperty.call(parsed.query, String(key))) {
        cached[key] = parsed.query[String(key)] as Q[keyof Q]
      }
    }

    return cached
  } catch {
    return {}
  }
}

function saveCachedQuery<Q extends { pageIndex: number; pageSize: number }>(
  query: Q,
  options: { storageKey?: string; storageVersion?: number },
) {
  if (!options.storageKey || typeof window === 'undefined')
    return

  try {
    window.localStorage.setItem(
      options.storageKey,
      JSON.stringify({
        version: options.storageVersion ?? 1,
        query: toRaw(query),
      }),
    )
  } catch {
    // Ignore storage quota/privacy-mode failures; the table should still work normally.
  }
}
