<script setup lang="ts">
import {
  AlertTriangle,
  ArrowLeft,
  Bike,
  CheckCircle2,
  ExternalLink,
  ImagePlus,
  MapPin,
  PackageCheck,
  Pencil,
  Plus,
  ReceiptText,
  Trash2,
  UserRound,
  Wrench
} from '@lucide/vue'
import type {
  Customer,
  Employee,
  Invoice,
  PagedResult,
  Part,
  PartCategory,
  RepairOrder,
  RepairOrderStatus,
  RepairOrderItem,
  ServiceCategory,
  Vehicle,
  WarehouseLocation
} from '~/types/api'
import { entityDetailRoute } from '~/utils/entityRoute'
import { formatCurrency, formatDate, formatNumber, statusLabel, statusTone } from '~/utils/format'

const route = useRoute()
const api = useApi()
const { uploadImage, deleteImage } = useMedia()
const auth = useAuth()
const toast = useToast()
const isEmployee = computed(() => auth.hasAnyRole('Employee'))
const order = ref<RepairOrder>()
const relatedInvoice = ref<Invoice>()
const customer = ref<Customer>()
const vehicle = ref<Vehicle>()
const employees = ref<Employee[]>([])
const parts = ref<Part[]>([])
const partCategories = ref<PartCategory[]>([])
const services = ref<ServiceCategory[]>([])
const warehouseLocations = ref<WarehouseLocation[]>([])
const loading = ref(true)
const saving = ref(false)
const deletingItemId = ref('')
const itemModal = ref(false)
const editingItemId = ref('')
const statusModal = ref(false)
const invoiceModal = ref(false)
const odometerModal = ref(false)
const issueModal = ref(false)
const issuingItemId = ref('')
const issueItem = ref<RepairOrderItem>()
const issueWarehouseLocationId = ref('')
const conditionImageInput = ref<HTMLInputElement>()
const maxConditionImages = 10
const statusForm = reactive({ status: 'Inspecting' as RepairOrderStatus, note: '' })
const invoiceForm = reactive({ discountType: 'Amount' as 'Amount' | 'Percentage', discountValue: 0, couponCode: '', taxRate: 0, notes: '' })
const odometerForm = reactive({ odometerIn: 0 })
const itemForm = reactive({
  itemType: '' as '' | 'Service' | 'Part', serviceId: '', partCategoryId: '', partId: '', description: '', quantity: 1,
  unitPrice: 0, discountType: 'Amount' as 'Amount' | 'Percentage', discountValue: 0, assignedEmployeeId: '', technicianNotes: ''
})

const orderId = computed(() => String(route.params.id))
const isOrderLocked = computed(() => !!order.value?.isDeleted || ['Completed', 'Delivered'].includes(order.value?.status || ''))
const statusOptions: RepairOrderStatus[] = ['Received', 'Inspecting', 'AwaitingApproval', 'Repairing', 'AwaitingParts', 'Completed', 'Delivered', 'Cancelled']
const canCreateInvoice = computed(() => {
  const activeItems = order.value?.items.filter(x => x.workStatus !== 'Cancelled') || []
  return order.value?.status === 'Repairing'
    && activeItems.length > 0
    && activeItems.every(x => x.workStatus === 'Completed')
})
const partCategoryOptions = computed(() => partCategories.value
  .filter(category => !category.isDeleted && category.isActive)
  .map(category => ({ code: category.id, name: category.name })))
const partOptions = computed(() => parts.value
  .filter(part => !part.isDeleted && part.isActive && part.partCategoryId === itemForm.partCategoryId)
  .map(part => ({
  code: part.id,
  name: `${part.name}${part.specifications?.length ? ` · ${part.specifications.map(x => x.value).join(' · ')}` : ''} · còn ${formatNumber(part.quantityOnHand)}`
})))
const serviceOptions = computed(() => services.value.filter(service => !service.isDeleted).map(service => ({
  code: service.id,
  name: `${service.name} · ${formatCurrency(service.defaultPrice)}`
})))
const employeeOptions = computed(() => employees.value.filter(employee => !employee.isDeleted).map(employee => ({
  code: employee.id,
  name: employee.fullName
})))
const issuePart = computed(() => parts.value.find(x => x.id === issueItem.value?.partId))
const issueStockRows = computed(() => {
  const part = issuePart.value
  if (!part) return []
  if (part.warehouseStocks?.length) {
    return part.warehouseStocks.filter(x => x.quantityOnHand > 0).map(stock => ({
      location: warehouseLocations.value.find(x => x.id === stock.warehouseLocationId && x.isActive && !x.isDeleted),
      quantity: stock.quantityOnHand
    })).filter(x => !!x.location)
  }
  const location = warehouseLocations.value.find(x => x.id === part.warehouseLocationId && x.isActive && !x.isDeleted)
  return location && part.quantityOnHand > 0 ? [{ location, quantity: part.quantityOnHand }] : []
})
const issueRemaining = computed(() => (issuePart.value?.quantityOnHand || 0) - (issueItem.value?.quantity || 0))
const selectedIssueStock = computed(() => issueStockRows.value.find(x =>
  x.location?.id === issueWarehouseLocationId.value))
const canIssueSelectedPart = computed(() => !!issueItem.value && !!issuePart.value
  && !!issueWarehouseLocationId.value
  && (selectedIssueStock.value?.quantity || 0) >= issueItem.value.quantity
  && !issueItem.value.inventoryIssued
  && issueItem.value.workStatus !== 'Cancelled'
  && issueItem.value.quantity > 0
  && issuePart.value.quantityOnHand >= issueItem.value.quantity)

const load = async () => {
  loading.value = true
  try {
    const current = await api.request<RepairOrder>(`/repair-orders/${orderId.value}`, {
      query: { includeDeleted: true }
    })
    order.value = current
    const [c, v, e, p, pc, s, wl, invoicePage] = await Promise.all([
      api.request<Customer>(`/customers/${current.customerId}?includeDeleted=true`),
      api.request<Vehicle>(`/vehicles/${current.vehicleId}?includeDeleted=true`),
      isEmployee.value
        ? Promise.resolve({ items: [], total: 0, page: 1, pageSize: 200, totalPages: 0 } as PagedResult<Employee>)
        : api.request<PagedResult<Employee>>('/employees?pageSize=200&includeDeleted=true'),
      api.request<PagedResult<Part>>('/parts?pageSize=200&includeDeleted=true'),
      isEmployee.value
        ? Promise.resolve({ items: [], total: 0, page: 1, pageSize: 200, totalPages: 0 } as PagedResult<PartCategory>)
        : api.request<PagedResult<PartCategory>>('/part-categories?pageSize=200&includeDeleted=true'),
      api.request<PagedResult<ServiceCategory>>('/service-categories?pageSize=200&includeDeleted=true'),
      api.request<PagedResult<WarehouseLocation>>('/warehouse-locations?pageSize=500&includeDeleted=true'),
      isEmployee.value
        ? Promise.resolve({ items: [], total: 0, page: 1, pageSize: 10, totalPages: 0 } as PagedResult<Invoice>)
        : api.request<PagedResult<Invoice>>('/invoices', {
            query: { repairOrderId: current.id, pageSize: 10 }
          })
    ])
    customer.value = c
    vehicle.value = v
    employees.value = e.items.map(x => ({ ...x, fullName: `${x.fullName}${x.isDeleted ? ' (đã xóa)' : ''}` }))
    parts.value = p.items
    partCategories.value = pc.items
    services.value = s.items
    warehouseLocations.value = wl.items
    relatedInvoice.value = invoicePage.items.find(x => x.paymentStatus !== 'Cancelled')
  } finally { loading.value = false }
}

const openAddItem = () => {
  editingItemId.value = ''
  Object.assign(itemForm, {
    itemType: '', serviceId: '', partCategoryId: '', partId: '', description: '', quantity: 1,
    unitPrice: 0, discountType: 'Amount', discountValue: 0, assignedEmployeeId: '', technicianNotes: ''
  })
  itemModal.value = true
}

const openEditItem = (item: RepairOrderItem) => {
  editingItemId.value = item.id
  Object.assign(itemForm, {
    itemType: item.itemType,
    serviceId: item.serviceId || '',
    partCategoryId: parts.value.find(x => x.id === item.partId)?.partCategoryId || '',
    partId: item.partId || '',
    description: item.description,
    quantity: item.itemType === 'Service' ? 1 : item.quantity,
    unitPrice: item.unitPrice,
    discountType: item.discountType || 'Amount',
    discountValue: item.discountValue || item.discountAmount,
    assignedEmployeeId: item.assignedEmployeeId || '',
    technicianNotes: item.technicianNotes || ''
  })
  itemModal.value = true
}

const selectItemType = () => {
  itemForm.serviceId = ''
  itemForm.partCategoryId = ''
  itemForm.partId = ''
  itemForm.description = ''
  itemForm.unitPrice = 0
  itemForm.quantity = 1
}

const selectPartCategory = () => {
  itemForm.partId = ''
  itemForm.description = ''
  itemForm.unitPrice = 0
}

const selectPart = (partId = itemForm.partId) => {
  const part = parts.value.find(x => x.id === partId)
  if (!part) return
  itemForm.description = part.name
  itemForm.unitPrice = part.salePrice
}

const selectService = (serviceId = itemForm.serviceId) => {
  const service = services.value.find(x => x.id === serviceId)
  if (!service) return
  itemForm.description = service.name
  itemForm.unitPrice = service.defaultPrice
  itemForm.quantity = 1
}

const saveItem = async () => {
  if (!itemForm.itemType) {
    toast.error('Chưa chọn loại hạng mục', 'Vui lòng chọn dịch vụ hoặc phụ tùng.')
    return
  }
  if (itemForm.itemType === 'Part' && !itemForm.partCategoryId) {
    toast.error('Chưa chọn danh mục phụ tùng', 'Vui lòng chọn danh mục trước khi chọn phụ tùng.')
    return
  }
  saving.value = true
  try {
    order.value = await api.request(`/repair-orders/${orderId.value}/items${editingItemId.value ? `/${editingItemId.value}` : ''}`, {
      method: editingItemId.value ? 'PUT' : 'POST',
      body: {
        ...itemForm,
        quantity: itemForm.itemType === 'Service' ? 1 : itemForm.quantity,
        serviceId: itemForm.itemType === 'Service' ? itemForm.serviceId : null,
        partId: itemForm.itemType === 'Part' ? itemForm.partId : null,
        assignedEmployeeId: itemForm.assignedEmployeeId || null
      }
    })
    toast.success(editingItemId.value ? 'Đã cập nhật hạng mục' : 'Đã thêm hạng mục', itemForm.description)
    itemModal.value = false
  } finally { saving.value = false }
}

const removeItem = async (item: RepairOrderItem) => {
  if (!confirm(`Xóa hạng mục “${item.description}” khỏi phiếu sửa chữa?`)) return
  deletingItemId.value = item.id
  try {
    order.value = await api.request(`/repair-orders/${orderId.value}/items/${item.id}`, {
      method: 'DELETE'
    })
    toast.success('Đã xóa hạng mục sửa chữa', item.description)
  } finally { deletingItemId.value = '' }
}

const openIssuePart = (item: RepairOrderItem) => {
  issueItem.value = item
  issueWarehouseLocationId.value = issueStockRows.value.find(x =>
    x.quantity >= item.quantity)?.location?.id || ''
  issueModal.value = true
}

const confirmIssuePart = async () => {
  if (!issueItem.value || !canIssueSelectedPart.value) return
  issuingItemId.value = issueItem.value.id
  try {
    order.value = await api.request(`/repair-orders/${orderId.value}/items/${issueItem.value.id}/issue`, {
      method: 'POST',
      body: { warehouseLocationId: issueWarehouseLocationId.value }
    })
    if (issuePart.value) {
      issuePart.value.quantityOnHand -= issueItem.value.quantity
      const selectedStock = issuePart.value.warehouseStocks?.find(x =>
        x.warehouseLocationId === issueWarehouseLocationId.value)
      if (selectedStock) selectedStock.quantityOnHand -= issueItem.value.quantity
    }
    toast.success('Đã xuất phụ tùng', `${issueItem.value.description} · ${formatNumber(issueItem.value.quantity)} ${issuePart.value?.unit || ''}`)
    issueModal.value = false
  } finally { issuingItemId.value = '' }
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

const openOdometer = () => {
  odometerForm.odometerIn = order.value?.odometerIn ?? vehicle.value?.odometer ?? 0
  odometerModal.value = true
}

const saveOdometer = async () => {
  saving.value = true
  try {
    order.value = await api.request(`/repair-orders/${orderId.value}/odometer`, {
      method: 'PATCH', body: odometerForm
    })
    if (vehicle.value) vehicle.value.odometer = odometerForm.odometerIn
    toast.success('Đã cập nhật ODO', `${formatNumber(odometerForm.odometerIn)} km`)
    odometerModal.value = false
  } finally { saving.value = false }
}

const updateWork = async (itemId: string, status: string) => {
  order.value = await api.request(`/repair-orders/${orderId.value}/items/${itemId}/work`, {
    method: 'PATCH', body: { status, technicianNotes: null }
  })
  toast.success('Đã cập nhật công việc', statusLabel(status))
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

const deletedLabel = (name?: string, isDeleted?: boolean) =>
  name ? `${name}${isDeleted ? ' (đã xóa)' : ''}` : '—'

const employeeName = (id?: string) => {
  const employee = employees.value.find(x => x.id === id)
  return employee ? deletedLabel(employee.fullName, employee.isDeleted) : id ? 'Nhân viên đã phân công' : 'Chưa phân công'
}

const removeOrder = async () => {
  if (!order.value || !confirm(`Xóa phiếu sửa chữa ${order.value.code}? Dữ liệu vẫn được lưu để tra cứu lịch sử.`)) return
  saving.value = true
  try {
    await api.request(`/repair-orders/${orderId.value}`, { method: 'DELETE' })
    toast.success('Đã xóa phiếu sửa chữa', order.value.code)
    await navigateTo('/repair-orders')
  } finally { saving.value = false }
}

const saveConditionImages = async (images: string[]) => {
  order.value = await api.request(`/repair-orders/${orderId.value}/condition-images`, {
    method: 'PATCH', body: { images }
  })
}

const chooseConditionImages = () => {
  if (conditionImageInput.value) conditionImageInput.value.value = ''
  conditionImageInput.value?.click()
}

const readConditionImages = async (event: Event) => {
  if (!order.value) return
  const current = order.value.vehicleConditionImages || []
  const files = Array.from((event.target as HTMLInputElement).files || [])
  const available = maxConditionImages - current.length
  if (available <= 0) {
    toast.error('Đã đủ số lượng ảnh', `Mỗi phiếu được lưu tối đa ${maxConditionImages} ảnh tình trạng xe.`)
    return
  }
  const added: string[] = []
  for (const file of files.slice(0, available)) {
    if (!file.type.startsWith('image/')) {
      toast.error('Tệp không hợp lệ', `${file.name} không phải là ảnh.`)
      continue
    }
    if (file.size > 2 * 1024 * 1024) {
      toast.error('Ảnh quá lớn', `${file.name} vượt quá 2 MB.`)
      continue
    }
    added.push(await uploadImage(file, 'repair-orders'))
  }
  if (added.length) {
    await saveConditionImages([...current, ...added])
    toast.success('Đã cập nhật ảnh tình trạng xe', `${added.length} ảnh mới`)
  }
}

const removeConditionImage = async (index: number) => {
  if (!order.value) return
  const images = [...(order.value.vehicleConditionImages || [])]
  const [path] = images.splice(index, 1)
  await saveConditionImages(images)
  await deleteImage(path)
  toast.success('Đã xóa ảnh tình trạng xe')
}

onMounted(load)
</script>

<template>
  <div class="page">
    <NuxtLink to="/repair-orders" class="back-link"><ArrowLeft :size="16" /> Danh sách phiếu sửa</NuxtLink>
    <div v-if="order" class="page-header">
      <div>
        <div class="inline"><h1 class="page-title mono">{{ order.code }}</h1><AppBadge v-if="order.isDeleted" tone="neutral">Đã xóa</AppBadge><AppBadge v-else :tone="statusTone(order.status)">{{ statusLabel(order.status) }}</AppBadge><AppBadge :tone="order.priority === 'Urgent' ? 'danger' : order.priority === 'High' ? 'warning' : 'neutral'">{{ statusLabel(order.priority) }}</AppBadge></div>
        <p class="page-subtitle">Tiếp nhận {{ formatDate(order.receivedAt, true) }} · Hẹn giao {{ formatDate(order.expectedDeliveryAt, true) }}</p>
      </div>
      <div v-if="!isEmployee" class="page-actions">
        <template v-if="isOrderLocked">
          <NuxtLink v-if="relatedInvoice" class="btn btn-accent" :to="`/invoices/${relatedInvoice.id}`"><ReceiptText :size="17" /> Xem hóa đơn</NuxtLink>
          <button v-else class="btn btn-accent" disabled title="Không tìm thấy hóa đơn còn hiệu lực"><ReceiptText :size="17" /> Xem hóa đơn</button>
        </template>
        <template v-else>
          <button class="btn btn-secondary danger-button" :disabled="saving" @click="removeOrder"><Trash2 :size="17" /> Xóa phiếu</button>
          <button class="btn btn-secondary" @click="openOdometer"><Pencil :size="17" /> Cập nhật ODO</button>
          <button class="btn btn-secondary" @click="openStatus"><CheckCircle2 :size="17" /> Đổi trạng thái</button>
          <button class="btn btn-accent" :disabled="!canCreateInvoice" :title="canCreateInvoice ? 'Xuất hóa đơn và hoàn tất phiếu' : 'Cần chuyển sang Đang sửa và hoàn thành tất cả hạng mục'" @click="invoiceModal = true"><ReceiptText :size="17" /> Hoàn tất & xuất hóa đơn</button>
        </template>
      </div>
    </div>
    <div v-else-if="loading" class="loading-skeleton" style="height: 110px" />

    <section v-if="isOrderLocked" class="locked-notice">
      <AlertTriangle v-if="order?.isDeleted" :size="19" />
      <CheckCircle2 v-else :size="19" />
      <div><strong>{{ order?.isDeleted ? 'Phiếu sửa chữa đã bị xóa' : 'Phiếu sửa chữa đã hoàn tất và xuất hóa đơn' }}</strong><span>Thông tin tiếp nhận, ODO, ảnh, hạng mục và tiến độ chỉ được hiển thị để tra cứu, không thể cập nhật.</span></div>
    </section>

    <section v-if="order" class="summary-strip">
      <div><UserRound :size="19" /><span>Khách hàng<strong><AppEntityLink :to="entityDetailRoute('Customer', order.customerId)">{{ deletedLabel(customer?.fullName, customer?.isDeleted) }}</AppEntityLink></strong><small>{{ customer?.phone }}</small></span></div>
      <div><Bike :size="19" /><span>Phương tiện<strong><AppEntityLink :to="entityDetailRoute('Vehicle', order.vehicleId)">{{ deletedLabel(vehicle?.licensePlate, vehicle?.isDeleted) }}</AppEntityLink></strong><small>ODO {{ formatNumber(order.odometerIn ?? vehicle?.odometer ?? 0) }} km</small></span></div>
      <div><Wrench :size="19" /><span>Yêu cầu<strong>{{ order.customerRequest }}</strong><small>{{ order.diagnosis || 'Chưa có chẩn đoán' }}</small></span></div>
    </section>

    <section v-if="order" class="card condition-images-card">
      <header class="card-header"><div><h2 class="card-title">Ảnh tình trạng xe khi tiếp nhận</h2><span class="section-note">Tối đa {{ maxConditionImages }} ảnh, mỗi ảnh 2 MB</span></div><button v-if="!isEmployee && !isOrderLocked && (order.vehicleConditionImages?.length || 0) < maxConditionImages" class="btn btn-secondary btn-sm" @click="chooseConditionImages"><ImagePlus :size="15" /> Thêm ảnh</button></header>
      <input ref="conditionImageInput" class="visually-hidden" type="file" accept="image/*" capture="environment" multiple @change="readConditionImages" />
      <AppImageGallery v-if="order.vehicleConditionImages?.length" class="condition-image-gallery" :images="order.vehicleConditionImages" alt="Ảnh tình trạng xe" :removable="!isEmployee && !isOrderLocked" @remove="removeConditionImage" />
      <button v-else-if="!isEmployee && !isOrderLocked" type="button" class="empty-image-upload" @click="chooseConditionImages"><ImagePlus :size="24" /><strong>Chụp hoặc chọn ảnh tình trạng xe</strong></button>
      <AppEmpty v-else title="Chưa có ảnh tiếp nhận" message="Phiếu sửa chữa này chưa ghi nhận ảnh tình trạng xe." />
    </section>

    <section class="card">
      <header class="card-header"><div><h2 class="card-title">Hạng mục sửa chữa</h2><span class="section-note">{{ order?.items.length || 0 }} hạng mục dịch vụ/phụ tùng</span></div><button v-if="!isEmployee" class="btn btn-primary btn-sm" :disabled="isOrderLocked" :title="isOrderLocked ? 'Phiếu đã hoàn tất và được khóa' : 'Thêm hạng mục'" @click="openAddItem"><Plus :size="15" /> Thêm hạng mục</button></header>
      <div class="table-wrap">
        <table v-if="order?.items.length" class="data-table">
          <thead><tr><th>Nội dung</th><th>Phân công</th><th>Tiến độ</th><th class="text-right">SL</th><th class="text-right">Đơn giá</th><th class="text-right">Thành tiền</th><th v-if="!isEmployee" class="text-right">Thao tác</th></tr></thead>
          <tbody><tr v-for="item in order.items" :key="item.id"><td><AppEntityLink class="cell-main" :to="item.itemType === 'Part' ? entityDetailRoute('Part', item.partId) : entityDetailRoute('ServiceCategory', item.serviceId)">{{ item.description }}</AppEntityLink><div class="cell-sub">{{ item.itemType === 'Part' ? 'Phụ tùng' : 'Dịch vụ' }}<span v-if="item.inventoryIssued"> · Đã xuất kho<span v-if="item.issuedWarehouseLocationCode"> · Vị trí <AppEntityLink :to="entityDetailRoute('WarehouseLocation', item.issuedWarehouseLocationId)"><strong class="mono issued-location">{{ item.issuedWarehouseLocationCode }}</strong></AppEntityLink></span></span><span v-else-if="item.itemType === 'Part' && parts.find(x => x.id === item.partId)?.warehouseLocationId"> · <AppEntityLink :to="entityDetailRoute('WarehouseLocation', parts.find(x => x.id === item.partId)?.warehouseLocationId)"><span class="mono">{{ warehouseLocations.find(x => x.id === parts.find(p => p.id === item.partId)?.warehouseLocationId)?.code }}</span></AppEntityLink></span></div><NuxtLink v-if="item.itemType === 'Part' && item.partId" class="part-detail-link" :to="`/inventory/${item.partId}`"><ExternalLink :size="13" /> Chi tiết phụ tùng</NuxtLink></td><td><AppEntityLink :to="isEmployee ? undefined : entityDetailRoute('Employee', item.assignedEmployeeId)">{{ employeeName(item.assignedEmployeeId) }}</AppEntityLink></td><td><select v-if="!isEmployee && !isOrderLocked" :value="item.workStatus" class="select work-select" @change="updateWork(item.id, ($event.target as HTMLSelectElement).value)"><option value="Pending">Chờ làm</option><option value="InProgress">Đang làm</option><option value="Completed">Hoàn thành</option><option value="Cancelled">Đã hủy</option></select><AppBadge v-else tone="neutral">{{ statusLabel(item.workStatus) }}</AppBadge></td><td class="text-right">{{ item.itemType === 'Service' ? '—' : formatNumber(item.quantity) }}</td><td class="text-right">{{ formatCurrency(item.unitPrice) }}</td><td class="text-right cell-main">{{ formatCurrency(item.lineTotal) }}</td><td v-if="!isEmployee" class="text-right"><div class="inline item-actions"><button v-if="item.itemType === 'Part' && !item.inventoryIssued" class="btn btn-accent btn-sm" :disabled="isOrderLocked || item.workStatus === 'Cancelled' || issuingItemId === item.id" title="Kiểm tra tồn và xuất phụ tùng" @click="openIssuePart(item)"><PackageCheck :size="14" /> Xuất hàng</button><AppBadge v-else-if="item.itemType === 'Part'" tone="success">Đã xuất</AppBadge><button class="btn btn-secondary btn-sm" :disabled="isOrderLocked || item.inventoryIssued || deletingItemId === item.id" :title="isOrderLocked ? 'Phiếu đã hoàn tất và được khóa' : item.inventoryIssued ? 'Phụ tùng đã xuất kho không thể cập nhật' : 'Cập nhật hạng mục'" @click="openEditItem(item)"><Pencil :size="14" /> Cập nhật</button><button class="icon-btn danger-button" :disabled="isOrderLocked || item.inventoryIssued || deletingItemId === item.id" :title="isOrderLocked ? 'Phiếu đã hoàn tất và được khóa' : item.inventoryIssued ? 'Phụ tùng đã xuất kho không thể xóa' : 'Xóa hạng mục'" @click="removeItem(item)"><Trash2 :size="15" /></button></div></td></tr></tbody>
          <tfoot><tr><td :colspan="isEmployee ? 5 : 6" class="text-right">Tổng dự kiến</td><td class="text-right total-cell">{{ formatCurrency(order.finalTotal) }}</td></tr></tfoot>
        </table>
        <AppEmpty v-else title="Chưa có hạng mục" message="Thêm công việc dịch vụ hoặc phụ tùng cần thay thế." />
      </div>
    </section>

    <section v-if="order" class="detail-columns">
      <article class="card"><header class="card-header"><h2 class="card-title">Thông tin kỹ thuật</h2></header><div class="card-body stack"><div class="info-row"><span>Nhân viên thực hiện</span><strong><AppEntityLink :to="isEmployee ? undefined : entityDetailRoute('Employee', order.serviceAdvisorId)">{{ employeeName(order.serviceAdvisorId) }}</AppEntityLink></strong></div><div class="info-row"><span>Tình trạng ban đầu</span><strong>{{ order.vehicleCondition }}</strong></div><div class="info-row"><span>Chẩn đoán</span><strong>{{ order.diagnosis || 'Chưa cập nhật' }}</strong></div><div class="info-row"><span>Ngày bàn giao</span><strong>{{ formatDate(order.deliveredAt, true) }}</strong></div></div></article>
      <article class="card"><header class="card-header"><h2 class="card-title">Lịch sử trạng thái</h2></header><div class="timeline"><div v-for="entry in [...order.statusHistory].reverse()" :key="entry.changedAt" class="timeline-row"><i /><div><strong>{{ statusLabel(entry.toStatus) }}</strong><span>{{ formatDate(entry.changedAt, true) }}</span><p v-if="entry.note">{{ entry.note }}</p></div></div></div></article>
    </section>

    <AppModal :open="itemModal" :title="editingItemId ? 'Cập nhật hạng mục sửa chữa' : 'Thêm hạng mục sửa chữa'" width="700px" @close="itemModal = false">
      <form id="item-form" class="form-grid" @submit.prevent="saveItem">
        <div class="field"><label>Loại hạng mục *</label><select v-model="itemForm.itemType" class="select" required @change="selectItemType"><option value="" disabled>Chọn hạng mục</option><option value="Service">Dịch vụ</option><option value="Part">Phụ tùng</option></select></div>
        <template v-if="itemForm.itemType === 'Part'">
          <div class="field"><label>Danh mục phụ tùng *</label><AppSearchSelect v-model="itemForm.partCategoryId" :options="partCategoryOptions" placeholder="Chọn danh mục phụ tùng" search-placeholder="Tìm danh mục..." required :clearable="false" @update:model-value="selectPartCategory" /></div>
          <div class="field"><label>Phụ tùng *</label><AppSearchSelect v-model="itemForm.partId" :options="partOptions" :disabled="!itemForm.partCategoryId" placeholder="Chọn phụ tùng" search-placeholder="Tìm phụ tùng..." required :clearable="false" @update:model-value="selectPart" /></div>
        </template>
        <div v-else-if="itemForm.itemType === 'Service'" class="field"><label>Chọn dịch vụ *</label><AppSearchSelect v-model="itemForm.serviceId" :options="serviceOptions" placeholder="Chọn danh mục dịch vụ" search-placeholder="Tìm dịch vụ..." required :clearable="false" @update:model-value="selectService" /></div>
        <div class="field span-2"><label>Mô tả *</label><input v-model.trim="itemForm.description" class="input" required /></div>
        <div v-if="itemForm.itemType === 'Part'" class="field"><label>Số lượng</label><AppNumberInput v-model="itemForm.quantity" class="input" min="0.01" step="0.01" required /></div>
        <div class="field"><label>Đơn giá</label><AppNumberInput v-model="itemForm.unitPrice" class="input" min="0" required /></div>
        <div class="field"><label>Kiểu giảm giá</label><select v-model="itemForm.discountType" class="select"><option value="Amount">Số tiền</option><option value="Percentage">Phần trăm (%)</option></select></div>
        <div class="field"><label>Giá trị giảm</label><AppNumberInput v-model="itemForm.discountValue" class="input" min="0" :max="itemForm.discountType === 'Percentage' ? 100 : undefined" /></div>
        <div class="field"><label>Nhân viên thực hiện</label><AppSearchSelect v-model="itemForm.assignedEmployeeId" :options="employeeOptions" placeholder="Chưa phân công" search-placeholder="Tìm nhân viên..." /></div>
        <div class="field span-2"><label>Ghi chú kỹ thuật</label><textarea v-model="itemForm.technicianNotes" class="textarea" /></div>
      </form>
      <template #footer><button class="btn btn-secondary" @click="itemModal = false">Hủy</button><button class="btn btn-primary" form="item-form" :disabled="saving">{{ saving ? 'Đang lưu...' : editingItemId ? 'Cập nhật hạng mục' : 'Thêm hạng mục' }}</button></template>
    </AppModal>

    <AppModal :open="statusModal" title="Cập nhật trạng thái phiếu" @close="statusModal = false">
      <form id="status-form" class="stack" @submit.prevent="changeStatus"><div class="field"><label>Trạng thái mới</label><select v-model="statusForm.status" class="select"><option v-for="item in statusOptions" :key="item" :value="item" :disabled="item === 'Completed'">{{ item === 'Completed' ? 'Hoàn thành (tự động khi xuất hóa đơn)' : statusLabel(item) }}</option></select></div><div class="field"><label>Ghi chú chuyển trạng thái</label><textarea v-model="statusForm.note" class="textarea" /></div></form>
      <template #footer><button class="btn btn-secondary" @click="statusModal = false">Hủy</button><button class="btn btn-primary" form="status-form" :disabled="saving">Cập nhật</button></template>
    </AppModal>

    <AppModal :open="odometerModal" title="Cập nhật ODO xe" description="Giá trị này sẽ được cập nhật đồng thời vào hồ sơ xe." @close="odometerModal = false">
      <form id="odometer-form" class="stack" @submit.prevent="saveOdometer"><div class="field"><label>Số km hiện tại</label><AppNumberInput v-model="odometerForm.odometerIn" class="input" min="0" required /></div></form>
      <template #footer><button class="btn btn-secondary" @click="odometerModal = false">Hủy</button><button class="btn btn-primary" form="odometer-form" :disabled="saving">Cập nhật ODO</button></template>
    </AppModal>

    <AppModal :open="issueModal" title="Kiểm tra và xuất phụ tùng" width="620px" @close="issueModal = false">
      <div v-if="issueItem && issuePart" class="issue-panel">
        <div class="issue-part-head"><div><span>Phụ tùng</span><strong>{{ issuePart.name }}</strong><small class="mono">{{ issuePart.code }}</small></div><NuxtLink class="btn btn-secondary btn-sm" :to="`/inventory/${issuePart.id}`"><ExternalLink :size="14" /> Xem chi tiết</NuxtLink></div>
        <div class="issue-stats">
          <div><span>Cần xuất</span><strong>{{ formatNumber(issueItem.quantity) }} {{ issuePart.unit }}</strong></div>
          <div><span>Tồn hiện tại</span><strong :class="{ danger: issuePart.quantityOnHand < issueItem.quantity }">{{ formatNumber(issuePart.quantityOnHand) }} {{ issuePart.unit }}</strong></div>
          <div><span>Còn lại sau xuất</span><strong :class="{ danger: issueRemaining < 0 }">{{ formatNumber(issueRemaining) }} {{ issuePart.unit }}</strong></div>
        </div>
        <div class="issue-location"><MapPin :size="19" /><div><span>Vị trí lấy hàng</span><div v-if="issueStockRows.length" class="issue-location-rows"><div v-for="row in issueStockRows" :key="row.location?.id" class="issue-location-option"><label :class="{ disabled: row.quantity < (issueItem?.quantity || 0) }"><input v-model="issueWarehouseLocationId" type="radio" name="issue-location" :value="row.location?.id" :disabled="row.quantity < (issueItem?.quantity || 0)" /><span><strong class="mono">{{ row.location?.code }}</strong><small>{{ formatNumber(row.quantity) }} {{ issuePart.unit }}</small></span></label><AppEntityLink class="issue-location-detail" :to="entityDetailRoute('WarehouseLocation', row.location?.id)" icon>Chi tiết</AppEntityLink></div></div><small v-else>Phụ tùng chưa có tồn tại ngăn kho đang hoạt động</small></div></div>
        <div v-if="!canIssueSelectedPart" class="alert alert-warning"><AlertTriangle :size="18" /><div><strong>Chưa đủ điều kiện xuất kho</strong><span v-if="issuePart.quantityOnHand < issueItem.quantity">Tổng tồn kho không đủ số lượng cần xuất.</span><span v-else-if="!issueStockRows.length">Phụ tùng chưa có tồn tại ngăn kho đang hoạt động.</span><span v-else-if="!issueStockRows.some(x => x.quantity >= (issueItem?.quantity || 0))">Không có một ngăn nào đủ số lượng cần xuất; hãy chuyển hoặc bổ sung tồn kho.</span><span v-else-if="!issueWarehouseLocationId">Vui lòng chọn một vị trí lấy hàng.</span><span v-else>Hạng mục không còn hợp lệ để xuất kho.</span></div></div>
        <div v-else class="issue-note">Khi xác nhận, tồn kho sẽ được trừ ngay. Lúc lập hóa đơn hệ thống nhận biết dòng này đã xuất và không trừ lần hai.</div>
      </div>
      <AppEmpty v-else title="Không tìm thấy phụ tùng" message="Phụ tùng có thể đã bị xóa hoặc không còn trong danh mục." />
      <template #footer><button class="btn btn-secondary" :disabled="!!issuingItemId" @click="issueModal = false">Hủy</button><button class="btn btn-accent" :disabled="!canIssueSelectedPart || !!issuingItemId" @click="confirmIssuePart"><PackageCheck :size="15" /> {{ issuingItemId ? 'Đang xuất...' : 'Xác nhận xuất hàng' }}</button></template>
    </AppModal>

    <AppModal :open="invoiceModal" title="Hoàn tất và xuất hóa đơn" description="Hệ thống sẽ tự trừ tồn phụ tùng, tạo hóa đơn và chuyển phiếu sửa chữa sang Hoàn tất." @close="invoiceModal = false">
      <form id="invoice-form" class="form-grid" @submit.prevent="createInvoice"><div class="field"><label>Kiểu giảm giá hóa đơn</label><select v-model="invoiceForm.discountType" class="select"><option value="Amount">Số tiền</option><option value="Percentage">Phần trăm (%)</option></select></div><div class="field"><label>Giá trị giảm</label><AppNumberInput v-model="invoiceForm.discountValue" class="input" min="0" :max="invoiceForm.discountType === 'Percentage' ? 100 : undefined" /></div><div class="field"><label>Coupon</label><input v-model.trim="invoiceForm.couponCode" class="input mono" placeholder="Nhập mã coupon (tùy chọn)" /></div><div class="field"><label>Thuế suất (%)</label><AppNumberInput v-model="invoiceForm.taxRate" class="input" min="0" max="100" /></div><div class="field span-2"><label>Ghi chú hóa đơn</label><textarea v-model="invoiceForm.notes" class="textarea" /></div></form>
      <template #footer><button class="btn btn-secondary" @click="invoiceModal = false">Hủy</button><button class="btn btn-accent" form="invoice-form" :disabled="saving">Hoàn tất & xuất hóa đơn</button></template>
    </AppModal>
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.locked-notice { display: flex; align-items: flex-start; gap: 10px; padding: 13px 15px; border: 1px solid #a9d9c8; border-radius: 12px; color: #17664f; background: #eefaf5; }
.locked-notice svg { flex: 0 0 auto; margin-top: 1px; }
.locked-notice strong,.locked-notice span { display: block; }
.locked-notice span { margin-top: 2px; color: #477466; font-size: 11px; }
.summary-strip { display: grid; grid-template-columns: .8fr .8fr 1.8fr; gap: 1px; overflow: hidden; border: 1px solid var(--line); border-radius: var(--radius-lg); background: var(--line); box-shadow: var(--shadow); }
.summary-strip > div { display: flex; align-items: flex-start; gap: 12px; padding: 18px; background: white; }
.summary-strip svg { flex: 0 0 auto; color: var(--blue); }
.summary-strip span, .summary-strip strong, .summary-strip small { display: block; }
.summary-strip span { color: var(--muted); font-size: 11px; }
.summary-strip strong { margin-top: 2px; color: var(--navy-950); font-size: 13px; }
.summary-strip small { margin-top: 2px; color: var(--muted); }
.section-note { display: block; margin-top: 2px; color: var(--muted); font-size: 11px; }
.visually-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); }
.condition-image-gallery { padding: 18px; }
.empty-image-upload { display: grid; min-height: 120px; margin: 18px; place-items: center; gap: 7px; border: 1px dashed #9eb2c2; border-radius: 12px; color: var(--navy-800); background: #f7fafc; }
.work-select { width: 135px; min-height: 34px; font-size: 12px; }
.item-actions { justify-content: flex-end; flex-wrap: nowrap; }
.issued-location { color: #805b09; font-weight: 850; }
.part-detail-link { display: inline-flex; align-items: center; gap: 5px; margin-top: 7px; padding: 4px 7px; border: 1px solid var(--line); border-radius: 7px; color: var(--navy-800); background: white; font-size: 10px; font-weight: 750; }.part-detail-link:hover { border-color: #9bb5c7; color: var(--blue); background: #f5f9fb; }
.issue-panel { display: grid; gap: 16px; }.issue-part-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 14px; }.issue-part-head span,.issue-part-head strong,.issue-part-head small { display: block; }.issue-part-head span,.issue-stats span,.issue-location > div > span { color: var(--muted); font-size: 11px; }.issue-part-head strong { margin: 3px 0; color: var(--navy-950); font-size: 16px; }.issue-stats { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 9px; }.issue-stats > div { padding: 13px; border: 1px solid var(--line); border-radius: 10px; background: #f8fafb; }.issue-stats strong { display: block; margin-top: 5px; color: var(--navy-950); }.issue-stats strong.danger { color: var(--red); }.issue-location { display: flex; align-items: flex-start; gap: 10px; padding: 14px; border-radius: 11px; color: #805b09; background: var(--amber-soft); }.issue-location > div { min-width: 0; flex: 1; }.issue-location strong,.issue-location small { display: block; }.issue-location strong { margin: 3px 0; font-size: 14px; }.issue-location small { color: #735d2b; }.issue-location-rows { display: flex; flex-wrap: wrap; gap: 7px; margin-top: 6px; }.issue-location-option { display: flex; align-items: center; gap: 7px; }.issue-location-option > label { display: flex; min-width: 125px; align-items: flex-start; gap: 8px; padding: 7px 9px; border: 1px solid rgb(128 91 9 / 18%); border-radius: 8px; background: rgb(255 255 255 / 48%); cursor: pointer; }.issue-location-option > label:has(input:checked) { border-color: #9a6b07; background: white; box-shadow: 0 0 0 2px rgb(154 107 7 / 14%); }.issue-location-option > label.disabled { cursor: not-allowed; opacity: .52; }.issue-location-rows input { flex: 0 0 auto; margin-top: 5px; accent-color: #956707; }.issue-location-rows label > span { min-width: 0; }.issue-location :deep(.issue-location-detail) { color: #805b09; font-size: 10px; }.issue-note { padding: 11px 13px; border-radius: 9px; color: #17664f; background: #eefaf5; font-size: 11px; }
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
@media (max-width: 560px) { .issue-part-head { flex-direction: column; }.issue-stats { grid-template-columns: 1fr; }.item-actions { flex-wrap: wrap; } }
</style>
