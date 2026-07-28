<script setup lang="ts">
import {
  ArrowLeft,
  Bike,
  CheckCircle2,
  PackageCheck,
  Plus,
  ReceiptText,
  UserRound,
  Wrench
} from '@lucide/vue'
import type {
  Customer,
  Employee,
  Invoice,
  PagedResult,
  Part,
  RepairOrder,
  RepairOrderStatus,
  Vehicle
} from '~/types/api'
import { formatCurrency, formatDate, formatNumber, statusLabel, statusTone } from '~/utils/format'

const route = useRoute()
const api = useApi()
const toast = useToast()
const order = ref<RepairOrder>()
const customer = ref<Customer>()
const vehicle = ref<Vehicle>()
const employees = ref<Employee[]>([])
const parts = ref<Part[]>([])
const loading = ref(true)
const saving = ref(false)
const itemModal = ref(false)
const statusModal = ref(false)
const invoiceModal = ref(false)
const statusForm = reactive({ status: 'Inspecting' as RepairOrderStatus, note: '' })
const invoiceForm = reactive({ discountAmount: 0, taxRate: 0, notes: '' })
const itemForm = reactive({
  itemType: 'Service', partId: '', description: '', quantity: 1,
  unitPrice: 0, discountAmount: 0, assignedEmployeeId: '', technicianNotes: ''
})

const orderId = computed(() => String(route.params.id))
const statusOptions: RepairOrderStatus[] = ['Received', 'Inspecting', 'AwaitingApproval', 'Repairing', 'AwaitingParts', 'Completed', 'Delivered', 'Cancelled']
const canIssueParts = computed(() => order.value?.items.some(x => x.itemType === 'Part' && !x.inventoryIssued))
const partOptions = computed(() => parts.value.map(part => ({
  code: part.id,
  name: `${part.name} · còn ${formatNumber(part.quantityOnHand)}`
})))
const employeeOptions = computed(() => employees.value.map(employee => ({
  code: employee.id,
  name: employee.fullName
})))

const load = async () => {
  loading.value = true
  try {
    const current = await api.request<RepairOrder>(`/repair-orders/${orderId.value}`)
    order.value = current
    const [c, v, e, p] = await Promise.all([
      api.request<Customer>(`/customers/${current.customerId}`),
      api.request<Vehicle>(`/vehicles/${current.vehicleId}`),
      api.request<PagedResult<Employee>>('/employees?pageSize=200'),
      api.request<PagedResult<Part>>('/parts?pageSize=200')
    ])
    customer.value = c
    vehicle.value = v
    employees.value = e.items
    parts.value = p.items
  } finally { loading.value = false }
}

const openAddItem = () => {
  Object.assign(itemForm, {
    itemType: 'Service', partId: '', description: '', quantity: 1,
    unitPrice: 0, discountAmount: 0, assignedEmployeeId: '', technicianNotes: ''
  })
  itemModal.value = true
}

const selectPart = (partId = itemForm.partId) => {
  const part = parts.value.find(x => x.id === partId)
  if (!part) return
  itemForm.description = part.name
  itemForm.unitPrice = part.salePrice
}

const addItem = async () => {
  saving.value = true
  try {
    order.value = await api.request(`/repair-orders/${orderId.value}/items`, {
      method: 'POST',
      body: {
        ...itemForm,
        serviceId: null,
        partId: itemForm.itemType === 'Part' ? itemForm.partId : null,
        assignedEmployeeId: itemForm.assignedEmployeeId || null
      }
    })
    toast.success('Đã thêm hạng mục', itemForm.description)
    itemModal.value = false
  } finally { saving.value = false }
}

const openStatus = () => {
  statusForm.status = order.value?.status === 'Received' ? 'Inspecting' : order.value?.status || 'Inspecting'
  statusForm.note = ''
  statusModal.value = true
}

const changeStatus = async () => {
  saving.value = true
  try {
    order.value = await api.request(`/repair-orders/${orderId.value}/status`, { method: 'PATCH', body: statusForm })
    toast.success('Đã cập nhật trạng thái', statusLabel(statusForm.status))
    statusModal.value = false
  } finally { saving.value = false }
}

const updateWork = async (itemId: string, status: string) => {
  order.value = await api.request(`/repair-orders/${orderId.value}/items/${itemId}/work`, {
    method: 'PATCH', body: { status, technicianNotes: null }
  })
  toast.success('Đã cập nhật công việc', statusLabel(status))
}

const issueParts = async () => {
  saving.value = true
  try {
    order.value = await api.request(`/repair-orders/${orderId.value}/issue-parts`, { method: 'POST' })
    toast.success('Đã xuất kho', 'Số lượng phụ tùng đã được trừ khỏi tồn kho.')
  } finally { saving.value = false }
}

const createInvoice = async () => {
  saving.value = true
  try {
    const invoice = await api.request<Invoice>('/invoices/from-repair-order', {
      method: 'POST', body: { ...invoiceForm, repairOrderId: orderId.value }
    })
    toast.success('Đã tạo hóa đơn', invoice.code)
    await navigateTo(`/invoices/${invoice.id}`)
  } finally { saving.value = false }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <NuxtLink to="/repair-orders" class="back-link"><ArrowLeft :size="16" /> Danh sách phiếu sửa</NuxtLink>
    <div v-if="order" class="page-header">
      <div>
        <div class="inline"><h1 class="page-title mono">{{ order.code }}</h1><AppBadge :tone="statusTone(order.status)">{{ statusLabel(order.status) }}</AppBadge><AppBadge :tone="order.priority === 'Urgent' ? 'danger' : order.priority === 'High' ? 'warning' : 'neutral'">{{ statusLabel(order.priority) }}</AppBadge></div>
        <p class="page-subtitle">Tiếp nhận {{ formatDate(order.receivedAt, true) }} · Hẹn giao {{ formatDate(order.expectedDeliveryAt, true) }}</p>
      </div>
      <div class="page-actions">
        <button v-if="canIssueParts" class="btn btn-secondary" :disabled="saving" @click="issueParts"><PackageCheck :size="17" /> Xuất phụ tùng</button>
        <button class="btn btn-secondary" @click="openStatus"><CheckCircle2 :size="17" /> Đổi trạng thái</button>
        <button class="btn btn-accent" :disabled="!order.items.length" @click="invoiceModal = true"><ReceiptText :size="17" /> Tạo hóa đơn</button>
      </div>
    </div>
    <div v-else-if="loading" class="loading-skeleton" style="height: 110px" />

    <section v-if="order" class="summary-strip">
      <div><UserRound :size="19" /><span>Khách hàng<strong>{{ customer?.fullName }}</strong><small>{{ customer?.phone }}</small></span></div>
      <div><Bike :size="19" /><span>Phương tiện<strong>{{ vehicle?.licensePlate }}</strong><small>ODO {{ formatNumber((order as any).odometerIn || vehicle?.odometer || 0) }} km</small></span></div>
      <div><Wrench :size="19" /><span>Yêu cầu<strong>{{ order.customerRequest }}</strong><small>{{ order.diagnosis || 'Chưa có chẩn đoán' }}</small></span></div>
    </section>

    <section class="card">
      <header class="card-header"><div><h2 class="card-title">Hạng mục sửa chữa</h2><span class="section-note">{{ order?.items.length || 0 }} hạng mục dịch vụ/phụ tùng</span></div><button class="btn btn-primary btn-sm" @click="openAddItem"><Plus :size="15" /> Thêm hạng mục</button></header>
      <div class="table-wrap">
        <table v-if="order?.items.length" class="data-table">
          <thead><tr><th>Nội dung</th><th>Phân công</th><th>Tiến độ</th><th class="text-right">SL</th><th class="text-right">Đơn giá</th><th class="text-right">Thành tiền</th></tr></thead>
          <tbody><tr v-for="item in order.items" :key="item.id"><td><div class="cell-main">{{ item.description }}</div><div class="cell-sub">{{ item.itemType === 'Part' ? 'Phụ tùng' : 'Dịch vụ' }}<span v-if="item.inventoryIssued"> · Đã xuất kho</span></div></td><td>{{ employees.find(x => x.id === item.assignedEmployeeId)?.fullName || 'Chưa phân công' }}</td><td><select :value="item.workStatus" class="select work-select" @change="updateWork(item.id, ($event.target as HTMLSelectElement).value)"><option value="Pending">Chờ làm</option><option value="InProgress">Đang làm</option><option value="Completed">Hoàn thành</option><option value="Cancelled">Đã hủy</option></select></td><td class="text-right">{{ formatNumber(item.quantity) }}</td><td class="text-right">{{ formatCurrency(item.unitPrice) }}</td><td class="text-right cell-main">{{ formatCurrency(item.lineTotal) }}</td></tr></tbody>
          <tfoot><tr><td colspan="5" class="text-right">Tổng dự kiến</td><td class="text-right total-cell">{{ formatCurrency(order.finalTotal) }}</td></tr></tfoot>
        </table>
        <AppEmpty v-else title="Chưa có hạng mục" message="Thêm công việc dịch vụ hoặc phụ tùng cần thay thế." />
      </div>
    </section>

    <section v-if="order" class="detail-columns">
      <article class="card"><header class="card-header"><h2 class="card-title">Thông tin kỹ thuật</h2></header><div class="card-body stack"><div class="info-row"><span>Tình trạng ban đầu</span><strong>{{ order.vehicleCondition }}</strong></div><div class="info-row"><span>Chẩn đoán</span><strong>{{ order.diagnosis || 'Chưa cập nhật' }}</strong></div><div class="info-row"><span>Ngày bàn giao</span><strong>{{ formatDate(order.deliveredAt, true) }}</strong></div></div></article>
      <article class="card"><header class="card-header"><h2 class="card-title">Lịch sử trạng thái</h2></header><div class="timeline"><div v-for="entry in [...order.statusHistory].reverse()" :key="entry.changedAt" class="timeline-row"><i /><div><strong>{{ statusLabel(entry.toStatus) }}</strong><span>{{ formatDate(entry.changedAt, true) }}</span><p v-if="entry.note">{{ entry.note }}</p></div></div></div></article>
    </section>

    <AppModal :open="itemModal" title="Thêm hạng mục sửa chữa" width="700px" @close="itemModal = false">
      <form id="item-form" class="form-grid" @submit.prevent="addItem">
        <div class="field"><label>Loại hạng mục</label><select v-model="itemForm.itemType" class="select"><option value="Service">Dịch vụ</option><option value="Part">Phụ tùng</option></select></div>
        <div v-if="itemForm.itemType === 'Part'" class="field"><label>Chọn phụ tùng *</label><AppSearchSelect v-model="itemForm.partId" :options="partOptions" placeholder="Chọn trong kho" search-placeholder="Tìm phụ tùng..." required :clearable="false" @update:model-value="selectPart" /></div>
        <div class="field span-2"><label>Mô tả *</label><input v-model.trim="itemForm.description" class="input" required /></div>
        <div class="field"><label>Số lượng</label><AppNumberInput v-model="itemForm.quantity" class="input" min="0.01" step="0.01" required /></div>
        <div class="field"><label>Đơn giá</label><AppNumberInput v-model="itemForm.unitPrice" class="input" min="0" required /></div>
        <div class="field"><label>Giảm giá</label><AppNumberInput v-model="itemForm.discountAmount" class="input" min="0" /></div>
        <div class="field"><label>Nhân viên thực hiện</label><AppSearchSelect v-model="itemForm.assignedEmployeeId" :options="employeeOptions" placeholder="Chưa phân công" search-placeholder="Tìm nhân viên..." /></div>
        <div class="field span-2"><label>Ghi chú kỹ thuật</label><textarea v-model="itemForm.technicianNotes" class="textarea" /></div>
      </form>
      <template #footer><button class="btn btn-secondary" @click="itemModal = false">Hủy</button><button class="btn btn-primary" form="item-form" :disabled="saving">{{ saving ? 'Đang thêm...' : 'Thêm hạng mục' }}</button></template>
    </AppModal>

    <AppModal :open="statusModal" title="Cập nhật trạng thái phiếu" @close="statusModal = false">
      <form id="status-form" class="stack" @submit.prevent="changeStatus"><div class="field"><label>Trạng thái mới</label><select v-model="statusForm.status" class="select"><option v-for="item in statusOptions" :key="item" :value="item">{{ statusLabel(item) }}</option></select></div><div class="field"><label>Ghi chú chuyển trạng thái</label><textarea v-model="statusForm.note" class="textarea" /></div></form>
      <template #footer><button class="btn btn-secondary" @click="statusModal = false">Hủy</button><button class="btn btn-primary" form="status-form" :disabled="saving">Cập nhật</button></template>
    </AppModal>

    <AppModal :open="invoiceModal" title="Tạo hóa đơn từ phiếu sửa" description="Hóa đơn sẽ sao chép toàn bộ hạng mục hiện tại." @close="invoiceModal = false">
      <form id="invoice-form" class="form-grid" @submit.prevent="createInvoice"><div class="field"><label>Giảm giá</label><AppNumberInput v-model="invoiceForm.discountAmount" class="input" min="0" /></div><div class="field"><label>Thuế suất (%)</label><AppNumberInput v-model="invoiceForm.taxRate" class="input" min="0" max="100" /></div><div class="field span-2"><label>Ghi chú hóa đơn</label><textarea v-model="invoiceForm.notes" class="textarea" /></div></form>
      <template #footer><button class="btn btn-secondary" @click="invoiceModal = false">Hủy</button><button class="btn btn-accent" form="invoice-form" :disabled="saving">Tạo hóa đơn</button></template>
    </AppModal>
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.summary-strip { display: grid; grid-template-columns: .8fr .8fr 1.8fr; gap: 1px; overflow: hidden; border: 1px solid var(--line); border-radius: var(--radius-lg); background: var(--line); box-shadow: var(--shadow); }
.summary-strip > div { display: flex; align-items: flex-start; gap: 12px; padding: 18px; background: white; }
.summary-strip svg { flex: 0 0 auto; color: var(--blue); }
.summary-strip span, .summary-strip strong, .summary-strip small { display: block; }
.summary-strip span { color: var(--muted); font-size: 11px; }
.summary-strip strong { margin-top: 2px; color: var(--navy-950); font-size: 13px; }
.summary-strip small { margin-top: 2px; color: var(--muted); }
.section-note { display: block; margin-top: 2px; color: var(--muted); font-size: 11px; }
.work-select { width: 135px; min-height: 34px; font-size: 12px; }
.data-table tfoot td { padding: 15px 16px; border-top: 2px solid var(--line); font-weight: 800; }
.total-cell { color: var(--navy-950); font-size: 17px; }
.detail-columns { display: grid; grid-template-columns: 1fr 1fr; gap: 18px; }
.info-row span, .info-row strong { display: block; }
.info-row span { color: var(--muted); font-size: 11px; }
.info-row strong { margin-top: 3px; color: var(--navy-950); }
.timeline { padding: 16px 20px; }
.timeline-row { position: relative; display: grid; grid-template-columns: 15px 1fr; gap: 10px; padding-bottom: 16px; }
.timeline-row:not(:last-child)::before { position: absolute; top: 9px; bottom: -4px; left: 5px; width: 1px; background: var(--line); content: ''; }
.timeline-row i { z-index: 1; width: 11px; height: 11px; margin-top: 4px; border: 3px solid white; border-radius: 50%; background: var(--teal); box-shadow: 0 0 0 1px var(--teal); }
.timeline-row strong, .timeline-row span { display: block; }
.timeline-row strong { color: var(--navy-950); font-size: 13px; }
.timeline-row span { color: var(--muted); font-size: 11px; }
.timeline-row p { margin: 4px 0 0; color: var(--muted); font-size: 12px; }
@media (max-width: 900px) { .summary-strip, .detail-columns { grid-template-columns: 1fr; } }
</style>
