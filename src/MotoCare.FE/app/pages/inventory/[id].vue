<script setup lang="ts">
import { AlertTriangle, ArrowLeft, ArrowRightLeft, Barcode, Boxes, History, MapPin, Package, Pencil, Tags, Truck } from '@lucide/vue'
import type { CashTransaction, InventoryTransaction, PagedResult, Part, PartBrand, PartCategory, Supplier, WarehouseLocation } from '~/types/api'
import { entityDetailRoute } from '~/utils/entityRoute'
import { formatCurrency, formatDate, formatNumber } from '~/utils/format'

const route = useRoute()
const api = useApi()
const auth = useAuth()
const toast = useToast()
const isEmployee = computed(() => auth.hasAnyRole('Employee'))
const part = ref<Part>()
const categories = ref<PartCategory[]>([])
const brands = ref<PartBrand[]>([])
const suppliers = ref<Supplier[]>([])
const warehouseLocations = ref<WarehouseLocation[]>([])
const transactions = ref<InventoryTransaction[]>([])
const vouchers = ref<CashTransaction[]>([])
const loading = ref(true)
const editModalOpen = ref(false)
const transferModal = ref(false)
const receiptHistoryModal = ref(false)
const saving = ref(false)
const transfer = reactive({ fromWarehouseLocationId: '', toWarehouseLocationId: '', quantity: 1, notes: '' })
interface SpecificationValue { code: string, name: string, unit?: string, value: string }
const form = reactive({
  code: '', barcode: '', name: '', partBrandId: '', partCategoryId: '', warehouseLocationId: '', warehouseLocationIds: [] as string[], unit: '',
  specifications: [] as SpecificationValue[], salePrice: 0, minQuantity: 0,
  replacementIntervalKm: null as number | null, replacementIntervalMonths: null as number | null,
  notes: '', isActive: true
})

const partId = computed(() => String(route.params.id))
const categoryName = computed(() => categories.value.find(x => x.id === part.value?.partCategoryId)?.name || '—')
const brandName = computed(() => brands.value.find(x => x.id === part.value?.partBrandId)?.name || 'Chưa chọn hãng')
const supplierRows = computed(() => (part.value?.supplierIds || [])
  .map(id => ({ id, supplier: suppliers.value.find(x => x.id === id) })))
const warehouseStockRows = computed(() => {
  if (!part.value) return []
  const ids = part.value.warehouseLocationIds?.length
    ? part.value.warehouseLocationIds
    : part.value.warehouseLocationId ? [part.value.warehouseLocationId] : []
  return ids.map(locationId => ({
    location: warehouseLocations.value.find(x => x.id === locationId),
    quantity: part.value?.warehouseStocks?.find(x => x.warehouseLocationId === locationId)?.quantityOnHand
      ?? (locationId === part.value?.warehouseLocationId && !part.value?.warehouseStocks?.length ? part.value.quantityOnHand : 0),
    isDefault: locationId === part.value?.warehouseLocationId
  }))
})
const stockAtLocation = (locationId: string) => {
  if (!part.value || !locationId) return 0
  return part.value.warehouseStocks?.find(x => x.warehouseLocationId === locationId)?.quantityOnHand
    ?? (part.value.warehouseLocationId === locationId && !part.value.warehouseStocks?.length ? part.value.quantityOnHand : 0)
}
const transferSourceOptions = computed(() => warehouseStockRows.value
  .filter(row => !!row.location && !row.location.isDeleted && row.quantity > 0)
  .map(row => ({
    code: row.location!.id,
    name: `${row.location!.code} · ${row.location!.name} · Tồn ${formatNumber(row.quantity)} ${part.value?.unit || ''}${row.location!.isActive ? '' : ' (tạm khóa)'}`
  })))
const transferDestinationOptions = computed(() => warehouseLocations.value
  .filter(x => !x.isDeleted && x.isActive && x.id !== transfer.fromWarehouseLocationId)
  .map(x => ({ code: x.id, name: `${x.code} · ${x.name}` })))
const transferSourceLocations = computed(() => warehouseLocations.value.filter(location =>
  transferSourceOptions.value.some(option => option.code === location.id)))
const transferDestinationLocations = computed(() => warehouseLocations.value.filter(location =>
  transferDestinationOptions.value.some(option => option.code === location.id)))
const transferSourceDetails = computed(() => Object.fromEntries(transferSourceLocations.value.map(location => [
  location.id,
  `Tồn ${formatNumber(stockAtLocation(location.id))} ${part.value?.unit || ''}`
])))
const transferSourceQuantity = computed(() => stockAtLocation(transfer.fromWarehouseLocationId))
const receiptTransactions = computed(() => transactions.value.filter(x => x.type === 'Receipt'))
const voucherCode = (id?: string) => vouchers.value.find(x => x.id === id)?.code || id || '—'
const transactionLocations = (transaction: InventoryTransaction) => {
  if (transaction.type === 'Transfer') {
    return [
      { id: transaction.fromWarehouseLocationId, code: transaction.fromWarehouseLocationCode },
      { id: transaction.toWarehouseLocationId, code: transaction.toWarehouseLocationCode }
    ].filter(x => !!x.code)
  }
  if (transaction.locationAllocations?.length) {
    return transaction.locationAllocations.map(x => ({
      id: x.warehouseLocationId,
      code: x.warehouseLocationCode
    }))
  }
  return transaction.warehouseLocationCode
    ? [{ id: transaction.warehouseLocationId, code: transaction.warehouseLocationCode }]
    : []
}
const referenceLabel = (type?: string) => ({
  CashTransaction: 'Phiếu thu chi',
  RepairOrder: 'Phiếu sửa chữa',
  Part: 'Phụ tùng',
  WarehouseStocktake: 'Kiểm kê vị trí',
  WarehouseTransfer: 'Chuyển vị trí',
  OpeningBalance: 'Số dư đầu kỳ',
  Stocktake: 'Kiểm kê kho',
  QualityControl: 'Kiểm soát chất lượng',
  ManualAdjustment: 'Điều chỉnh thủ công'
}[type || ''] || type || '—')
const referenceRoute = (transaction: InventoryTransaction) =>
  transaction.referenceType === 'CashTransaction' && isEmployee.value
    ? undefined
    : transaction.referenceType === 'WarehouseStocktake'
    ? entityDetailRoute('WarehouseLocation', transaction.referenceId)
    : entityDetailRoute(transaction.referenceType, transaction.referenceId)
const referenceCode = (transaction: InventoryTransaction) =>
  transaction.referenceType === 'CashTransaction'
    ? voucherCode(transaction.referenceId)
    : transaction.referenceId
const isLowStock = computed(() => !!part.value && part.value.quantityOnHand < part.value.minQuantity)
const brandOptions = computed(() => brands.value.filter(x => !x.isDeleted).map(x => ({ code: x.id, name: x.name })))
const categoryOptions = computed(() => categories.value.filter(x => !x.isDeleted).map(x => ({ code: x.id, name: x.name })))
const selectedSpecificationDefinitions = computed(() =>
  categories.value.find(x => x.id === form.partCategoryId)?.specificationDefinitions || [])
const unitOptions = [
  'Cái', 'Chiếc', 'Bộ', 'Cặp', 'Hộp', 'Chai', 'Bình', 'Tuýp',
  'Gói', 'Túi', 'Cuộn', 'Mét', 'Lít', 'Thanh', 'Tấm'
].map(name => ({ code: name, name }))
const normalizeSpecificationValue = (dataType: string, value?: string) => {
  if (dataType !== 'Boolean') return value || ''
  return ['true', '1', 'yes', 'có'].includes((value || '').toLowerCase()) ? 'true' : 'false'
}
const setSpecificationValue = (index: number, value: string) => { specificationValue(index).value = value }

const movementLabel = (type: InventoryTransaction['type']) => ({
  Receipt: 'Nhập kho',
  RepairIssue: 'Xuất sửa chữa',
  RepairReturn: 'Hoàn trả sửa chữa',
  AdjustmentIncrease: 'Điều chỉnh tăng',
  AdjustmentDecrease: 'Điều chỉnh giảm',
  Transfer: 'Chuyển vị trí'
}[type] || type)
const isIncrease = (type: InventoryTransaction['type']) =>
  type === 'Receipt' || type === 'RepairReturn' || type === 'AdjustmentIncrease'
const movementTone = (type: InventoryTransaction['type']): 'success' | 'warning' | 'neutral' =>
  type === 'Transfer' ? 'neutral' : isIncrease(type) ? 'success' : 'warning'
const movementPrefix = (type: InventoryTransaction['type']) =>
  type === 'Transfer' ? '↔ ' : isIncrease(type) ? '+' : '−'
const supplierName = (id?: string) => suppliers.value.find(x => x.id === id)?.name || '—'
const specificationValue = (index: number) => form.specifications[index] as SpecificationValue

const openEdit = () => {
  if (!part.value) return
  const definitions = categories.value.find(x => x.id === part.value?.partCategoryId)?.specificationDefinitions || []
  Object.assign(form, {
    code: part.value.code,
    barcode: part.value.barcode || '',
    name: part.value.name,
    partBrandId: part.value.partBrandId || '',
    partCategoryId: part.value.partCategoryId,
    warehouseLocationId: part.value.warehouseLocationId || '',
    warehouseLocationIds: part.value.warehouseLocationIds?.length
      ? [...part.value.warehouseLocationIds]
      : part.value.warehouseLocationId ? [part.value.warehouseLocationId] : [],
    unit: part.value.unit,
    specifications: definitions.map(definition => ({
      code: definition.code,
      name: definition.name,
      unit: definition.unit,
      value: normalizeSpecificationValue(definition.dataType, part.value?.specifications?.find(x => x.code === definition.code)?.value)
    })),
    salePrice: part.value.salePrice,
    minQuantity: part.value.minQuantity,
    replacementIntervalKm: part.value.replacementIntervalKm ?? null,
    replacementIntervalMonths: part.value.replacementIntervalMonths ?? null,
    notes: part.value.notes || '',
    isActive: part.value.isActive
  })
  editModalOpen.value = true
}

const selectCategory = (id: string | null) => {
  form.partCategoryId = id || ''
  const definitions = categories.value.find(x => x.id === form.partCategoryId)?.specificationDefinitions || []
  form.specifications = definitions.map(definition => ({
    code: definition.code,
    name: definition.name,
    unit: definition.unit,
    value: normalizeSpecificationValue(definition.dataType, form.specifications.find(x => x.code === definition.code)?.value)
  }))
}

const savePart = async () => {
  if (!form.name.trim()) {
    toast.error('Chưa nhập tên phụ tùng', 'Vui lòng nhập tên phụ tùng.')
    return
  }
  if (!form.partCategoryId) {
    toast.error('Chưa chọn danh mục', 'Vui lòng chọn danh mục phụ tùng.')
    return
  }
  if (!form.unit) {
    toast.error('Chưa chọn đơn vị', 'Vui lòng chọn đơn vị phụ tùng.')
    return
  }
  const missingSpecification = selectedSpecificationDefinitions.value.find((definition, index) =>
    definition.isRequired && !form.specifications[index]?.value?.trim())
  if (missingSpecification) {
    toast.error('Thiếu thông số kỹ thuật', `Vui lòng nhập “${missingSpecification.name}”.`)
    return
  }

  saving.value = true
  try {
    part.value = await api.request<Part>(`/parts/${partId.value}`, { method: 'PUT', body: form })
    editModalOpen.value = false
    toast.success('Đã cập nhật phụ tùng', form.name)
  } finally {
    saving.value = false
  }
}

const openTransfer = () => {
  if (!part.value) return
  const sourceId = transferSourceOptions.value[0]?.code || ''
  Object.assign(transfer, {
    fromWarehouseLocationId: sourceId,
    toWarehouseLocationId: warehouseLocations.value.find(x =>
      !x.isDeleted && x.isActive && x.id !== sourceId)?.id || '',
    quantity: 1,
    notes: ''
  })
  transferModal.value = true
}
const changeTransferSource = (locationId: string) => {
  transfer.fromWarehouseLocationId = locationId
  if (transfer.toWarehouseLocationId === locationId) {
    transfer.toWarehouseLocationId = transferDestinationOptions.value[0]?.code || ''
  }
}
const saveTransfer = async () => {
  if (!part.value || !transfer.fromWarehouseLocationId || !transfer.toWarehouseLocationId) {
    toast.error('Chưa chọn đủ vị trí', 'Vui lòng chọn ngăn nguồn và ngăn đích.')
    return
  }
  if (transfer.quantity <= 0 || transfer.quantity > transferSourceQuantity.value) {
    toast.error('Số lượng chuyển không hợp lệ', `Ngăn nguồn hiện có ${formatNumber(transferSourceQuantity.value)} ${part.value.unit}.`)
    return
  }
  saving.value = true
  try {
    await api.request('/inventory/transfers', {
      method: 'POST',
      body: { ...transfer, partId: part.value.id }
    })
    const destination = warehouseLocations.value.find(x => x.id === transfer.toWarehouseLocationId)
    toast.success('Đã chuyển phụ tùng', `${formatNumber(transfer.quantity)} ${part.value.unit} sang ${destination?.code || 'ngăn đích'}.`)
    transferModal.value = false
    await load()
  } finally {
    saving.value = false
  }
}

const loadAllTransactions = async () => {
  const firstPage = await api.request<PagedResult<InventoryTransaction>>('/inventory/transactions', {
    query: { partId: partId.value, page: 1, pageSize: 200 }
  })
  const remainingPages = await Promise.all(Array.from(
    { length: Math.max(0, firstPage.totalPages - 1) },
    (_, index) => api.request<PagedResult<InventoryTransaction>>('/inventory/transactions', {
      query: { partId: partId.value, page: index + 2, pageSize: 200 }
    })
  ))
  return [firstPage, ...remainingPages].flatMap(page => page.items)
}

const loadReferencedVouchers = async (items: InventoryTransaction[]) => {
  if (isEmployee.value) return []
  const ids = [...new Set(items
    .filter(item => item.referenceType?.toLowerCase() === 'cashtransaction')
    .map(item => item.referenceId)
    .filter((id): id is string => Boolean(id)))]
  const results = await Promise.allSettled(ids.map(id =>
    api.request<CashTransaction>(`/cash-transactions/${id}`, { query: { includeDeleted: true } })))
  return results.flatMap(result => result.status === 'fulfilled' ? [result.value] : [])
}

const load = async () => {
  loading.value = true
  try {
    part.value = await api.request<Part>(`/parts/${partId.value}`, { query: { includeDeleted: true } })
    const optional = async <T>(request: Promise<T>, fallback: T) => {
      try { return await request } catch { return fallback }
    }
    const [categoryPage, brandPage, supplierPage, locationPage, currentTransactions] = await Promise.all([
      optional(api.request<PagedResult<PartCategory>>('/part-categories?pageSize=200&includeDeleted=true'), { items: [], total: 0, page: 1, pageSize: 200, totalPages: 0 }),
      optional(api.request<PagedResult<PartBrand>>('/part-brands?pageSize=200&includeDeleted=true'), { items: [], total: 0, page: 1, pageSize: 200, totalPages: 0 }),
      optional(api.request<PagedResult<Supplier>>('/suppliers?pageSize=200&includeDeleted=true'), { items: [], total: 0, page: 1, pageSize: 200, totalPages: 0 }),
      optional(api.request<PagedResult<WarehouseLocation>>('/warehouse-locations?pageSize=500&includeDeleted=true'), { items: [], total: 0, page: 1, pageSize: 200, totalPages: 0 }),
      optional(loadAllTransactions(), [])
    ])
    categories.value = categoryPage.items
    brands.value = brandPage.items
    suppliers.value = supplierPage.items
    warehouseLocations.value = locationPage.items
    transactions.value = currentTransactions
    vouchers.value = await loadReferencedVouchers(currentTransactions)
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <NuxtLink to="/inventory" class="back-link"><ArrowLeft :size="16" /> Kho phụ tùng</NuxtLink>

    <template v-if="part">
      <div class="page-header">
        <div>
          <div class="inline">
            <h1 class="page-title">{{ part.name }}</h1>
            <AppBadge :tone="part.isDeleted || !part.isActive ? 'neutral' : 'success'">
              {{ part.isDeleted ? 'Đã xóa' : part.isActive ? 'Đang kinh doanh' : 'Tạm ngừng' }}
            </AppBadge>
          </div>
          <p class="page-subtitle mono">{{ part.code }}<span v-if="part.barcode"> · {{ part.barcode }}</span></p>
        </div>
        <div class="page-actions">
          <button class="btn btn-secondary" @click="receiptHistoryModal = true"><History :size="17" /> Lịch sử nhập hàng</button>
          <button v-if="!isEmployee && !part.isDeleted" class="btn btn-secondary" :disabled="part.quantityOnHand <= 0" @click="openTransfer"><ArrowRightLeft :size="17" /> Chuyển giữa các ngăn</button>
          <button v-if="!isEmployee && !part.isDeleted" class="btn btn-primary" @click="openEdit"><Pencil :size="17" /> Cập nhật phụ tùng</button>
        </div>
      </div>

      <div v-if="isLowStock" class="alert alert-warning">
        <AlertTriangle :size="19" />
        <div><strong>Tồn kho dưới định mức</strong><div>Còn {{ formatNumber(part.quantityOnHand) }} {{ part.unit }}, định mức tối thiểu {{ formatNumber(part.minQuantity) }} {{ part.unit }}.</div></div>
      </div>

      <section class="stock-grid">
        <article class="stock-card"><span>Tồn hiện tại</span><strong :class="{ danger: isLowStock }">{{ formatNumber(part.quantityOnHand) }}</strong><small>{{ part.unit }}</small></article>
        <article class="stock-card"><span>Định mức tối thiểu</span><strong>{{ formatNumber(part.minQuantity) }}</strong><small>{{ part.unit }}</small></article>
        <article class="stock-card"><span>Giá nhập gần nhất</span><strong>{{ part.importPrice ? formatCurrency(part.importPrice) : '—' }}</strong></article>
        <article class="stock-card"><span>Giá bán</span><strong>{{ formatCurrency(part.salePrice) }}</strong></article>
      </section>

      <section class="detail-layout">
        <article class="card">
          <header class="card-header"><h2 class="card-title"><Package :size="19" /> Thông tin phụ tùng</h2></header>
          <div class="card-body detail-grid">
            <div><span><Tags :size="14" /> Danh mục</span><strong><AppEntityLink :to="entityDetailRoute('PartCategory', part.partCategoryId)">{{ categoryName }}</AppEntityLink></strong></div>
            <div><span><Boxes :size="14" /> Hãng phụ tùng</span><strong><AppEntityLink :to="entityDetailRoute('PartBrand', part.partBrandId)">{{ brandName }}</AppEntityLink></strong></div>
            <div><span><Barcode :size="14" /> Barcode</span><strong class="mono">{{ part.barcode || '—' }}</strong></div>
            <div><span>Đơn vị tính</span><strong>{{ part.unit }}</strong></div>
            <div><span><Truck :size="14" /> Nhà cung cấp đã nhập</span><div v-if="supplierRows.length" class="entity-list"><AppEntityLink v-for="row in supplierRows" :key="row.id" :to="entityDetailRoute('Supplier', row.id)">{{ row.supplier?.name || 'Nhà cung cấp cũ' }}</AppEntityLink></div><strong v-else>Chưa nhập từ nhà cung cấp</strong></div>
            <div class="span-2"><span><MapPin :size="14" /> Tồn theo vị trí</span><div v-if="warehouseStockRows.length" class="warehouse-stock-list"><div v-for="row in warehouseStockRows" :key="row.location?.id"><NuxtLink v-if="row.location" class="warehouse-location-link" :to="`/warehouse-locations/${row.location.id}`" title="Xem chi tiết vị trí"><span class="location-badge mono">{{ row.location.code }}</span><b>{{ row.location.name }}</b></NuxtLink><span v-else class="location-badge mono">Vị trí cũ</span><small v-if="row.isDefault">Mặc định nhập</small><strong>{{ formatNumber(row.quantity) }} {{ part.unit }}</strong></div></div><strong v-else>Chưa xếp vị trí</strong></div>
            <div><span>Chu kỳ theo quãng đường</span><strong>{{ part.replacementIntervalKm ? `${formatNumber(part.replacementIntervalKm)} km` : 'Không thiết lập' }}</strong></div>
            <div><span>Chu kỳ theo thời gian</span><strong>{{ part.replacementIntervalMonths ? `${formatNumber(part.replacementIntervalMonths)} tháng` : 'Không thiết lập' }}</strong></div>
            <div class="span-2"><span>Ghi chú</span><strong>{{ part.notes || '—' }}</strong></div>
          </div>
        </article>

        <article class="card">
          <header class="card-header"><h2 class="card-title">Thông số kỹ thuật</h2><span class="muted">{{ part.specifications?.length || 0 }} thông số</span></header>
          <div v-if="part.specifications?.length" class="spec-list">
            <div v-for="specification in part.specifications" :key="specification.code">
              <span>{{ specification.name }}</span>
              <strong>{{ specification.value === 'true' ? 'Có' : specification.value === 'false' ? 'Không' : specification.value }}<small v-if="specification.unit"> {{ specification.unit }}</small></strong>
            </div>
          </div>
          <AppEmpty v-else title="Chưa có thông số kỹ thuật" message="Phụ tùng này chưa được khai báo thông số theo danh mục." />
        </article>
      </section>

      <section class="card">
        <header class="card-header"><div><h2 class="card-title">Lịch sử nhập – xuất kho</h2><span class="section-note">Toàn bộ biến động tồn kho của phụ tùng</span></div><span class="muted">{{ formatNumber(transactions.length) }} giao dịch</span></header>
        <div class="table-wrap">
          <table v-if="transactions.length" class="data-table">
            <thead><tr><th>Thời gian</th><th>Loại giao dịch</th><th>Vị trí</th><th>Nhà cung cấp</th><th>Tham chiếu</th><th>Ghi chú</th><th class="text-right">Số lượng</th><th class="text-right">Đơn giá</th></tr></thead>
            <tbody>
              <tr v-for="transaction in transactions" :key="transaction.id">
                <td>{{ formatDate(transaction.transactionDate, true) }}</td>
                <td><AppBadge :tone="movementTone(transaction.type)">{{ movementLabel(transaction.type) }}</AppBadge></td>
                <td><div v-if="transactionLocations(transaction).length" class="transaction-locations"><template v-for="(location, index) in transactionLocations(transaction)" :key="`${location.id || location.code}-${index}`"><span v-if="index" class="muted">{{ transaction.type === 'Transfer' ? '→' : '·' }}</span><AppEntityLink :to="entityDetailRoute('WarehouseLocation', location.id)"><span class="mono">{{ location.code }}</span></AppEntityLink></template></div><span v-else>—</span></td>
                <td><AppEntityLink :to="entityDetailRoute('Supplier', transaction.supplierId)">{{ supplierName(transaction.supplierId) }}</AppEntityLink></td>
                <td><div>{{ referenceLabel(transaction.referenceType) }}</div><AppEntityLink v-if="transaction.referenceId" class="cell-sub mono" :to="referenceRoute(transaction)">{{ referenceCode(transaction) }}</AppEntityLink></td>
                <td>{{ transaction.notes || '—' }}</td>
                <td class="text-right movement" :class="transaction.type === 'Transfer' ? '' : isIncrease(transaction.type) ? 'increase' : 'decrease'">{{ movementPrefix(transaction.type) }}{{ formatNumber(transaction.quantity) }}</td>
                <td class="text-right">{{ transaction.unitCost ? formatCurrency(transaction.unitCost) : '—' }}</td>
              </tr>
            </tbody>
          </table>
          <AppEmpty v-else title="Chưa có biến động kho" message="Phụ tùng này chưa phát sinh giao dịch nhập hoặc xuất kho." />
        </div>
      </section>
    </template>

    <div v-else-if="loading" class="loading-skeleton" style="height: 420px" />

    <AppModal :open="transferModal" :title="`Chuyển vị trí: ${part?.name || ''}`" width="680px" @close="transferModal = false">
      <form id="part-transfer-form" class="form-grid" @submit.prevent="saveTransfer">
        <div class="transfer-note span-2"><ArrowRightLeft :size="18" /><div><strong>Lệnh chuyển phụ tùng</strong><span>Chỉ thay đổi số lượng giữa các ngăn; tổng tồn kho của phụ tùng không đổi.</span></div></div>
        <div class="field"><label>Ngăn nguồn *</label><WarehouseLocationSinglePicker :model-value="transfer.fromWarehouseLocationId" :locations="transferSourceLocations" :location-details="transferSourceDetails" title="Chọn ngăn nguồn" description="Sơ đồ chỉ hiển thị các ngăn đang có phụ tùng này. Số tồn hiện tại được ghi trên từng ngăn." placeholder="Chưa chọn ngăn nguồn" action-label="Chọn trên sơ đồ" @update:model-value="changeTransferSource" /><small class="muted">Khả dụng: {{ formatNumber(transferSourceQuantity) }} {{ part?.unit }}</small></div>
        <div class="field"><label>Ngăn đích *</label><WarehouseLocationSinglePicker v-model="transfer.toWarehouseLocationId" :locations="transferDestinationLocations" title="Chọn ngăn đích" description="Chọn ngăn sẽ nhận phụ tùng. Ngăn nguồn đã được loại khỏi sơ đồ." placeholder="Chưa chọn ngăn đích" action-label="Chọn trên sơ đồ" /></div>
        <div class="field"><label>Số lượng chuyển *</label><AppNumberInput v-model="transfer.quantity" class="input" min="0.01" :max="transferSourceQuantity" step="0.01" required /></div>
        <div class="field transfer-preview"><span>Sau khi chuyển</span><strong>{{ formatNumber(transferSourceQuantity - Number(transfer.quantity || 0)) }} {{ part?.unit }}</strong><small>còn lại tại ngăn nguồn</small></div>
        <div class="field span-2"><label>Lý do chuyển *</label><textarea v-model.trim="transfer.notes" class="textarea" required placeholder="Ví dụ: Sắp xếp lại kho, chuyển sang ngăn dễ lấy hơn..." /></div>
      </form>
      <template #footer><button class="btn btn-secondary" @click="transferModal = false">Hủy</button><button class="btn btn-primary" form="part-transfer-form" :disabled="saving || !transferDestinationOptions.length">Tạo lệnh chuyển</button></template>
    </AppModal>

    <AppModal :open="receiptHistoryModal" :title="`Lịch sử nhập hàng: ${part?.name || ''}`" width="940px" @close="receiptHistoryModal = false">
      <div class="table-wrap">
        <table v-if="receiptTransactions.length" class="data-table">
          <thead><tr><th>Thời gian</th><th>Mã giao dịch</th><th>Phiếu nhập</th><th>Nhà cung cấp</th><th>Vị trí nhập</th><th class="text-right">Số lượng</th><th class="text-right">Giá nhập</th><th class="text-right">Thành tiền</th></tr></thead>
          <tbody><tr v-for="transaction in receiptTransactions" :key="transaction.id"><td>{{ formatDate(transaction.transactionDate, true) }}</td><td class="mono">{{ transaction.code }}</td><td><AppEntityLink class="mono" :to="isEmployee ? undefined : entityDetailRoute('CashTransaction', transaction.referenceId)">{{ voucherCode(transaction.referenceId) }}</AppEntityLink></td><td><AppEntityLink :to="entityDetailRoute('Supplier', transaction.supplierId)">{{ supplierName(transaction.supplierId) }}</AppEntityLink></td><td><div v-if="transactionLocations(transaction).length" class="transaction-locations"><template v-for="(location, index) in transactionLocations(transaction)" :key="`${location.id || location.code}-${index}`"><span v-if="index" class="muted">·</span><AppEntityLink :to="entityDetailRoute('WarehouseLocation', location.id)"><span class="mono">{{ location.code }}</span></AppEntityLink></template></div><span v-else>—</span></td><td class="text-right">{{ formatNumber(transaction.quantity) }} {{ part?.unit }}</td><td class="text-right">{{ formatCurrency(transaction.unitCost) }}</td><td class="text-right cell-main">{{ formatCurrency(transaction.quantity * transaction.unitCost) }}</td></tr></tbody>
        </table>
        <AppEmpty v-else title="Chưa có lịch sử nhập hàng" message="Phụ tùng này chưa được nhập bằng phiếu chi hoặc giao dịch nhập kho." />
      </div>
      <template #footer><button class="btn btn-primary" @click="receiptHistoryModal = false">Đóng</button></template>
    </AppModal>

    <AppModal :open="editModalOpen" title="Cập nhật phụ tùng" width="760px" @close="editModalOpen = false">
      <form id="part-detail-form" class="form-grid" @submit.prevent="savePart">
        <div class="field"><label>Mã phụ tùng</label><input v-model.trim="form.code" class="input" /></div>
        <div class="field"><label>Barcode</label><input v-model.trim="form.barcode" class="input" /></div>
        <div class="field span-2"><label>Tên phụ tùng *</label><input v-model.trim="form.name" class="input" required /></div>
        <div class="field"><label>Danh mục *</label><AppSearchSelect :model-value="form.partCategoryId" :options="categoryOptions" required :clearable="false" placeholder="Chọn danh mục" @update:model-value="selectCategory" /></div>
        <div class="field"><label>Hãng phụ tùng</label><AppSearchSelect v-model="form.partBrandId" :options="brandOptions" :clearable="true" placeholder="Chọn hãng" search-placeholder="Tìm hãng..." /></div>
        <div class="field span-2"><label>Các vị trí trong kho</label><WarehouseLocationPicker v-model="form.warehouseLocationIds" v-model:default-location-id="form.warehouseLocationId" :locations="warehouseLocations" /><small class="muted">Có thể chọn nhiều ngăn. Ngăn mặc định sẽ nhận hàng khi nhập kho.</small></div>
        <div class="field"><label>Đơn vị *</label><AppSearchSelect v-model="form.unit" :options="unitOptions" required :clearable="false" placeholder="Chọn đơn vị" search-placeholder="Tìm đơn vị..." /></div>
        <div class="field"><label>Giá bán</label><AppNumberInput v-model="form.salePrice" class="input" min="0" /></div>
        <div class="field"><label>Số lượng cảnh báo (min)</label><AppNumberInput v-model="form.minQuantity" class="input" min="0" /></div>
        <div class="field"><label>Thay sau số km</label><AppNumberInput v-model="form.replacementIntervalKm" class="input" min="1" placeholder="Ví dụ: 12000" /></div>
        <div class="field"><label>Thay sau số tháng</label><AppNumberInput v-model="form.replacementIntervalMonths" class="input" min="1" placeholder="Ví dụ: 24" /></div>
        <div class="info-note span-2">Có thể khai báo một hoặc cả hai chu kỳ. Hệ thống sẽ nhắc theo điều kiện đến trước kể từ lần lắp gần nhất.</div>
        <template v-if="selectedSpecificationDefinitions.length">
          <div class="form-section span-2">Thông số kỹ thuật</div>
          <div v-for="(definition, index) in selectedSpecificationDefinitions" :key="definition.code" class="field">
            <label>{{ definition.name }}<span v-if="definition.unit"> ({{ definition.unit }})</span>{{ definition.isRequired ? ' *' : '' }}</label>
            <select v-if="definition.dataType === 'Selection'" v-model="specificationValue(index).value" class="select" :required="definition.isRequired"><option value="">Chọn {{ definition.name.toLowerCase() }}</option><option v-for="option in definition.options" :key="option" :value="option">{{ option }}</option></select>
            <label v-else-if="definition.dataType === 'Boolean'" class="boolean-spec-input"><input type="checkbox" :checked="specificationValue(index).value === 'true'" @change="setSpecificationValue(index, ($event.target as HTMLInputElement).checked ? 'true' : 'false')" /><span>{{ specificationValue(index).value === 'true' ? 'Có' : 'Không' }}</span></label>
            <input v-else-if="definition.dataType === 'Number'" type="number" step="any" class="input" :required="definition.isRequired" :value="specificationValue(index).value" @input="setSpecificationValue(index, ($event.target as HTMLInputElement).value)" />
            <input v-else v-model.trim="specificationValue(index).value" class="input" :required="definition.isRequired" />
          </div>
        </template>
        <div class="field span-2"><label>Ghi chú</label><textarea v-model.trim="form.notes" class="textarea" maxlength="2000" /></div>
        <label class="check-row span-2"><input v-model="form.isActive" type="checkbox" /> Phụ tùng đang kinh doanh</label>
      </form>
      <template #footer>
        <button class="btn btn-secondary" :disabled="saving" @click="editModalOpen = false">Hủy</button>
        <button class="btn btn-primary" form="part-detail-form" :disabled="saving">{{ saving ? 'Đang lưu...' : 'Lưu thay đổi' }}</button>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.stock-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; }
.stock-card { padding: 17px 19px; border: 1px solid var(--line); border-radius: var(--radius-lg); background: white; box-shadow: var(--shadow-sm); }
.stock-card span,.stock-card strong,.stock-card small { display: block; }
.stock-card span { color: var(--muted); font-size: 11px; }
.info-note { padding: 12px 14px; border-radius: 10px; color: #805b09; background: var(--amber-soft); font-size: 12px; }
.transfer-note { display: flex; align-items: center; gap: 10px; padding: 12px 14px; border-radius: 10px; color: var(--navy-900); background: #eef5f8; }.transfer-note strong,.transfer-note span { display: block; }.transfer-note span { margin-top: 2px; color: var(--muted); font-size: 11px; }.transfer-preview { justify-content: center; padding: 9px 12px; border-radius: 9px; background: #f6f8fa; }.transfer-preview span,.transfer-preview strong,.transfer-preview small { display: block; }.transfer-preview span,.transfer-preview small { color: var(--muted); font-size: 10px; }.transfer-preview strong { margin: 2px 0; color: var(--navy-900); }
.boolean-spec-input { display: flex; min-height: 40px; align-items: center; gap: 9px; padding: 0 12px; border: 1px solid var(--line); border-radius: 9px; background: white; }
.stock-card strong { margin-top: 5px; color: var(--navy-950); font-size: 21px; }
.stock-card small { margin-top: 2px; color: var(--muted); }
.stock-card strong.danger { color: var(--red); }
.detail-layout { display: grid; grid-template-columns: minmax(0, 1.35fr) minmax(300px, .65fr); gap: 18px; }
.card-title { display: flex; align-items: center; gap: 8px; }
.detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }
.detail-grid span,.detail-grid strong { display: block; }
.detail-grid span { display: flex; align-items: center; gap: 5px; color: var(--muted); font-size: 11px; }
.detail-grid strong { margin-top: 4px; color: var(--navy-950); }
.entity-list,.transaction-locations { display: flex; flex-wrap: wrap; align-items: center; gap: 5px 8px; margin-top: 4px; }
.entity-list { font-weight: 750; }
.transaction-locations { margin-top: 0; }
.detail-grid strong small { display: block; margin-top: 4px; color: var(--muted); font-weight: 500; }.location-badge { display: inline-flex !important; padding: 4px 8px; border-radius: 7px; color: #805b09 !important; background: var(--amber-soft); font-size: 12px !important; }.warehouse-stock-list { display: grid; grid-template-columns: repeat(auto-fit, minmax(145px, 1fr)); gap: 7px; margin-top: 7px; }.warehouse-stock-list > div { position: relative; padding: 9px; border: 1px solid var(--line); border-radius: 9px; background: #f8fafb; }.warehouse-location-link { display: grid; width: max-content; max-width: 100%; gap: 4px; }.warehouse-location-link b { overflow: hidden; color: var(--navy-900); font-size: 10px; text-overflow: ellipsis; white-space: nowrap; }.warehouse-location-link:hover b { color: var(--teal); text-decoration: underline; }.warehouse-stock-list small { display: block; margin-top: 4px; color: var(--teal); font-size: 9px; }.warehouse-stock-list strong { display: block; margin-top: 5px; font-size: 11px; }
.span-2 { grid-column: span 2; }
.spec-list { display: grid; gap: 0; padding: 8px 18px 18px; }
.spec-list > div { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 12px 0; border-bottom: 1px solid var(--line); }
.spec-list > div:last-child { border-bottom: 0; }
.spec-list span { color: var(--muted); font-size: 11px; }
.spec-list strong { color: var(--navy-950); text-align: right; }
.movement { font-weight: 800; }
.movement.increase { color: var(--teal); }
.movement.decrease { color: var(--red); }
.form-section { padding-bottom: 7px; border-bottom: 1px solid var(--line); color: var(--navy-950); font-size: 12px; font-weight: 800; }
.check-row { display: flex; align-items: center; gap: 9px; color: var(--navy-900); font-weight: 700; }
@media (max-width: 1000px) { .stock-grid { grid-template-columns: repeat(2, 1fr); }.detail-layout { grid-template-columns: 1fr; } }
@media (max-width: 560px) { .stock-grid,.detail-grid { grid-template-columns: 1fr; }.span-2 { grid-column: auto; } }
</style>
