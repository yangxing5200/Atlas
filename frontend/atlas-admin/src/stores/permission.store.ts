import { defineStore } from 'pinia'
import { useAuthStore } from './auth.store'

export const usePermissionStore = defineStore('permission', {
  getters: {
    can: () => (permission: string) => useAuthStore().hasPermission(permission),
    canAny: () => (permissions: string[]) => useAuthStore().hasAnyPermission(permissions),
  },
})
