<script setup lang="ts">
import { ArrowLeft, BellRing, Bike, ClipboardList, Pencil, Plus, Star, Trash2 } from '@lucide/vue'
import type {
  Customer,
  LoyaltyAccount,
  PagedResult,
  PartReplacementReminder,
  RepairOrder,
  Vehicle,
  VehicleBrand,
  VehicleModel
} from '~/types/api'
import { formatCurrency, formatDate, formatNumber, statusLabel, statusTone } from '~/utils/format'

const route = useRoute()
const api = useApi()
const auth = useAuth()
const toast = useToast()
const isEmployee = computed(() => auth.hasAnyRole('Employee'))
const customer = ref<Customer>()
const vehicles = ref<Vehicle[]>([])
const history = ref<RepairOrder[]>([])
const loyalty = ref<{ account: LoyaltyAccount | null, transactions: any[] }>()
const models = ref<VehicleModel[]>([])
const brands = ref<VehicleBrand[]>([])
const replacementReminders = ref<PartReplacementReminder[]>([])
const loading = ref(true)
const customerModalOpen = ref(false)
const modalOpen = ref(false)
const editingVehicle = ref<Vehicle>()
const savingCustomer = ref(false)
const saving = ref(false)
const customerForm = reactive({
  fullName: '',
  phone: '',
  email: '',
  addressDetails: emptyAddressDetails(),
  dateOfBirth: '',
  gender: '',
  taxCode: '',
  notes: '',
  isActive: true
})
const vehicleForm = reactive({
  vehicleModelId: '', licensePlate: '', frameNumber: '', engineNumber: '',
  manufactureYear: new Date().getFullYear(), color: '', odometer: 0,
  purchaseDate: '', notes: '', isActive: true
})

const customerId = computed(() => String(route.params.id))
const modelName = (id: string) => {
  const model = models.value.find(x => x.id === id)
  const brand = brands.value.find(x => x.id === model?.brandId)
  return model ? `${brand?.name || ''} ${model.name}`.trim() : 'Chưa rõ dòng xe'
}
const modelOptions = computed(() => models.value.map(model => ({
  code: model.id,
  name: modelName(model.id)
})))
const replacementDueText = (item: PartReplacementReminder) => {
  const details: string[] = []
  if (item.dueOdometer !== undefined) details.push(`${formatNumber(item.dueOdometer)} km`)
  if (item.dueAt) details.push(formatDate(item.dueAt))
  return details.join(' · ')
}
const replacementStatusText = (item: PartReplacementReminder) => {
  if (item.isOverdue) return 'Đã đến hạn thay'
  const details: string[] = []
  if (item.remainingKm !== undefined) details.push(`còn ${formatNumber(item.remainingKm)} km`)
  if (item.remainingDays !== undefined) details.push(`còn ${formatNumber(item.remainingDays)} ngày`)
  return details.join(' · ') || 'Đang theo dõi'
}

const openCustomerForm = () => {
  if (!customer.value) return
  Object.assign(customerForm, {
    fullName: customer.value.fullName,
    phone: customer.value.phone,
    email: customer.value.email || '',
    addressDetails: normalizeAddressDetails(customer.value.addressDetails, customer.value.address),
    dateOfBirth: customer.value.dateOfBirth?.slice(0, 10) || '',
    gender: customer.value.gender || '',
    taxCode: customer.value.taxCode || '',
    notes: customer.value.notes || '',
    isActive: customer.value.isActive
  })
  customerModalOpen.value = true
}

const saveCustomer = async () => {
  savingCustomer.value = true
  try {
    const updated = await api.request<Customer>(`/customers/${customerId.value}`, {
      method: 'PUT',
      body: {
        ...customerForm,
        address: formatAddressDetails(customerForm.addressDetails),
        dateOfBirth: customerForm.dateOfBirth || null
      }
    })
    customer.value = updated
    customerModalOpen.value = false
    toast.success('Đã cập nhật khách hàng', `${updated.fullName} đã được lưu.`)
  } finally {
    savingCustomer.value = false
  }
}

const openVehicleForm = (vehicle?: Vehicle) => {
  editingVehicle.value = vehicle
  Object.assign(vehicleForm, vehicle ? {
    vehicleModelId: vehicle.vehicleModelId,
    licensePlate: vehicle.licensePlate,
    frameNumber: vehicle.frameNumber || '',
    engineNumber: vehicle.engineNumber || '',
    manufactureYear: vehicle.manufactureYear || new Date().getFullYear(),
    color: vehicle.color || '',
    odometer: vehicle.odometer || 0,
    purchaseDate: vehicle.purchaseDate?.slice(0, 10) || '',
    notes: vehicle.notes || '',
    isActive: vehicle.isActive
  } : {
    vehicleModelId: '', licensePlate: '', frameNumber: '', engineNumber: '',
    manufactureYear: new Date().getFullYear(), color: '', odometer: 0,
    purchaseDate: '', notes: '', isActive: true
  })
  modalOpen.value = true
}

const load = async () => {
  loading.value = true
  try {
    const [c, v, h, l, m, b, reminders] = await Promise.all([
      api.request<Customer>(`/customers/${customerId.value}`),
      api.request<PagedResult<Vehicle>>('/vehicles', { query: { customerId: customerId.value, pageSize: 100 } }),
      api.request<PagedResult<RepairOrder>>(`/customers/${customerId.value}/repair-history`, { query: { pageSize: 100 } }),
      api.request<{ account: LoyaltyAccount | null, transactions: any[] }>(`/customers/${customerId.value}/loyalty`),
      api.request<PagedResult<VehicleModel>>('/vehicle-models?pageSize=200'),
      api.request<PagedResult<VehicleBrand>>('/vehicle-brands?pageSize=200'),
      api.request<PartReplacementReminder[]>(`/customers/${customerId.value}/part-replacement-reminders`)
    ])
    customer.value = c
    vehicles.value = v.items
    history.value = h.items
    loyalty.value = l
    models.value = m.items
    brands.value = b.items
    replacementReminders.value = reminders
  } finally { loading.value = false }
}

const saveVehicle = async () => {
  saving.value = true
  try {
    await api.request(`/vehicles${editingVehicle.value ? `/${editingVehicle.value.id}` : ''}`, {
      method: editingVehicle.value ? 'PUT' : 'POST',
      body: {
        ...vehicleForm,
        customerId: customerId.value,
        purchaseDate: vehicleForm.purchaseDate || null,
        frameNumber: vehicleForm.frameNumber || null,
        engineNumber: vehicleForm.engineNumber || null
      }
    })
    toast.success(editingVehicle.value ? 'Đã cập nhật xe' : 'Đã thêm xe', vehicleForm.licensePlate.toUpperCase())
    modalOpen.value = false
    const page = await api.request<PagedResult<Vehicle>>('/vehicles', { query: { customerId: customerId.value, pageSize: 100 } })
    vehicles.value = page.items
  } finally { saving.value = false }
}

const removeVehicle = async (vehicle: Vehicle) => {
  if (!confirm(`Xóa xe ${vehicle.licensePlate}?`)) return
  await api.request(`/vehicles/${vehicle.id}`, { method: 'DELETE' })
  toast.success('Đã xóa xe', vehicle.licensePlate)
  vehicles.value = vehicles.value.filter(x => x.id !== vehicle.id)
}

onMounted(load)
</script>

<template>
  <div class="page">
    <NuxtLink to="/customers" class="back-link"><ArrowLeft :size="16" /> Danh sách khách hàng</NuxtLink>
    <div v-if="customer" class="page-header">
      <div>
        <div class="inline">
          <h1 class="page-title">{{ customer.fullName }}</h1>
          <AppBadge :tone="customer.isActive ? 'success' : 'neutral'">{{ customer.isActive ? 'Hoạt động' : 'Tạm khóa' }}</AppBadge>
        </div>
        <p class="page-subtitle mono">{{ customer.code }} · {{ customer.phone }} · {{ customer.email || 'Chưa có email' }}</p>
      </div>
      <div class="page-actions">
        <button class="btn btn-secondary" @click="openCustomerForm"><Pencil :size="17" /> Cập nhật</button>
        <button class="btn btn-secondary" @click="openVehicleForm()"><Plus :size="17" /> Thêm xe</button>
        <NuxtLink class="btn btn-accent" :to="{ path: '/repair-orders/new', query: { customerId } }"><ClipboardList :size="17" /> Tạo phiếu sửa</NuxtLink>
      </div>
    </div>
    <div v-else-if="loading" class="loading-skeleton" style="height: 120px" />

    <section class="profile-grid">
      <article class="card">
        <header class="card-header"><h2 class="card-title">Thông tin khách hàng</h2></header>
        <div class="card-body detail-grid">
          <div><span>Điện thoại</span><strong>{{ customer?.phone || '—' }}</strong></div>
          <div><span>Địa chỉ</span><strong>{{ customer?.address || '—' }}</strong></div>
          <div><span>Mã số thuế</span><strong>{{ customer?.taxCode || '—' }}</strong></div>
          <div><span>Ghi chú</span><strong>{{ customer?.notes || '—' }}</strong></div>
        </div>
      </article>
      <article class="loyalty-card">
        <div class="tier-icon"><Star :size="22" /></div>
        <div>
          <span>Hạng thành viên</span>
          <strong>{{ loyalty?.account?.currentTierCode || customer?.loyaltyTierCode || 'MEMBER' }}</strong>
        </div>
        <div class="point-block">
          <strong>{{ formatNumber(loyalty?.account?.availablePoints || customer?.loyaltyPointBalance || 0) }}</strong>
          <span>điểm khả dụng</span>
        </div>
        <small>Chi tiêu tích lũy {{ formatCurrency(loyalty?.account?.eligibleSpend || 0) }}</small>
      </article>
    </section>

    <section class="card replacement-card">
      <header class="card-header"><div><h2 class="card-title"><BellRing :size="19" /> Lịch thay thế phụ tùng</h2><span class="section-note">Tính từ lần lắp hoàn tất gần nhất theo thời gian và số km</span></div><span class="muted">{{ replacementReminders.length }} phụ tùng</span></header>
      <div class="table-wrap">
        <table v-if="replacementReminders.length" class="data-table replacement-table">
          <thead><tr><th>Xe</th><th>Phụ tùng</th><th>Lắp gần nhất</th><th>Hạn thay dự kiến</th><th>Trạng thái</th><th>Phiếu sửa chữa</th></tr></thead>
          <tbody><tr v-for="item in replacementReminders" :key="`${item.vehicleId}-${item.partId}`"><td><strong class="mono">{{ item.licensePlate }}</strong><span class="cell-sub">ODO {{ item.currentOdometer !== undefined ? `${formatNumber(item.currentOdometer)} km` : 'chưa cập nhật' }}</span></td><td><NuxtLink class="cell-main link" :to="`/inventory/${item.partId}`">{{ item.partName }}</NuxtLink><span class="cell-sub mono">{{ item.partCode }}</span></td><td>{{ formatDate(item.installedAt) }}<span class="cell-sub">{{ item.installedOdometer !== undefined ? `${formatNumber(item.installedOdometer)} km` : 'Không có ODO' }}</span></td><td>{{ replacementDueText(item) }}</td><td><AppBadge :tone="item.isOverdue ? 'danger' : item.isDueSoon ? 'warning' : 'neutral'">{{ replacementStatusText(item) }}</AppBadge></td><td><NuxtLink class="link mono" :to="`/repair-orders/${item.lastRepairOrderId}`">Xem phiếu</NuxtLink></td></tr></tbody>
        </table>
        <AppEmpty v-else :icon="BellRing" title="Chưa có lịch thay phụ tùng" message="Lịch sẽ được tạo khi phụ tùng có chu kỳ thay được lắp và hoàn tất trên phiếu sửa chữa." />
      </div>
    </section>

    <section class="card">
      <header class="card-header">
        <h2 class="card-title">Xe của khách hàng</h2>
        <span class="muted">{{ vehicles.length }} xe</span>
      </header>
      <div v-if="vehicles.length" class="vehicle-grid">
        <article v-for="vehicle in vehicles" :key="vehicle.id" class="vehicle-card">
          <div class="vehicle-icon"><Bike :size="22" /></div>
          <div>
            <strong>{{ vehicle.licensePlate }}</strong>
            <span>{{ modelName(vehicle.vehicleModelId) }} · {{ vehicle.manufactureYear || '—' }}</span>
            <small>Số máy: {{ vehicle.engineNumber || '—' }} · ODO: {{ formatNumber(vehicle.odometer || 0) }} km</small>
          </div>
          <div class="inline"><button class="icon-btn" title="Cập nhật xe" @click="openVehicleForm(vehicle)"><Pencil :size="15" /></button><button v-if="!isEmployee" class="icon-btn danger-button" title="Xóa xe" @click="removeVehicle(vehicle)"><Trash2 :size="15" /></button><NuxtLink class="btn btn-secondary btn-sm" :to="{ path: '/repair-orders/new', query: { customerId, vehicleId: vehicle.id } }">Tiếp nhận</NuxtLink></div>
        </article>
      </div>
      <AppEmpty v-else :icon="Bike" title="Khách hàng chưa có xe" message="Thêm phương tiện để có thể tạo phiếu sửa chữa." />
    </section>

    <section class="card">
      <header class="card-header"><h2 class="card-title">Lịch sử sửa chữa</h2><span class="muted">{{ history.length }} phiếu</span></header>
      <div class="table-wrap">
        <table v-if="history.length" class="data-table">
          <thead><tr><th>Mã phiếu</th><th>Ngày nhận</th><th>Trạng thái</th><th class="text-right">Tổng tiền</th></tr></thead>
          <tbody><tr v-for="order in history" :key="order.id"><td><NuxtLink class="cell-main link mono" :to="`/repair-orders/${order.id}`">{{ order.code }}</NuxtLink></td><td>{{ formatDate(order.receivedAt, true) }}</td><td><AppBadge :tone="statusTone(order.status)">{{ statusLabel(order.status) }}</AppBadge></td><td class="text-right cell-main">{{ formatCurrency(order.finalTotal) }}</td></tr></tbody>
        </table>
        <AppEmpty v-else title="Chưa có lịch sử sửa chữa" message="Các phiếu sửa của khách sẽ hiển thị tại đây." />
      </div>
    </section>

    <AppModal
      :open="customerModalOpen"
      title="Cập nhật khách hàng"
      description="Thông tin này được sử dụng trên phiếu sửa chữa, hóa đơn và hồ sơ chăm sóc khách hàng."
      width="760px"
      @close="customerModalOpen = false"
    >
      <form id="customer-detail-form" class="form-grid" @submit.prevent="saveCustomer">
        <div class="field">
          <label>Họ và tên *</label>
          <input v-model.trim="customerForm.fullName" class="input" required maxlength="150" />
        </div>
        <div class="field">
          <label>Số điện thoại *</label>
          <input v-model.trim="customerForm.phone" class="input" required maxlength="30" />
        </div>
        <div class="field">
          <label>Email</label>
          <input v-model.trim="customerForm.email" class="input" type="email" maxlength="200" />
        </div>
        <div class="field">
          <label>Mã số thuế</label>
          <input v-model.trim="customerForm.taxCode" class="input" maxlength="30" />
        </div>
        <div class="field">
          <label>Ngày sinh</label>
          <input v-model="customerForm.dateOfBirth" class="input" type="date" />
        </div>
        <div class="field">
          <label>Giới tính</label>
          <select v-model="customerForm.gender" class="select">
            <option value="">Không khai báo</option>
            <option value="Male">Nam</option>
            <option value="Female">Nữ</option>
            <option value="Other">Khác</option>
          </select>
        </div>
        <AppAddressFields v-model="customerForm.addressDetails" />
        <div class="field span-2">
          <label>Ghi chú</label>
          <textarea v-model.trim="customerForm.notes" class="textarea" maxlength="2000" />
        </div>
        <label class="check-row span-2">
          <input v-model="customerForm.isActive" type="checkbox" /> Hồ sơ đang hoạt động
        </label>
      </form>
      <template #footer>
        <button class="btn btn-secondary" :disabled="savingCustomer" @click="customerModalOpen = false">Hủy</button>
        <button class="btn btn-primary" form="customer-detail-form" :disabled="savingCustomer">
          {{ savingCustomer ? 'Đang lưu...' : 'Lưu thay đổi' }}
        </button>
      </template>
    </AppModal>

    <AppModal :open="modalOpen" :title="editingVehicle ? 'Cập nhật thông tin xe' : 'Thêm xe cho khách hàng'" width="720px" @close="modalOpen = false">
      <form id="vehicle-form" class="form-grid" @submit.prevent="saveVehicle">
        <div class="field"><label>Dòng xe *</label><AppSearchSelect v-model="vehicleForm.vehicleModelId" :options="modelOptions" placeholder="Chọn dòng xe" search-placeholder="Tìm hãng hoặc dòng xe..." required :clearable="false" /></div>
        <div class="field"><label>Biển số *</label><input v-model.trim="vehicleForm.licensePlate" class="input" required placeholder="59-A1 123.45" /></div>
        <div class="field"><label>Số khung</label><input v-model.trim="vehicleForm.frameNumber" class="input" /></div>
        <div class="field"><label>Số máy</label><input v-model.trim="vehicleForm.engineNumber" class="input" /></div>
        <div class="field"><label>Năm sản xuất</label><AppNumberInput v-model="vehicleForm.manufactureYear" class="input" min="1900" max="2200" /></div>
        <div class="field"><label>Màu xe</label><input v-model.trim="vehicleForm.color" class="input" /></div>
        <div class="field"><label>Số km hiện tại</label><AppNumberInput v-model="vehicleForm.odometer" class="input" min="0" /></div>
        <div class="field"><label>Ngày mua</label><input v-model="vehicleForm.purchaseDate" class="input" type="date" /></div>
        <div class="field span-2"><label>Ghi chú</label><textarea v-model="vehicleForm.notes" class="textarea" /></div>
        <label class="check-row span-2"><input v-model="vehicleForm.isActive" type="checkbox" /> Xe đang hoạt động</label>
      </form>
      <template #footer><button class="btn btn-secondary" @click="modalOpen = false">Hủy</button><button class="btn btn-primary" form="vehicle-form" :disabled="saving">{{ saving ? 'Đang lưu...' : editingVehicle ? 'Cập nhật xe' : 'Lưu xe' }}</button></template>
    </AppModal>
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.profile-grid { display: grid; grid-template-columns: minmax(0, 1.5fr) minmax(300px, .7fr); gap: 18px; }
.detail-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 20px; }
.detail-grid span, .detail-grid strong { display: block; }
.detail-grid span { color: var(--muted); font-size: 11px; }
.detail-grid strong { margin-top: 4px; color: var(--navy-950); }
.loyalty-card { display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: 14px; padding: 22px; border-radius: var(--radius-lg); color: white; background: linear-gradient(135deg, var(--navy-950), var(--navy-700)); box-shadow: var(--shadow); }
.loyalty-card span, .loyalty-card strong { display: block; }
.loyalty-card > div > span, .loyalty-card small { color: #a9c2d5; font-size: 11px; }
.loyalty-card > div > strong { font-size: 20px; }
.tier-icon { display: grid; width: 42px; height: 42px; place-items: center; border-radius: 12px; color: var(--navy-950); background: var(--amber); }
.point-block { text-align: right; }
.point-block strong { color: var(--amber); font-size: 24px !important; }
.loyalty-card small { grid-column: 2 / -1; }
.vehicle-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 14px; padding: 18px; }
.vehicle-card { display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: 13px; padding: 16px; border: 1px solid var(--line); border-radius: 13px; }
.vehicle-icon { display: grid; width: 44px; height: 44px; place-items: center; border-radius: 12px; color: var(--navy-800); background: var(--blue-soft); }
.vehicle-card strong, .vehicle-card span, .vehicle-card small { display: block; }
.vehicle-card > div > strong { color: var(--navy-950); font-size: 17px; }
.vehicle-card span, .vehicle-card small { margin-top: 2px; color: var(--muted); font-size: 11px; }
.link { color: var(--blue); }
.replacement-card { overflow: hidden; }.replacement-table { min-width: 900px; }
@media (max-width: 900px) { .profile-grid, .vehicle-grid { grid-template-columns: 1fr; } }
@media (max-width: 560px) { .detail-grid { grid-template-columns: 1fr; gap: 13px; }.loyalty-card { grid-template-columns: auto 1fr; padding: 18px; }.point-block { grid-column: 1 / -1; text-align: left; }.loyalty-card small { grid-column: 1 / -1; }.vehicle-grid { padding: 14px; }.vehicle-card { grid-template-columns: auto 1fr; }.vehicle-card > .btn,.vehicle-card > .icon-btn { grid-column: 1 / -1; width: 100%; } }
</style>
