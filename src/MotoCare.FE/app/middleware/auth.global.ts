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

  if (auth.hasAnyRole('Employee')) {
    const allowed = ['/customers', '/repair-orders', '/inventory']
    if (!allowed.some(path => to.path === path || to.path.startsWith(`${path}/`))) {
      return navigateTo('/repair-orders')
    }
  }
  if (to.path.startsWith('/users') && !auth.hasAnyRole('Admin', 'Administrator')) return navigateTo('/')
  if (to.path.startsWith('/coupons') && !auth.hasAnyRole('Admin', 'Administrator')) return navigateTo('/')
  if (to.path.startsWith('/settings') && !auth.hasAnyRole('Admin', 'Administrator')) return navigateTo('/')
  if (to.path.startsWith('/audit-logs') && !auth.hasAnyRole('Admin', 'Administrator', 'Manager')) return navigateTo('/')
})
