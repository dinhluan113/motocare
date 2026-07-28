import type { ApiEnvelope } from '~/types/api'

export const useApi = () => {
  const config = useRuntimeConfig()
  const auth = useAuth()
  const toast = useToast()

  const request = async <T>(path: string, options: Record<string, any> = {}) => {
    try {
      const response = await $fetch<ApiEnvelope<T>>(path, {
        ...options,
        baseURL: config.public.apiBase,
        headers: {
          ...(options.headers || {}),
          ...(auth.token.value
            ? { Authorization: `Bearer ${auth.token.value}` }
            : {})
        }
      })
      return response.data
    } catch (error: any) {
      if (error?.response?.status === 401) {
        await auth.logout()
      }
      const message = error?.data?.message || error?.message || 'Không thể kết nối máy chủ.'
      toast.error('Thao tác không thành công', message)
      throw error
    }
  }

  const download = async (path: string, filename: string) => {
    try {
      const blob = await $fetch<Blob>(path, {
        baseURL: config.public.apiBase,
        responseType: 'blob',
        headers: auth.token.value
          ? { Authorization: `Bearer ${auth.token.value}` }
          : {}
      })
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = filename
      anchor.click()
      URL.revokeObjectURL(url)
    } catch (error: any) {
      toast.error(
        'Không thể tải tệp',
        error?.data?.message || 'Vui lòng thử lại.'
      )
      throw error
    }
  }

  return { request, download }
}
