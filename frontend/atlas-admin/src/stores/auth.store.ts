import { defineStore } from 'pinia'
import type { LoginResponse, UserDto } from '@/api/auth.api'

function readPermissions() {
  const raw = localStorage.getItem('ATLAS_PERMISSIONS')
  if (raw) {
    try {
      const parsed = JSON.parse(raw)
      return Array.isArray(parsed) ? parsed.filter((item) => typeof item === 'string') : []
    } catch {
      return []
    }
  }

  return []
}

function readJson<T>(key: string, fallback: T): T {
  const raw = localStorage.getItem(key)
  if (!raw) return fallback
  try {
    return JSON.parse(raw) as T
  } catch {
    return fallback
  }
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('ATLAS_TOKEN') || '',
    refreshToken: localStorage.getItem('ATLAS_REFRESH_TOKEN') || '',
    tenantId: localStorage.getItem('ATLAS_TENANT_ID') || '',
    storeId: localStorage.getItem('ATLAS_STORE_ID') || '',
    permissions: readPermissions(),
    contextLoaded: false,
    user: readJson<UserDto | null>('ATLAS_USER', null),
    expiresAt: localStorage.getItem('ATLAS_EXPIRES_AT') || '',
  }),

  getters: {
    isAuthenticated: (state) => Boolean(state.token),
    displayName: (state) => state.user?.realName || state.user?.userName || '未登录',
  },

  actions: {
    setToken(token: string) {
      this.token = token
      localStorage.setItem('ATLAS_TOKEN', token)
    },

    setSession(response: LoginResponse) {
      this.token = response.token
      this.refreshToken = response.refreshToken || ''
      this.user = response.user || null
      this.tenantId = response.user?.tenantId ? String(response.user.tenantId) : ''
      this.storeId = response.currentStore?.id ? String(response.currentStore.id) : ''
      this.expiresAt = response.expiresAt || ''

      localStorage.setItem('ATLAS_TOKEN', this.token)
      if (this.refreshToken) localStorage.setItem('ATLAS_REFRESH_TOKEN', this.refreshToken)
      else localStorage.removeItem('ATLAS_REFRESH_TOKEN')

      if (this.user) localStorage.setItem('ATLAS_USER', JSON.stringify(this.user))
      else localStorage.removeItem('ATLAS_USER')

      if (this.tenantId) localStorage.setItem('ATLAS_TENANT_ID', this.tenantId)
      else localStorage.removeItem('ATLAS_TENANT_ID')

      if (this.storeId) localStorage.setItem('ATLAS_STORE_ID', this.storeId)
      else localStorage.removeItem('ATLAS_STORE_ID')

      if (this.expiresAt) localStorage.setItem('ATLAS_EXPIRES_AT', this.expiresAt)
      else localStorage.removeItem('ATLAS_EXPIRES_AT')
    },

    setContext(context: { tenantId?: string; storeId?: string; permissions?: string[] }) {
      if (context.tenantId !== undefined) {
        this.tenantId = context.tenantId
        localStorage.setItem('ATLAS_TENANT_ID', context.tenantId)
      }

      if (context.storeId !== undefined) {
        this.storeId = context.storeId
        localStorage.setItem('ATLAS_STORE_ID', context.storeId)
      }

      if (context.permissions !== undefined) {
        this.permissions = context.permissions
        localStorage.setItem('ATLAS_PERMISSIONS', JSON.stringify(context.permissions))
      }

      this.contextLoaded = true
    },

    hasPermission(permission: string) {
      return this.permissions.includes(permission)
    },

    hasAllPermissions(permissions: string[] = []) {
      return permissions.every((permission) => this.hasPermission(permission))
    },

    hasAnyPermission(permissions: string[] = []) {
      return permissions.length === 0 || permissions.some((permission) => this.hasPermission(permission))
    },

    logout() {
      this.token = ''
      this.refreshToken = ''
      this.tenantId = ''
      this.storeId = ''
      this.permissions = []
      this.contextLoaded = false
      this.user = null
      this.expiresAt = ''
      localStorage.removeItem('ATLAS_TOKEN')
      localStorage.removeItem('ATLAS_REFRESH_TOKEN')
      localStorage.removeItem('ATLAS_TENANT_ID')
      localStorage.removeItem('ATLAS_STORE_ID')
      localStorage.removeItem('ATLAS_PERMISSIONS')
      localStorage.removeItem('ATLAS_USER')
      localStorage.removeItem('ATLAS_EXPIRES_AT')
    },
  },
})
