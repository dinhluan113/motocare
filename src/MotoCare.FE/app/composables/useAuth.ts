import type { ApiEnvelope, AuthUser, LoginResult } from '~/types/api'

export const useAuth = () => {
  const config = useRuntimeConfig()
  const token = useState<string | null>('auth.token', () => null)
  const user = useState<AuthUser | null>('auth.user', () => null)
  const initialized = useState<boolean>('auth.initialized', () => false)

  const setSession = (login: LoginResult) => {
    token.value = login.accessToken
    user.value = {
      id: login.userId,
      username: login.username,
      fullName: login.fullName,
      roles: login.roles
    }
    localStorage.setItem('motocare.token', login.accessToken)
  }

  const clearSession = () => {
    token.value = null
    user.value = null
    if (import.meta.client) localStorage.removeItem('motocare.token')
  }

  const loadMe = async () => {
    if (!token.value) return false
    try {
      const response = await $fetch<ApiEnvelope<AuthUser>>('/auth/me', {
        baseURL: config.public.apiBase,
        headers: { Authorization: `Bearer ${token.value}` }
      })
      user.value = response.data
      return true
    } catch {
      clearSession()
      return false
    }
  }

  const initialize = async () => {
    if (initialized.value || !import.meta.client) return
    token.value = localStorage.getItem('motocare.token')
    if (token.value) await loadMe()
    initialized.value = true
  }

  const login = async (username: string, password: string) => {
    const response = await $fetch<ApiEnvelope<LoginResult>>('/auth/login', {
      method: 'POST',
      baseURL: config.public.apiBase,
      body: { username, password }
    })
    setSession(response.data)
    return response.data
  }

  const logout = async () => {
    clearSession()
    await navigateTo('/login')
  }

  const hasAnyRole = (...roles: string[]) =>
    Boolean(user.value?.roles.some(role => roles.includes(role)))

  return {
    token,
    user,
    initialized,
    isAuthenticated: computed(() => Boolean(token.value && user.value)),
    initialize,
    login,
    logout,
    loadMe,
    hasAnyRole
  }
}
