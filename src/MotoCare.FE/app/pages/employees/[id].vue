<script setup lang="ts">
import { ArrowLeft, CalendarDays, CheckCircle2, ClipboardList, ListChecks, MapPin, Phone, UserCog } from '@lucide/vue'
import type { Employee, RepairOrderItem, RepairOrderStatus } from '~/types/api'
import { entityDetailRoute } from '~/utils/entityRoute'
import { formatCurrency, formatDate, formatNumber, statusLabel, statusTone } from '~/utils/format'

interface EmployeeWorkOrder {
  id: string
  code: string
  status: RepairOrderStatus
  receivedAt: string
  deliveredAt?: string
  items: RepairOrderItem[]
}

interface EmployeeWorkRow {
  orderId: string
  orderCode: string
  orderStatus: RepairOrderStatus
  receivedAt: string
  deliveredAt?: string
  item: RepairOrderItem
}

const route = useRoute()
const api = useApi()
const employee = ref<Employee>()
const workHistory = ref<EmployeeWorkOrder[]>([])
const loading = ref(true)

const employeeId = computed(() => String(route.params.id))
const workRows = computed<EmployeeWorkRow[]>(() => workHistory.value.flatMap(order =>
  order.items.map(item => ({
    orderId: order.id,
    orderCode: order.code,
    orderStatus: order.status,
    receivedAt: order.receivedAt,
    deliveredAt: order.deliveredAt,
    item
  }))))
const completedItemCount = computed(() => workRows.value.filter(row => row.item.workStatus === 'Completed').length)
const inProgressItemCount = computed(() => workRows.value.filter(row => row.item.workStatus === 'InProgress').length)
const completionRate = computed(() => workRows.value.length
  ? Math.round(completedItemCount.value * 100 / workRows.value.length)
  : 0)
const itemDetailRoute = (item: RepairOrderItem) => item.itemType === 'Part'
  ? entityDetailRoute('Part', item.partId)
  : entityDetailRoute('ServiceCategory', item.serviceId)

const load = async () => {
  loading.value = true
  try {
    employee.value = await api.request<Employee>(`/employees/${employeeId.value}`, {
      query: { includeDeleted: true }
    })
    try {
      workHistory.value = await api.request<EmployeeWorkOrder[]>(`/employees/${employeeId.value}/work-history`, {
        query: { limit: 500 }
      })
    } catch {
      workHistory.value = []
    }
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <NuxtLink class="back-link" to="/employees"><ArrowLeft :size="16" /> Danh sách nhân viên</NuxtLink>

    <template v-if="employee">
      <div class="page-header">
        <div>
          <div class="title-line">
            <h1 class="page-title">{{ employee.fullName }}</h1>
            <AppBadge :tone="employee.isDeleted ? 'neutral' : statusTone(employee.status)">
              {{ employee.isDeleted ? 'Đã xóa' : statusLabel(employee.status) }}
            </AppBadge>
          </div>
          <p class="page-subtitle"><span class="mono">{{ employee.employeeCode }}</span> · {{ employee.position }}</p>
        </div>
      </div>

      <section class="metric-grid">
        <article class="metric"><ClipboardList :size="20" /><div><span>Phiếu đã tham gia</span><strong>{{ formatNumber(workHistory.length) }}</strong></div></article>
        <article class="metric"><ListChecks :size="20" /><div><span>Hạng mục được giao</span><strong>{{ formatNumber(workRows.length) }}</strong><small>{{ formatNumber(inProgressItemCount) }} đang thực hiện</small></div></article>
        <article class="metric"><CheckCircle2 :size="20" /><div><span>Hạng mục hoàn thành</span><strong>{{ formatNumber(completedItemCount) }}</strong></div></article>
        <article class="metric"><UserCog :size="20" /><div><span>Tỷ lệ hoàn thành</span><strong>{{ formatNumber(completionRate) }}%</strong></div></article>
      </section>

      <section class="profile-grid">
        <article class="card">
          <header class="card-header"><h2 class="card-title">Hồ sơ nhân viên</h2></header>
          <div class="card-body detail-grid">
            <div><span>Mã nhân viên</span><strong class="mono">{{ employee.employeeCode }}</strong></div>
            <div><span>Chức vụ</span><strong>{{ employee.position }}</strong></div>
            <div><span><Phone :size="14" /> Điện thoại</span><strong>{{ employee.phone }}</strong></div>
            <div><span>Email</span><strong>{{ employee.email || '—' }}</strong></div>
            <div><span>Ngày sinh</span><strong>{{ formatDate(employee.dateOfBirth) }}</strong></div>
            <div><span><CalendarDays :size="14" /> Ngày vào làm</span><strong>{{ formatDate(employee.hireDate) }}</strong></div>
            <div><span>Trình độ kỹ năng</span><strong>{{ employee.skillLevel || '—' }}</strong></div>
            <div><span>Lương cơ bản</span><strong>{{ formatCurrency(employee.baseSalary) }}</strong></div>
            <div class="span-2"><span><MapPin :size="14" /> Địa chỉ</span><strong>{{ employee.address || '—' }}</strong></div>
            <div class="span-2"><span>Ghi chú</span><strong>{{ employee.notes || '—' }}</strong></div>
          </div>
        </article>

        <article class="card">
          <header class="card-header"><h2 class="card-title">Chuyên môn</h2><UserCog :size="20" /></header>
          <div class="card-body specialty-body">
            <div v-if="employee.specialties?.length" class="specialty-list">
              <span v-for="specialty in employee.specialties" :key="specialty">{{ specialty }}</span>
            </div>
            <AppEmpty v-else title="Chưa khai báo chuyên môn" message="Hồ sơ nhân viên chưa có lĩnh vực chuyên môn." />
            <dl>
              <div><dt>Trạng thái làm việc</dt><dd>{{ statusLabel(employee.status) }}</dd></div>
              <div><dt>Cập nhật hồ sơ</dt><dd>{{ formatDate(employee.updatedAt, true) }}</dd></div>
            </dl>
          </div>
        </article>
      </section>

      <section class="card">
        <header class="card-header"><div><h2 class="card-title">Hạng mục đã thực hiện</h2><span class="section-note">Công việc được phân công trên các phiếu sửa chữa</span></div><span class="muted">{{ formatNumber(workRows.length) }} hạng mục</span></header>
        <div v-if="workRows.length" class="table-wrap">
          <table class="data-table work-table">
            <thead><tr><th>Phiếu sửa</th><th>Ngày nhận</th><th>Hạng mục</th><th>Loại</th><th>Tiến độ</th><th class="text-right">Giá trị</th></tr></thead>
            <tbody>
              <tr v-for="row in workRows" :key="`${row.orderId}-${row.item.id}`">
                <td>
                  <AppEntityLink :to="`/repair-orders/${row.orderId}`" block icon>
                    <strong class="mono">{{ row.orderCode }}</strong>
                    <span class="cell-sub">{{ statusLabel(row.orderStatus) }}</span>
                  </AppEntityLink>
                </td>
                <td>{{ formatDate(row.receivedAt, true) }}</td>
                <td><AppEntityLink class="cell-main" :to="itemDetailRoute(row.item)">{{ row.item.description }}</AppEntityLink><div v-if="row.item.technicianNotes" class="cell-sub notes">{{ row.item.technicianNotes }}</div></td>
                <td>{{ row.item.itemType === 'Part' ? 'Phụ tùng' : 'Dịch vụ' }}</td>
                <td><AppBadge :tone="statusTone(row.item.workStatus)">{{ statusLabel(row.item.workStatus) }}</AppBadge></td>
                <td class="text-right cell-main">{{ formatCurrency(row.item.lineTotal) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <AppEmpty v-else :icon="ClipboardList" title="Chưa có lịch sử công việc" message="Nhân viên chưa được phân công hạng mục sửa chữa nào." />
      </section>
    </template>

    <div v-else-if="loading" class="loading-skeleton" style="height: 420px" />
    <AppEmpty v-else :icon="UserCog" title="Không tìm thấy nhân viên" message="Nhân viên không tồn tại hoặc bạn không có quyền xem hồ sơ này." />
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.title-line { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.metric-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; }
.metric { display: flex; min-width: 0; align-items: center; gap: 12px; padding: 17px 19px; border: 1px solid var(--line); border-radius: var(--radius-lg); color: var(--navy-800); background: white; box-shadow: var(--shadow-sm); }
.metric span,.metric strong,.metric small { display: block; }.metric span,.metric small { color: var(--muted); font-size: 10px; }.metric strong { margin-top: 4px; color: var(--navy-950); font-size: 19px; }.metric small { margin-top: 2px; }
.profile-grid { display: grid; grid-template-columns: minmax(0, 1.4fr) minmax(280px, .65fr); gap: 18px; align-items: start; }
.detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }.detail-grid span,.detail-grid strong { display: block; }.detail-grid span { display: flex; align-items: center; gap: 5px; color: var(--muted); font-size: 11px; }.detail-grid strong { margin-top: 4px; color: var(--navy-950); overflow-wrap: anywhere; }.span-2 { grid-column: span 2; }
.specialty-body { display: grid; gap: 18px; }.specialty-list { display: flex; flex-wrap: wrap; gap: 7px; }.specialty-list span { padding: 6px 9px; border-radius: 999px; color: var(--navy-800); background: var(--blue-soft); font-size: 11px; font-weight: 750; }.specialty-body dl { display: grid; gap: 10px; margin: 0; }.specialty-body dl div { padding-top: 10px; border-top: 1px solid var(--line); }.specialty-body dt { color: var(--muted); font-size: 10px; }.specialty-body dd { margin: 3px 0 0; color: var(--navy-950); font-weight: 700; }
.work-table { min-width: 850px; }.notes { max-width: 360px; overflow: hidden; text-overflow: ellipsis; }
@media (max-width: 1000px) { .metric-grid { grid-template-columns: repeat(2, 1fr); }.profile-grid { grid-template-columns: 1fr; } }
@media (max-width: 640px) { .metric-grid,.detail-grid { grid-template-columns: 1fr; }.span-2 { grid-column: auto; } }
</style>
