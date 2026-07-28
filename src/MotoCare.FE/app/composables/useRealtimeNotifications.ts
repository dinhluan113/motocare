import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection
} from '@microsoft/signalr'
import type { AppNotification } from '~/types/api'

let connection: HubConnection | null = null

export const useRealtimeNotifications = () => {
  const auth = useAuth()
  const config = useRuntimeConfig()
  const items = useState<AppNotification[]>('notifications.items', () => [])
  const connected = useState<boolean>('notifications.connected', () => false)
  const api = useApi()

  const load = async () => {
    if (!auth.token.value) return
    items.value = await api.request<AppNotification[]>('/notifications?limit=50')
  }

  const connect = async () => {
    if (!import.meta.client || !auth.token.value) return
    if (connection?.state === HubConnectionState.Connected) return
    const apiRoot = String(config.public.apiBase).replace(/\/api\/v1\/?$/, '')
    connection = new HubConnectionBuilder()
      .withUrl(`${apiRoot}/hubs/notifications`, {
        accessTokenFactory: () => auth.token.value || ''
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    connection.on('notification', (notification: AppNotification) => {
      items.value.unshift(notification)
    })
    connection.onreconnected(() => {
      connected.value = true
      load()
    })
    connection.onclose(() => {
      connected.value = false
    })
    await connection.start()
    connected.value = true
  }

  const markRead = async (id: string) => {
    await api.request(`/notifications/${id}/read`, { method: 'PATCH' })
    const item = items.value.find(value => value.id === id)
    if (item && auth.user.value) item.readByUserIds.push(auth.user.value.id)
  }

  const unreadCount = computed(() => {
    const userId = auth.user.value?.id
    return items.value.filter(item =>
      !item.isRead && (!userId || !item.readByUserIds.includes(userId))
    ).length
  })

  return { items, connected, unreadCount, load, connect, markRead }
}
