export default defineNuxtPlugin(async () => {
  const auth = useAuth()
  await auth.initialize()
  if (auth.isAuthenticated.value) {
    const notifications = useRealtimeNotifications()
    await Promise.allSettled([
      notifications.load(),
      notifications.connect()
    ])
  }
})
