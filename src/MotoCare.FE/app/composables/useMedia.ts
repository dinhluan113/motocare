export const useMedia = () => {
  const config = useRuntimeConfig()
  const api = useApi()

  const url = (path?: string) => {
    if (!path) return ''
    if (/^(data:|blob:|https?:\/\/)/i.test(path)) return path
    try {
      return `${new URL(String(config.public.apiBase)).origin}${path.startsWith('/') ? path : `/${path}`}`
    } catch {
      return path
    }
  }

  const uploadImage = async (file: File, category: string) => {
    const body = new FormData()
    body.append('file', file)
    const result = await api.request<{ path: string }>('/uploads/images', {
      method: 'POST',
      query: { category },
      body
    })
    return result.path
  }

  const deleteImage = async (path?: string) => {
    if (!path?.startsWith('/uploads/')) return
    await api.request('/uploads/images', { method: 'DELETE', body: { path } })
  }

  return { mediaUrl: url, uploadImage, deleteImage }
}
