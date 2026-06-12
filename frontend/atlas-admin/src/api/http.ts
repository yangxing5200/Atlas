import axios, {
  type AxiosError,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from 'axios'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '@/stores/auth.store'
import type { ApiErrorPayload } from './types'

const client = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 30_000,
  withCredentials: true,
})

client.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const auth = useAuthStore()

  if (auth.token) {
    config.headers.Authorization = `Bearer ${auth.token}`
  }

  if (auth.tenantId) {
    config.headers['X-Tenant-Id'] = auth.tenantId
  }

  if (auth.storeId) {
    config.headers['X-Store-Id'] = auth.storeId
  }

  return config
})

client.interceptors.response.use(
  (response) => response.data,
  (error: AxiosError<ApiErrorPayload>) => {
    const status = error.response?.status
    const payload = error.response?.data
    const message = payload?.message || payload?.detail || payload?.title || error.message || '请求失败'

    if (status === 401) {
      useAuthStore().logout()
      ElMessage.error(window.location.pathname === '/login' ? message : '登录已过期，请重新登录')
      if (window.location.pathname !== '/login') {
        const redirect = encodeURIComponent(`${window.location.pathname}${window.location.search}`)
        window.location.assign(`/login?redirect=${redirect}`)
      }
    } else if (status === 403) {
      ElMessage.error('没有权限执行该操作')
    } else {
      ElMessage.error(message)
    }

    return Promise.reject(error)
  },
)

export const http = {
  get<T = unknown, D = unknown>(url: string, config?: AxiosRequestConfig<D>) {
    return client.get<T>(url, config) as unknown as Promise<T>
  },

  delete<T = unknown, D = unknown>(url: string, config?: AxiosRequestConfig<D>) {
    return client.delete<T>(url, config) as unknown as Promise<T>
  },

  post<T = unknown, D = unknown>(url: string, data?: D, config?: AxiosRequestConfig<D>) {
    return client.post<T>(url, data, config) as unknown as Promise<T>
  },

  put<T = unknown, D = unknown>(url: string, data?: D, config?: AxiosRequestConfig<D>) {
    return client.put<T>(url, data, config) as unknown as Promise<T>
  },
}
