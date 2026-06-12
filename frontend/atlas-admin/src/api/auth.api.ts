import { http } from './http'

export interface LoginRequest {
  domain: string
  userName: string
  password: string
  rememberMe: boolean
}

export interface RefreshTokenRequest {
  refreshToken: string
}

export interface StoreInfoDto {
  id: number
  code?: string
  name: string
  type?: string | number
  isPrimary?: boolean
  permission?: string
}

export interface UserDto {
  id: number
  tenantId: number
  userName: string
  realName: string
  nickName?: string | null
  phone?: string | null
  email?: string | null
  avatar?: string | null
  defaultStoreId?: number | null
  defaultStoreName?: string | null
}

export interface LoginResponse {
  success: boolean
  message?: string | null
  token: string
  refreshToken?: string | null
  user?: UserDto | null
  expiresIn: number
  expiresAt?: string | null
  currentStore?: StoreInfoDto | null
  accessibleStores?: StoreInfoDto[] | null
  requirePasswordChange: boolean
}

export interface AuthorizationContextSnapshot {
  tenantId: number
  userId: number
  storeId?: number | null
  permissions: string[]
  capabilities: string[]
  featureFlags: string[]
}

export interface OperationResult {
  success: boolean
  message?: string | null
}

export const authApi = {
  login(data: LoginRequest) {
    return http.post<LoginResponse>('/user/login', data)
  },

  refreshToken(data: RefreshTokenRequest) {
    return http.post<LoginResponse>('/user/refresh-token', data)
  },

  logout() {
    return http.post<OperationResult>('/user/logout')
  },

  context() {
    return http.get<AuthorizationContextSnapshot>('/auth/context')
  },
}
