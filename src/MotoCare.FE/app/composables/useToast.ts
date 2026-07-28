export interface ToastMessage {
  id: number
  type: 'success' | 'error' | 'info'
  title: string
  message?: string
}

export const useToast = () => {
  const messages = useState<ToastMessage[]>('toast.messages', () => [])

  const show = (
    type: ToastMessage['type'],
    title: string,
    message?: string
  ) => {
    const id = Date.now() + Math.floor(Math.random() * 1000)
    messages.value.push({ id, type, title, message })
    window.setTimeout(() => remove(id), 4200)
  }

  const remove = (id: number) => {
    messages.value = messages.value.filter(item => item.id !== id)
  }

  return {
    messages,
    remove,
    success: (title: string, message?: string) => show('success', title, message),
    error: (title: string, message?: string) => show('error', title, message),
    info: (title: string, message?: string) => show('info', title, message)
  }
}
