<script setup lang="ts">
import { ArrowLeft, ClipboardCheck, ImagePlus } from '@lucide/vue'
import type { Customer, Employee, PagedResult, Vehicle } from '~/types/api'

const route = useRoute()
const api = useApi()
const { uploadImage, deleteImage } = useMedia()
const auth = useAuth()
const toast = useToast()
const isEmployee = computed(() => auth.hasAnyRole('Employee'))
const customers = ref<Customer[]>([])
const vehicles = ref<Vehicle[]>([])
const employees = ref<Employee[]>([])
const saving = ref(false)
const conditionImageInput = ref<HTMLInputElement>()
const maxConditionImages = 10
const form = reactive({
  customerId: String(route.query.customerId || ''),
  vehicleId: String(route.query.vehicleId || ''),
  expectedDeliveryAt: '',
  odometerIn: 0,
  fuelLevel: '',
  vehicleCondition: '',
  vehicleConditionImages: [] as string[],
  customerRequest: '',
  diagnosis: '',
  internalNotes: '',
  priority: 'Normal',
  serviceAdvisorId: ''
})
const customerOptions = computed(() => customers.value.map(item => ({
  code: item.id,
  name: `${item.fullName} · ${item.phone}`
})))
const vehicleOptions = computed(() => vehicles.value.map(item => ({
  code: item.id,
  name: item.licensePlate
})))
const employeeOptions = computed(() => employees.value.map(item => ({
  code: item.id,
  name: `${item.fullName} · ${item.position}`
})))

const loadReferences = async () => {
  const [c, e] = await Promise.all([
    api.request<PagedResult<Customer>>('/customers?pageSize=200'),
    isEmployee.value
      ? Promise.resolve({ items: [], total: 0, page: 1, pageSize: 200, totalPages: 0 } as PagedResult<Employee>)
      : api.request<PagedResult<Employee>>('/employees?pageSize=200')
  ])
  customers.value = c.items
  employees.value = e.items.filter(x => x.status === 'Active')
  if (form.customerId) await loadVehicles()
}

const loadVehicles = async () => {
  form.vehicleId = ''
  form.odometerIn = 0
  const page = await api.request<PagedResult<Vehicle>>('/vehicles', { query: { customerId: form.customerId, pageSize: 100 } })
  vehicles.value = page.items
  const requested = String(route.query.vehicleId || '')
  if (requested && page.items.some(x => x.id === requested)) form.vehicleId = requested
}

watch(() => form.customerId, loadVehicles)
watch(() => form.vehicleId, (vehicleId) => {
  const vehicle = vehicles.value.find(x => x.id === vehicleId)
  form.odometerIn = vehicle?.odometer || 0
})

const submit = async () => {
  saving.value = true
  try {
    const order = await api.request<{ id: string, code: string }>('/repair-orders', {
      method: 'POST',
      body: {
        ...form,
        expectedDeliveryAt: form.expectedDeliveryAt ? new Date(form.expectedDeliveryAt).toISOString() : null,
        serviceAdvisorId: form.serviceAdvisorId || null
      }
    })
    toast.success('Đã tạo phiếu sửa chữa', order.code)
    await navigateTo(`/repair-orders/${order.id}`)
  } finally { saving.value = false }
}

const chooseConditionImages = () => {
  if (conditionImageInput.value) conditionImageInput.value.value = ''
  conditionImageInput.value?.click()
}

const readConditionImages = async (event: Event) => {
  const files = Array.from((event.target as HTMLInputElement).files || [])
  const available = maxConditionImages - form.vehicleConditionImages.length
  if (available <= 0) {
    toast.error('Đã đủ số lượng ảnh', `Mỗi phiếu được lưu tối đa ${maxConditionImages} ảnh tình trạng xe.`)
    return
  }
  for (const file of files.slice(0, available)) {
    if (!file.type.startsWith('image/')) {
      toast.error('Tệp không hợp lệ', `${file.name} không phải là ảnh.`)
      continue
    }
    if (file.size > 2 * 1024 * 1024) {
      toast.error('Ảnh quá lớn', `${file.name} vượt quá 2 MB.`)
      continue
    }
    form.vehicleConditionImages.push(await uploadImage(file, 'repair-orders'))
  }
}

const removeConditionImage = async (index: number) => {
  const [path] = form.vehicleConditionImages.splice(index, 1)
  await deleteImage(path)
}

onMounted(loadReferences)
</script>

<template>
  <div class="page narrow-page">
    <NuxtLink to="/repair-orders" class="back-link"><ArrowLeft :size="16" /> Danh sách phiếu sửa</NuxtLink>
    <div class="page-header">
      <div><h1 class="page-title">Tiếp nhận sửa chữa</h1><p class="page-subtitle">Ghi nhận tình trạng ban đầu và yêu cầu của khách để bắt đầu quy trình.</p></div>
    </div>
    <form class="stack" @submit.prevent="submit">
      <section class="card">
        <header class="card-header"><h2 class="card-title">Khách hàng & phương tiện</h2><span class="step-number">01</span></header>
        <div class="card-body form-grid">
          <div class="field"><label>Khách hàng *</label><AppSearchSelect v-model="form.customerId" :options="customerOptions" placeholder="Chọn khách hàng" search-placeholder="Tìm tên hoặc số điện thoại..." required :clearable="false" /></div>
          <div class="field"><label>Xe tiếp nhận *</label><AppSearchSelect v-model="form.vehicleId" :options="vehicleOptions" placeholder="Chọn biển số" search-placeholder="Tìm biển số..." required :clearable="false" :disabled="!form.customerId" /><small v-if="form.customerId && !vehicles.length" class="form-hint">Khách chưa có xe. Thêm xe trong hồ sơ khách hàng.</small></div>
          <div class="field"><label>ODO khi nhận (km)</label><AppNumberInput v-model="form.odometerIn" class="input" min="0" /></div>
          <div class="field"><label>Mức nhiên liệu</label><select v-model="form.fuelLevel" class="select"><option value="">Không ghi nhận</option><option>0%</option><option>25%</option><option>50%</option><option>75%</option><option>100%</option></select></div>
        </div>
      </section>
      <section class="card">
        <header class="card-header"><h2 class="card-title">Nội dung tiếp nhận</h2><span class="step-number">02</span></header>
        <div class="card-body form-grid">
          <div class="field span-2"><label>Yêu cầu của khách hàng *</label><textarea v-model.trim="form.customerRequest" class="textarea" required placeholder="Mô tả vấn đề, nhu cầu sửa chữa/nâng cấp..." /></div>
          <div class="field span-2"><label>Tình trạng xe khi nhận *</label><textarea v-model.trim="form.vehicleCondition" class="textarea" required placeholder="Trầy xước, phụ kiện đi kèm, tình trạng vận hành..." /></div>
          <div class="field span-2 condition-images-field">
            <label>Ảnh tình trạng xe <span class="muted">(tùy chọn, tối đa {{ maxConditionImages }} ảnh)</span></label>
            <input ref="conditionImageInput" class="visually-hidden" type="file" accept="image/*" capture="environment" multiple @change="readConditionImages" />
            <AppImageGallery v-if="form.vehicleConditionImages.length" :images="form.vehicleConditionImages" alt="Ảnh tình trạng xe" removable @remove="removeConditionImage" />
            <button v-if="form.vehicleConditionImages.length < maxConditionImages" type="button" class="upload-condition-images" @click="chooseConditionImages"><ImagePlus :size="22" /><span>Chụp hoặc chọn ảnh</span><small>Mỗi ảnh tối đa 2 MB</small></button>
          </div>
          <div class="field span-2"><label>Chẩn đoán sơ bộ</label><textarea v-model.trim="form.diagnosis" class="textarea" /></div>
        </div>
      </section>
      <section class="card">
        <header class="card-header"><h2 class="card-title">Kế hoạch xử lý</h2><span class="step-number">03</span></header>
        <div class="card-body form-grid">
          <div class="field"><label>Ngày dự kiến giao</label><input v-model="form.expectedDeliveryAt" class="input" type="datetime-local" /></div>
          <div class="field"><label>Mức ưu tiên</label><select v-model="form.priority" class="select"><option value="Low">Thấp</option><option value="Normal">Bình thường</option><option value="High">Cao</option><option value="Urgent">Khẩn cấp</option></select></div>
          <div v-if="!isEmployee" class="field"><label>Nhân viên thực hiện</label><AppSearchSelect v-model="form.serviceAdvisorId" :options="employeeOptions" placeholder="Chưa phân công" search-placeholder="Tìm nhân viên..." /></div>
          <div class="field"><label>Ghi chú nội bộ</label><input v-model.trim="form.internalNotes" class="input" /></div>
        </div>
      </section>
      <div class="submit-bar"><NuxtLink class="btn btn-secondary" to="/repair-orders">Hủy</NuxtLink><button class="btn btn-accent" :disabled="saving"><ClipboardCheck :size="18" /> {{ saving ? 'Đang tạo phiếu...' : 'Tạo phiếu sửa chữa' }}</button></div>
    </form>
  </div>
</template>

<style scoped>
.narrow-page { max-width: 980px; margin: 0 auto; }
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.step-number { display: grid; width: 32px; height: 32px; place-items: center; border-radius: 10px; color: var(--navy-900); background: var(--amber-soft); font-weight: 800; }
.form-hint { color: var(--red); }
.visually-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); }
.condition-images-field { display: grid; gap: 10px; }
.upload-condition-images { display: flex; min-height: 82px; align-items: center; justify-content: center; gap: 8px; border: 1px dashed #9eb2c2; border-radius: 12px; color: var(--navy-800); background: #f7fafc; }
.upload-condition-images small { color: var(--muted); }
.submit-bar { position: sticky; z-index: 5; bottom: 14px; display: flex; justify-content: flex-end; gap: 10px; padding: 13px; border: 1px solid var(--line); border-radius: 14px; background: rgb(255 255 255 / 92%); box-shadow: 0 14px 40px rgb(15 35 55 / 15%); backdrop-filter: blur(10px); }
@media (max-width: 560px) { .upload-condition-images { align-items: center; flex-direction: column; padding: 14px; text-align: center; }.submit-bar { bottom: 8px; display: grid; grid-template-columns: .7fr 1.3fr; padding: 9px; }.submit-bar .btn { width: 100%; padding-right: 10px; padding-left: 10px; } }
</style>
