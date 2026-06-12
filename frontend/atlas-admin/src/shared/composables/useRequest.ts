import { ref } from 'vue'

export function useRequest() {
  const loading = ref(false)

  async function run<T>(request: () => Promise<T>) {
    loading.value = true
    try {
      return await request()
    } finally {
      loading.value = false
    }
  }

  return {
    get loading() {
      return loading.value
    },
    run,
  }
}
