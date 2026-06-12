import { computed } from 'vue'
import { useAuthStore } from '@/stores/auth.store'

export function usePermission(permission?: string, permissionsAny?: string[]) {
  const auth = useAuthStore()

  const visible = computed(() => {
    if (permission) return auth.hasPermission(permission)
    if (permissionsAny?.length) return auth.hasAnyPermission(permissionsAny)
    return true
  })

  return { visible }
}
