<script setup lang="ts">
import {
  Bell,
  ChartNoAxesCombined,
  ChevronDown,
  CircleUserRound,
  ClipboardList,
  TicketPercent,
  Database,
  FileText,
  LayoutDashboard,
  LogOut,
  Menu,
  Package,
  ReceiptText,
  Search,
  Settings,
  ScrollText,
  ShieldCheck,
  Truck,
  Star,
  Users,
  WalletCards,
  Warehouse,
  Wifi,
  WifiOff,
  Wrench,
  X
} from '@lucide/vue'
import { formatDate } from '~/utils/format'
import { entityDetailRoute } from '~/utils/entityRoute'

const route = useRoute()
const auth = useAuth()
const api = useApi()
const notifications = useRealtimeNotifications()
const mobileOpen = ref(false)
const notificationOpen = ref(false)
const demoDataEnabled = ref(false)

const allNavigation = [
  { label: 'Tổng quan', to: '/', icon: LayoutDashboard },
  { label: 'Phiếu sửa chữa', to: '/repair-orders', icon: ClipboardList },
  { label: 'Khách hàng & xe', to: '/customers', icon: Users },
  { label: 'Nhân viên', to: '/employees', icon: Wrench },
  { label: 'Nhà cung cấp', to: '/suppliers', icon: Truck },
  { label: 'Kho phụ tùng', to: '/inventory', icon: Package },
  { label: 'Quản lý kho', to: '/warehouse-locations', icon: Warehouse },
  { label: 'Hóa đơn', to: '/invoices', icon: ReceiptText },
  { label: 'Thu chi', to: '/finance', icon: WalletCards },
  { label: 'Loyalty', to: '/loyalty', icon: Star },
  { label: 'Coupon', to: '/coupons', icon: TicketPercent, adminOnly: true },
  { label: 'Báo cáo', to: '/reports', icon: ChartNoAxesCombined },
  { label: 'Danh mục', to: '/catalogs', icon: Database },
  { label: 'Lịch sử thao tác', to: '/audit-logs', icon: ScrollText },
  { label: 'Tài khoản & quyền', to: '/users', icon: ShieldCheck, adminOnly: true },
  { label: 'Cài đặt', to: '/settings', icon: Settings, adminOnly: true, demoDataOnly: true }
]
const employeePaths = new Set(['/repair-orders', '/customers', '/inventory'])
const navigation = computed(() => {
  const isEmployee = auth.hasAnyRole('Employee')
  const isAdmin = auth.hasAnyRole('Admin', 'Administrator')
  return allNavigation.filter(item =>
    (!isEmployee || employeePaths.has(item.to))
    && (!(item as any).adminOnly || isAdmin)
    && (!(item as any).demoDataOnly || demoDataEnabled.value))
})

onMounted(async () => {
  if (!auth.hasAnyRole('Admin', 'Administrator')) return
  try {
    const status = await api.request<{ enabled: boolean }>('/settings/demo-data')
    demoDataEnabled.value = status.enabled
  } catch {
    demoDataEnabled.value = false
  }
})

const active = (to: string) =>
  to === '/' ? route.path === '/' : route.path.startsWith(to)

const unread = (item: any) =>
  !item.isRead && !item.readByUserIds?.includes(auth.user.value?.id)
const roleLabel = computed(() => {
  const role = auth.user.value?.roles?.[0]
  return role === 'Admin' || role === 'Administrator' ? 'Admin' : role === 'Manager' ? 'Quản lý' : role === 'Employee' ? 'Nhân viên' : role
})

const openNotification = async (item: any) => {
  if (unread(item)) await notifications.markRead(item.id)
  const target = entityDetailRoute(item.referenceType, item.referenceId)
  if (target) await navigateTo(target)
  notificationOpen.value = false
}

watch(() => route.fullPath, () => {
  mobileOpen.value = false
})
</script>

<template>
  <div class="admin-shell">
    <Transition name="fade">
      <button
        v-if="mobileOpen"
        class="mobile-backdrop"
        aria-label="Đóng menu"
        @click="mobileOpen = false"
      />
    </Transition>

    <aside class="sidebar" :class="{ 'sidebar-open': mobileOpen }">
      <div class="brand">
        <div class="brand-mark">
          <Wrench :size="21" />
        </div>
        <div>
          <strong>MotoCare</strong>
          <span>Workshop OS</span>
        </div>
        <button class="sidebar-close" aria-label="Đóng menu" @click="mobileOpen = false">
          <X :size="20" />
        </button>
      </div>

      <div class="shop-card">
        <span>Không gian làm việc</span>
        <strong>Tiệm xe trung tâm</strong>
        <small><i /> Đang vận hành</small>
      </div>

      <nav aria-label="Điều hướng chính">
        <NuxtLink
          v-for="item in navigation"
          :key="item.to"
          :to="item.to"
          class="nav-link"
          :class="{ active: active(item.to) }"
        >
          <component :is="item.icon" :size="18" />
          <span>{{ item.label }}</span>
        </NuxtLink>
      </nav>

      <div class="sidebar-footer">
        <div class="support-note">
          <FileText :size="17" />
          <div>
            <strong>Cần hỗ trợ?</strong>
            <span>Xem tài liệu vận hành</span>
          </div>
        </div>
        <button class="logout" @click="auth.logout">
          <LogOut :size="17" />
          Đăng xuất
        </button>
      </div>
    </aside>

    <div class="main-column">
      <header class="topbar">
        <button class="menu-button" aria-label="Mở menu" @click="mobileOpen = true">
          <Menu :size="21" />
        </button>
        <div class="global-search">
          <Search :size="17" />
          <input aria-label="Tìm nhanh" placeholder="Tìm khách hàng, biển số, phiếu sửa..." />
          <kbd>⌘ K</kbd>
        </div>
        <div class="top-actions">
          <div class="connection" :title="notifications.connected.value ? 'Realtime đã kết nối' : 'Realtime mất kết nối'">
            <Wifi v-if="notifications.connected.value" :size="16" />
            <WifiOff v-else :size="16" />
          </div>
          <div class="notification-wrap">
            <button
              class="icon-btn bell-button"
              aria-label="Thông báo"
              @click="notificationOpen = !notificationOpen"
            >
              <Bell :size="19" />
              <span v-if="notifications.unreadCount.value">{{ Math.min(99, notifications.unreadCount.value) }}</span>
            </button>
            <div v-if="notificationOpen" class="notification-panel">
              <div class="notification-head">
                <strong>Thông báo</strong>
                <span>{{ notifications.unreadCount.value }} chưa đọc</span>
              </div>
              <div class="notification-list">
                <button
                  v-for="item in notifications.items.value.slice(0, 8)"
                  :key="item.id"
                  :class="{ unread: unread(item) }"
                  @click="openNotification(item)"
                >
                  <i />
                  <div>
                    <strong>{{ item.title }}</strong>
                    <p>{{ item.message }}</p>
                    <span>{{ formatDate(item.createdAt, true) }}</span>
                  </div>
                </button>
                <AppEmpty
                  v-if="!notifications.items.value.length"
                  title="Chưa có thông báo"
                  message="Cảnh báo và thay đổi công việc sẽ xuất hiện tại đây."
                />
              </div>
            </div>
          </div>
          <div class="profile">
            <div class="avatar">
              <CircleUserRound :size="21" />
            </div>
            <div>
              <strong>{{ auth.user.value?.fullName }}</strong>
              <span>{{ roleLabel }}</span>
            </div>
            <ChevronDown :size="15" />
          </div>
        </div>
      </header>

      <main class="content">
        <slot />
      </main>
    </div>
  </div>
</template>

<style scoped>
.admin-shell {
  min-height: 100vh;
}

.sidebar {
  position: fixed;
  z-index: 50;
  inset: 0 auto 0 0;
  display: flex;
  width: var(--sidebar-width);
  max-width: calc(100vw - 44px);
  flex-direction: column;
  padding: 18px 14px;
  color: #d8e6f2;
  background:
    radial-gradient(circle at 25% 5%, rgb(245 158 11 / 12%), transparent 13rem),
    var(--navy-950);
}

.brand {
  display: flex;
  align-items: center;
  gap: 11px;
  padding: 6px 8px 18px;
}

.brand-mark {
  display: grid;
  width: 42px;
  height: 42px;
  place-items: center;
  border-radius: 12px;
  color: var(--navy-950);
  background: var(--amber);
}

.brand strong,
.brand span {
  display: block;
}

.brand strong {
  color: white;
  font-size: 18px;
  letter-spacing: -0.03em;
}

.brand span {
  color: #87a2b8;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.12em;
  text-transform: uppercase;
}

.sidebar-close {
  display: none;
  margin-left: auto;
  border: 0;
  color: white;
  background: transparent;
}

.shop-card {
  display: grid;
  gap: 3px;
  margin: 3px 4px 18px;
  padding: 12px 13px;
  border: 1px solid rgb(255 255 255 / 9%);
  border-radius: 12px;
  background: rgb(255 255 255 / 5%);
}

.shop-card span {
  color: #7895ad;
  font-size: 9px;
  font-weight: 800;
  letter-spacing: 0.09em;
  text-transform: uppercase;
}

.shop-card strong {
  color: white;
  font-size: 12px;
}

.shop-card small {
  display: flex;
  align-items: center;
  gap: 6px;
  color: #8dcfc6;
  font-size: 10px;
}

.shop-card i {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #36c4b1;
  box-shadow: 0 0 0 3px rgb(54 196 177 / 12%);
}

nav {
  display: grid;
  min-height: 0;
  flex: 1 1 auto;
  gap: 4px;
  overflow-y: auto;
  overscroll-behavior: contain;
  scrollbar-width: thin;
}

.nav-link {
  position: relative;
  display: flex;
  min-height: 43px;
  align-items: center;
  gap: 12px;
  padding: 9px 12px;
  border-radius: 10px;
  color: #93abc0;
  font-size: 12px;
  font-weight: 650;
  transition: color 140ms ease, background 140ms ease;
}

.nav-link:hover {
  color: white;
  background: rgb(255 255 255 / 5%);
}

.nav-link.active {
  color: white;
  background: linear-gradient(90deg, rgb(245 158 11 / 16%), rgb(245 158 11 / 5%));
}

.nav-link.active::before {
  position: absolute;
  left: 0;
  width: 3px;
  height: 23px;
  border-radius: 3px;
  background: var(--amber);
  content: '';
}

.nav-link.active svg {
  color: var(--amber);
}

.sidebar-footer {
  display: grid;
  flex: 0 0 auto;
  gap: 10px;
  margin-top: auto;
}

.support-note {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 11px;
  border-radius: 10px;
  color: #8da9bf;
  background: rgb(255 255 255 / 4%);
}

.support-note strong,
.support-note span {
  display: block;
}

.support-note strong {
  color: #dce8f1;
  font-size: 11px;
}

.support-note span {
  font-size: 9px;
}

.logout {
  display: flex;
  min-height: 39px;
  align-items: center;
  gap: 10px;
  padding: 8px 12px;
  border: 0;
  border-radius: 9px;
  color: #93abc0;
  background: transparent;
}

.main-column {
  min-width: 0;
  min-height: 100vh;
  margin-left: var(--sidebar-width);
}

.topbar {
  position: sticky;
  z-index: 40;
  top: 0;
  display: flex;
  min-height: 68px;
  align-items: center;
  gap: 18px;
  padding: 10px 28px;
  border-bottom: 1px solid rgb(213 222 230 / 88%);
  background: rgb(255 255 255 / 88%);
  backdrop-filter: blur(16px);
}

.menu-button {
  display: none;
  width: 40px;
  height: 40px;
  place-items: center;
  border: 1px solid var(--line);
  border-radius: 10px;
  background: white;
}

.global-search {
  position: relative;
  display: flex;
  width: min(440px, 46vw);
  align-items: center;
}

.global-search > svg {
  position: absolute;
  left: 12px;
  color: #8a98a6;
}

.global-search input {
  width: 100%;
  height: 40px;
  padding: 0 52px 0 39px;
  border: 1px solid transparent;
  border-radius: 10px;
  background: #f1f4f6;
  outline: none;
}

.global-search input:focus {
  border-color: #bfd0dd;
  background: white;
}

.global-search kbd {
  position: absolute;
  right: 9px;
  padding: 2px 6px;
  border: 1px solid #d7dee5;
  border-radius: 5px;
  color: #7c8996;
  background: white;
  font-size: 10px;
}

.top-actions {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-left: auto;
}

.connection {
  display: grid;
  width: 30px;
  height: 30px;
  place-items: center;
  color: var(--teal);
}

.notification-wrap {
  position: relative;
}

.bell-button {
  position: relative;
}

.bell-button > span {
  position: absolute;
  top: -5px;
  right: -5px;
  display: grid;
  min-width: 19px;
  height: 19px;
  place-items: center;
  padding: 0 4px;
  border: 2px solid white;
  border-radius: 99px;
  color: white;
  background: var(--red);
  font-size: 9px;
  font-weight: 800;
}

.notification-panel {
  position: absolute;
  top: 49px;
  right: 0;
  width: min(390px, calc(100vw - 28px));
  overflow: hidden;
  border: 1px solid var(--line);
  border-radius: 15px;
  background: white;
  box-shadow: 0 24px 65px rgb(10 31 51 / 24%);
}

.notification-head {
  display: flex;
  justify-content: space-between;
  padding: 15px 16px;
  border-bottom: 1px solid var(--line);
}

.notification-head span {
  color: var(--muted);
  font-size: 11px;
}

.notification-list {
  max-height: 420px;
  overflow-y: auto;
}

.notification-list > button {
  display: grid;
  width: 100%;
  grid-template-columns: 8px 1fr;
  gap: 10px;
  padding: 13px 16px;
  border: 0;
  border-bottom: 1px solid #edf1f4;
  text-align: left;
  background: white;
}

.notification-list > button:hover,
.notification-list > button.unread {
  background: #f7fafc;
}

.notification-list i {
  width: 7px;
  height: 7px;
  margin-top: 6px;
  border-radius: 50%;
  background: #c5cdd5;
}

.notification-list .unread i {
  background: var(--amber);
}

.notification-list strong {
  color: var(--navy-900);
  font-size: 12px;
}

.notification-list p {
  margin: 3px 0;
  color: var(--muted);
  font-size: 11px;
}

.notification-list span {
  color: #94a0ac;
  font-size: 9px;
}

.profile {
  display: flex;
  align-items: center;
  gap: 9px;
  margin-left: 4px;
  padding-left: 13px;
  border-left: 1px solid var(--line);
}

.avatar {
  display: grid;
  width: 37px;
  height: 37px;
  place-items: center;
  border-radius: 10px;
  color: var(--navy-800);
  background: var(--amber-soft);
}

.profile strong,
.profile span {
  display: block;
  max-width: 150px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.profile strong {
  color: var(--navy-950);
  font-size: 11px;
}

.profile span {
  color: var(--muted);
  font-size: 9px;
}

.content {
  width: min(1520px, 100%);
  min-width: 0;
  margin: 0 auto;
  padding: 28px;
}

.mobile-backdrop {
  position: fixed;
  z-index: 45;
  inset: 0;
  border: 0;
  background: rgb(4 15 26 / 55%);
  backdrop-filter: blur(2px);
}

.fade-enter-active,
.fade-leave-active { transition: opacity 160ms ease; }
.fade-enter-from,
.fade-leave-to { opacity: 0; }

@media (max-width: 1020px) {
  .sidebar {
    transform: translateX(-102%);
    transition: transform 180ms ease;
  }

  .sidebar.sidebar-open {
    transform: translateX(0);
  }

  .sidebar-close,
  .menu-button {
    display: grid;
  }

  .main-column {
    margin-left: 0;
  }
}

@media (max-width: 720px) {
  .topbar {
    min-height: 62px;
    gap: 8px;
    padding: 10px 14px;
  }

  .global-search {
    display: none;
  }

  .connection,
  .profile > div:not(.avatar),
  .profile > svg {
    display: none;
  }

  .profile {
    margin-left: 0;
    padding-left: 9px;
  }

  .content {
    padding: 18px 14px calc(22px + env(safe-area-inset-bottom));
  }

  .notification-panel {
    position: fixed;
    top: 68px;
    right: 10px;
    left: 10px;
    width: auto;
    max-height: calc(100dvh - 80px);
  }

  .notification-list {
    max-height: calc(100dvh - 140px);
  }
}

@media (max-width: 380px) {
  .topbar {
    padding-right: 10px;
    padding-left: 10px;
  }

  .top-actions {
    gap: 6px;
  }

  .profile {
    padding-left: 6px;
  }
}
</style>
