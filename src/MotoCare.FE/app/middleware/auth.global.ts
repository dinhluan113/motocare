export default defineNuxtRouteMiddleware(async (to) => {
  if (!import.meta.client) return
  const auth = useAuth()
  await auth.initialize()

  if (to.path === '/login') {
    if (auth.isAuthenticated.value) return navigateTo('/')
    return
  }

  if (!auth.isAuthenticated.value) {
    return navigateTo(`/login?redirect=${encodeURIComponent(to.fullPath)}`)
  }
})
