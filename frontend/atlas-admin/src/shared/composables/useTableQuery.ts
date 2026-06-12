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
  options: { immediate?: boolean } = {},
) {
  const query = reactive({ ...initialQuery }) as Q
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
