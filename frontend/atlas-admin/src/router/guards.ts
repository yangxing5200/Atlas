import type { Router } from 'vue-router'
import { authApi } from '@/api/auth.api'
import { useAuthStore } from '@/stores/auth.store'

function getSafeRedirect(value: unknown) {
  return typeof value === 'string' && value.startsWith('/') ? value : '/bidops'
}

export function setupRouterGuards(router: Router) {
  router.beforeEach(async (to) => {
    const auth = useAuthStore()
    const isPublicRoute = to.meta.public === true

    if (isPublicRoute) {
      if (to.path === '/login' && auth.isAuthenticated) {
        return getSafeRedirect(to.query.redirect)
      }
      return true
    }

    if (!auth.isAuthenticated) {
      return {
        path: '/login',
        query: { redirect: to.fullPath },
      }
    }

    if (!auth.contextLoaded || auth.permissions.length === 0) {
      try {
        const context = await authApi.context()
        auth.setContext({
          tenantId: String(context.tenantId),
          storeId: context.storeId ? String(context.storeId) : '',
          permissions: context.permissions,
        })
      } catch {
        return {
          path: '/login',
          query: { redirect: to.fullPath },
        }
      }
    }

    const requiredAll = to.meta.permissions || []
    const requiredAny = to.meta.permissionsAny || []

    if (requiredAll.length > 0 && !auth.hasAllPermissions(requiredAll)) {
      return '/403'
    }

    if (requiredAny.length > 0 && !auth.hasAnyPermission(requiredAny)) {
      return '/403'
    }

    return true
  })
}
