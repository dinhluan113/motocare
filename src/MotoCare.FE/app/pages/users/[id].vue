<script setup lang="ts">
import { ArrowLeft, CalendarDays, KeyRound, ShieldCheck, UserCog } from '@lucide/vue'
import type { Employee, PagedResult, UserAccount } from '~/types/api'
import { formatDate, statusLabel } from '~/utils/format'

type EmployeeDetail = Employee & { hireDate?: string }
type BadgeTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral'

const route = useRoute()
const api = useApi()
const user = ref<UserAccount>()
const employees = ref<EmployeeDetail[]>([])
const loading = ref(true)

const userId = computed(() => String(route.params.id || ''))
const employee = computed(() => employees.value.find(item => item.id === user.value?.employeeId))

const roleLabel = (role: string) => ({
  Admin: 'Admin',
  Administrator: 'Quản trị viên',
  Manager: 'Quản lý',
  Employee: 'Nhân viên'
}[role] || role)
const roleTone = (role: string): BadgeTone =>
  role === 'Admin' || role === 'Administrator' ? 'danger' : role === 'Manager' ? 'warning' : 'neutral'
const employeeStatusTone = (status?: Employee['status']): BadgeTone =>
  status === 'Active' ? 'success' : status === 'OnLeave' ? 'warning' : 'neutral'

const loadEmployees = async () => {
  const firstPage = await api.request<PagedResult<EmployeeDetail>>('/employees', {
    query: { page: 1, pageSize: 200, includeDeleted: true }
  })
  const remainingPages = await Promise.all(Array.from(
    { length: Math.max(0, firstPage.totalPages - 1) },
    (_, index) => api.request<PagedResult<EmployeeDetail>>('/employees', {
      query: { page: index + 2, pageSize: 200, includeDeleted: true }
    })
  ))
  return [firstPage, ...remainingPages].flatMap(page => page.items)
}

const load = async () => {
  loading.value = true
  user.value = undefined
  employees.value = []

  try {
    user.value = await api.request<UserAccount>(`/users/${userId.value}`, {
      query: { includeDeleted: true }
    })
    try {
      employees.value = await loadEmployees()
    } catch {
      employees.value = []
    }
  } catch {
    user.value = undefined
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch(userId, () => load())
</script>

<template>
  <div class="page">
    <NuxtLink class="back-link" to="/users"><ArrowLeft :size="16" /> Tài khoản & quyền</NuxtLink>

    <template v-if="user">
      <div class="page-header">
        <div>
          <div class="breadcrumb">Quản trị <span>›</span> Tài khoản & quyền</div>
          <div class="title-line">
            <h1 class="page-title">{{ user.fullName }}</h1>
            <AppBadge :tone="user.isDeleted || !user.isActive ? 'neutral' : 'success'">{{ user.isDeleted ? 'Đã xóa' : user.isActive ? 'Hoạt động' : 'Tạm khóa' }}</AppBadge>
          </div>
          <p class="page-subtitle mono">@{{ user.username }}</p>
        </div>
      </div>

      <section class="metric-grid">
        <article class="metric"><KeyRound :size="20" /><div><span>Tên đăng nhập</span><strong class="mono">{{ user.username }}</strong></div></article>
        <article class="metric"><ShieldCheck :size="20" /><div><span>Vai trò</span><strong>{{ user.roles.map(roleLabel).join(', ') || 'Chưa phân quyền' }}</strong></div></article>
        <article class="metric"><CalendarDays :size="20" /><div><span>Đăng nhập gần nhất</span><strong>{{ formatDate(user.lastLoginAt, true) }}</strong></div></article>
      </section>

      <section class="card">
        <header class="card-header"><h2 class="card-title">Thông tin tài khoản</h2><ShieldCheck :size="19" /></header>
        <div class="card-body detail-grid">
          <div><span>Tên đăng nhập</span><strong class="mono">{{ user.username }}</strong></div>
          <div><span>Họ và tên</span><strong>{{ user.fullName }}</strong></div>
          <div>
            <span>Vai trò</span>
            <div class="badge-list"><AppBadge v-for="role in user.roles" :key="role" :tone="roleTone(role)">{{ roleLabel(role) }}</AppBadge><strong v-if="!user.roles.length">Chưa phân quyền</strong></div>
          </div>
          <div><span>Trạng thái</span><strong>{{ user.isDeleted ? 'Đã xóa' : user.isActive ? 'Đang hoạt động' : 'Đã tạm khóa' }}</strong></div>
          <div class="span-2"><span>Đăng nhập gần nhất</span><strong>{{ formatDate(user.lastLoginAt, true) }}</strong></div>
        </div>
      </section>

      <section class="card">
        <header class="card-header"><div><h2 class="card-title">Nhân viên liên kết</h2><span class="section-note">Hồ sơ nhân viên được gắn với tài khoản đăng nhập này</span></div><UserCog :size="20" /></header>
        <div v-if="user.employeeId" class="card-body employee-card">
          <div class="employee-main">
            <AppEntityLink :to="`/employees/${user.employeeId}`" block icon>
              <span class="employee-name">{{ employee?.fullName || 'Nhân viên không còn trong danh sách' }}</span>
              <span class="mono muted">{{ employee?.employeeCode || user.employeeId }}</span>
            </AppEntityLink>
            <AppBadge v-if="employee" :tone="employeeStatusTone(employee.status)">{{ statusLabel(employee.status) }}</AppBadge>
          </div>
          <div class="employee-details">
            <div><span>Số điện thoại</span><strong>{{ employee?.phone || '—' }}</strong></div>
            <div><span>Vị trí công việc</span><strong>{{ employee?.position || '—' }}</strong></div>
            <div><span>Ngày vào làm</span><strong>{{ formatDate(employee?.hireDate) }}</strong></div>
            <div><span>Chuyên môn</span><strong>{{ employee?.specialties?.join(', ') || '—' }}</strong></div>
          </div>
        </div>
        <AppEmpty v-else title="Chưa liên kết nhân viên" message="Tài khoản này chưa được gắn với hồ sơ nhân viên nào." />
      </section>
    </template>

    <div v-else-if="loading" class="loading-skeleton" style="height: 420px" />
    <AppEmpty v-else title="Không tìm thấy tài khoản" message="Tài khoản không tồn tại hoặc bạn không có quyền xem dữ liệu này." />
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.breadcrumb { margin-bottom: 7px; color: var(--muted); font-size: 11px; font-weight: 700; }.breadcrumb span { padding: 0 5px; color: var(--amber); }
.title-line { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.metric-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 14px; }
.metric { display: flex; min-width: 0; align-items: center; gap: 12px; padding: 17px 19px; border: 1px solid var(--line); border-radius: var(--radius-lg); color: var(--navy-800); background: white; box-shadow: var(--shadow-sm); }
.metric span,.metric strong { display: block; }.metric span { color: var(--muted); font-size: 10px; }.metric strong { margin-top: 4px; overflow: hidden; color: var(--navy-950); font-size: 16px; text-overflow: ellipsis; }
.card-header > svg { color: var(--muted); }
.detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }.detail-grid span,.detail-grid strong { display: block; }.detail-grid span { color: var(--muted); font-size: 11px; }.detail-grid strong { margin-top: 4px; color: var(--navy-950); }.span-2 { grid-column: span 2; }
.badge-list { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; margin-top: 6px; }
.employee-card { display: grid; gap: 18px; }.employee-main { display: flex; align-items: center; justify-content: space-between; gap: 14px; padding-bottom: 16px; border-bottom: 1px solid var(--line); }.employee-name { color: var(--navy-950); font-size: 16px; font-weight: 800; }.employee-details { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; }.employee-details span,.employee-details strong { display: block; }.employee-details span { color: var(--muted); font-size: 11px; }.employee-details strong { margin-top: 4px; color: var(--navy-950); }
@media (max-width: 720px) { .metric-grid { grid-template-columns: 1fr; }.detail-grid,.employee-details { grid-template-columns: 1fr; gap: 14px; }.span-2 { grid-column: auto; }.employee-main { align-items: flex-start; flex-direction: column; } }
</style>
