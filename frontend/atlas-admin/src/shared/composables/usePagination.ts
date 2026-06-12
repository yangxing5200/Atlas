import { reactive } from 'vue'

export function usePagination(pageSize = 20) {
  return reactive({
    pageIndex: 1,
    pageSize,
  })
}
