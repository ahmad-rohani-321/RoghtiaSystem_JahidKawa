export interface AuthUser {
  id: number
  userName: string
  roles: string[]
  permissions: string[]
}

export interface AuthSession {
  user: AuthUser
  expiresAt: string
}

export interface AuthTokenResponse extends AuthSession {
  token: string
}

export interface AuthCredentials {
  userName: string
  password: string
  rememberMe?: boolean
}
