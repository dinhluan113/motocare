<script setup lang="ts">
import { AlertTriangle, Edit3, History, Package, Plus, Search, SlidersHorizontal, Trash2 } from '@lucide/vue'
import type { CashTransaction, InventoryTransaction, PagedResult, Part, PartBrand, PartCategory, Supplier } from '~/types/api'
import { formatCurrency, formatDate, formatNumber } from '~/utils/format'
interface SpecificationValue { code: string, name: string, unit?: string, value: string }

const api = useApi()
const auth = useAuth()
const toast = useToast()
const isEmployee = computed(() => auth.hasAnyRole('Employee'))
const result = ref<PagedResult<Part>>({ items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 })
const brands = ref<PartBrand[]>([])
const categories = ref<PartCategory[]>([])
const suppliers = ref<Supplier[]>([])
const vouchers = ref<CashTransaction[]>([])
const historyItems = ref<InventoryTransaction[]>([])
const search = ref('')
const categoryId = ref('')
const supplierId = ref('')
const loading = ref(true)
const saving = ref(false)
const partModal = ref(false)
const partFormTab = ref<'general' | 'specifications'>('general')
const adjustmentModal = ref(false)
const historyModal = ref(false)
const editing = ref<Part>()
const selectedPart = ref<Part>()
const form = reactive({
  code: '', barcode: '', name: '', partBrandId: '', partCategoryId: '', unit: '',
  specifications: [] as SpecificationValue[], salePrice: 0, minQuantity: 0,
  replacementIntervalKm: null as number | null, replacementIntervalMonths: null as number | null,
  notes: '', isActive: true
})
const adjustment = reactive({ type: 'AdjustmentIncrease', quantity: 1, notes: '' })
const brandOptions = computed(() => brands.value.filter(x => !x.isDeleted).map(x => ({ code: x.id, name: x.name })))
const categoryOptions = computed(() => [{ code: '', name: 'Tất cả danh mục' }, ...categories.value.filter(x => !x.isDeleted).map(x => ({ code: x.id, name: x.name }))])
const supplierOptions = computed(() => [{ code: '', name: 'Tất cả nhà cung cấp' }, ...suppliers.value.filter(x => !x.isDeleted).map(x => ({ code: x.id, name: x.name }))])
const selectedSpecificationDefinitions = computed(() => categories.value.find(x => x.id === form.partCategoryId)?.specificationDefinitions || [])
const unitOptions = [
  'Cái', 'Chiếc', 'Bộ', 'Cặp', 'Hộp', 'Chai', 'Bình', 'Tuýp',
  'Gói', 'Túi', 'Cuộn', 'Mét', 'Lít', 'Thanh', 'Tấm'
].map(name => ({ code: name, name }))
const normalizeSpecificationValue = (dataType: string, value?: string) => {
  if (dataType !== 'Boolean') return value || ''
  return ['true', '1', 'yes', 'có'].includes((value || '').toLowerCase()) ? 'true' : 'false'
}
const setSpecificationValue = (index: number, value: string) => { specificationValue(index).value = value }

const load = async (page = 1) => {
  loading.value = true
  try {
    const [partsPage, brandPage, categoryPage, supplierPage, voucherPage] = await Promise.all([
      api.request<PagedResult<Part>>('/parts', { query: { search: search.value || undefined, categoryId: categoryId.value || undefined, supplierId: supplierId.value || undefined, page, pageSize: 20 } }),
      api.request<PagedResult<PartBrand>>('/part-brands?pageSize=200&includeDeleted=true'),
      api.request<PagedResult<PartCategory>>('/part-categories?pageSize=200&includeDeleted=true'),
      api.request<PagedResult<Supplier>>('/suppliers?pageSize=200&includeDeleted=true'),
      isEmployee.value
        ? Promise.resolve({ items: [], total: 0, page: 1, pageSize: 200, totalPages: 0 } as PagedResult<CashTransaction>)
        : api.request<PagedResult<CashTransaction>>('/cash-transactions?pageSize=200')
    ])
    result.value = partsPage
    brands.value = brandPage.items.map(x => ({ ...x, name: `${x.name}${x.isDeleted ? ' (đã xóa)' : ''}` }))
    categories.value = categoryPage.items.map(x => ({ ...x, name: `${x.name}${x.isDeleted ? ' (đã xóa)' : ''}` }))
    suppliers.value = supplierPage.items.map(x => ({ ...x, name: `${x.name}${x.isDeleted ? ' (đã xóa)' : ''}` }))
    vouchers.value = voucherPage.items
  } finally { loading.value = false }
}
let timer: ReturnType<typeof setTimeout>
watch(search, () => { clearTimeout(timer); timer = setTimeout(() => load(), 350) })
watch([categoryId, supplierId], () => load())

const openPart = (part?: Part) => {
  editing.value = part
  partFormTab.value = 'general'
  const targetCategoryId = part?.partCategoryId || ''
  const definitions = categories.value.find(x => x.id === targetCategoryId)?.specificationDefinitions || []
  Object.assign(form, {
    code: part?.code || '', barcode: part?.barcode || '', name: part?.name || '',
    partBrandId: part?.partBrandId || '', partCategoryId: targetCategoryId,
    specifications: definitions.map(definition => ({
      code: definition.code, name: definition.name, unit: definition.unit,
      value: normalizeSpecificationValue(definition.dataType, part?.specifications?.find(x => x.code === definition.code)?.value)
    })),
    unit: part?.unit || '', salePrice: part?.salePrice || 0, minQuantity: part?.minQuantity || 0,
    replacementIntervalKm: part?.replacementIntervalKm ?? null,
    replacementIntervalMonths: part?.replacementIntervalMonths ?? null,
    notes: part?.notes || '', isActive: part?.isActive ?? true
  })
  partModal.value = true
}
const selectCategory = (id: string) => {
  form.partCategoryId = id
  const definitions = categories.value.find(x => x.id === id)?.specificationDefinitions || []
  form.specifications = definitions.map(definition => ({
    code: definition.code, name: definition.name, unit: definition.unit,
    value: normalizeSpecificationValue(definition.dataType, form.specifications.find(x => x.code === definition.code)?.value)
  }))
}
const savePart = async () => {
  if (!form.name.trim()) {
    partFormTab.value = 'general'
    toast.error('Chưa nhập tên phụ tùng', 'Vui lòng nhập tên phụ tùng.')
    return
  }
  if (!form.partCategoryId) {
    partFormTab.value = 'general'
    toast.error('Chưa chọn danh mục', 'Vui lòng chọn danh mục phụ tùng.')
    return
  }
  if (!form.unit) {
    partFormTab.value = 'general'
    toast.error('Chưa chọn đơn vị', 'Vui lòng chọn đơn vị phụ tùng.')
    return
  }
  const missingSpecification = selectedSpecificationDefinitions.value.find((definition, index) =>
    definition.isRequired && !form.specifications[index]?.value?.trim())
  if (missingSpecification) {
    partFormTab.value = 'specifications'
    toast.error('Thiếu thông số kỹ thuật', `Vui lòng nhập “${missingSpecification.name}”.`)
    return
  }
  saving.value = true
  try {
    await api.request(`/parts${editing.value ? `/${editing.value.id}` : ''}`, { method: editing.value ? 'PUT' : 'POST', body: form })
    toast.success('Đã lưu phụ tùng', form.name); partModal.value = false; await load(result.value.page)
  } finally { saving.value = false }
}
const removePart = async (part: Part) => {
  if (!confirm(`Xóa phụ tùng ${part.name}? Dữ liệu tham chiếu cũ vẫn được giữ lại.`)) return
  await api.request(`/parts/${part.id}`, { method: 'DELETE' })
  toast.success('Đã xóa phụ tùng', part.name)
  await load(result.value.page)
}
const openAdjustment = (part: Part) => {
  selectedPart.value = part
  Object.assign(adjustment, { type: 'AdjustmentIncrease', quantity: 1, notes: '' })
  adjustmentModal.value = true
}
const saveAdjustment = async () => {
  saving.value = true
  try {
    await api.request('/inventory/movements', { method: 'POST', body: { ...adjustment, partId: selectedPart.value?.id, unitCost: 0, referenceType: 'ManualAdjustment', referenceId: null } })
    toast.success('Đã điều chỉnh tồn kho', selectedPart.value?.name); adjustmentModal.value = false; await load(result.value.page)
  } finally { saving.value = false }
}
const openHistory = async (part: Part) => {
  selectedPart.value = part
  const page = await api.request<PagedResult<InventoryTransaction>>('/inventory/transactions', { query: { partId: part.id, pageSize: 200 } })
  historyItems.value = page.items.filter(x => x.type === 'Receipt')
  historyModal.value = true
}
const referenceName = (items: Array<{ id: string, name: string, isDeleted: boolean }>, id?: string, fallback = '—') => {
  const item = items.find(x => x.id === id)
  return item?.name || fallback
}
const supplierName = (id?: string) => referenceName(suppliers.value, id)
const voucherCode = (id?: string) => vouchers.value.find(x => x.id === id)?.code || '—'
const specificationValue = (index: number) => form.specifications[index] as SpecificationValue
onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-header">
      <div><div class="breadcrumb">Nhà cung cấp <span>›</span> Danh mục <span>›</span> Phụ tùng</div><h1 class="page-title">Kho phụ tùng</h1><p class="page-subtitle">Khai báo phụ tùng tại đây; mọi giá nhập và tăng tồn từ mua hàng được ghi nhận qua phiếu chi.</p></div>
      <button v-if="!isEmployee" class="btn btn-accent" @click="openPart()"><Plus :size="17" /> Thêm phụ tùng</button>
    </div>
    <div v-if="result.items.some(x => x.quantityOnHand < x.minQuantity)" class="alert alert-warning"><AlertTriangle :size="19" /><div><strong>Có phụ tùng dưới định mức</strong><div>Các dòng màu đỏ cần được bổ sung bằng phiếu chi nhập hàng.</div></div></div>
    <section class="card">
      <header class="card-header filter-head">
        <div class="search-box"><Search :size="17" /><input v-model="search" class="input" placeholder="Tìm tên, mã hoặc thông số (VD: P205/55)..." /></div>
        <AppSearchSelect v-model="categoryId" :options="categoryOptions" :clearable="false" placeholder="Tất cả danh mục" />
        <AppSearchSelect v-model="supplierId" :options="supplierOptions" :clearable="false" placeholder="Tất cả nhà cung cấp" />
        <span class="muted">{{ formatNumber(result.total) }} phụ tùng</span>
      </header>
      <div class="table-wrap">
        <table v-if="result.items.length" class="data-table">
          <thead><tr><th>Phụ tùng</th><th>Danh mục / hãng</th><th>Nhà cung cấp đã nhập</th><th class="text-right">Giá nhập gần nhất</th><th class="text-right">Giá bán</th><th class="text-right">Tồn / Min</th><th class="text-right">Thao tác</th></tr></thead>
          <tbody><tr v-for="part in result.items" :key="part.id" :class="{ 'low-row': part.quantityOnHand < part.minQuantity }"><td><NuxtLink class="part-link" :to="`/inventory/${part.id}`"><span class="cell-main">{{ part.name }}</span><span class="cell-sub mono">{{ part.code }}<span v-if="part.barcode"> · {{ part.barcode }}</span></span><span v-if="part.specifications?.length" class="spec-summary">{{ part.specifications.map(x => `${x.name}: ${x.value}${x.unit ? ` ${x.unit}` : ''}`).join(' · ') }}</span></NuxtLink></td><td><div class="cell-main">{{ categories.find(x => x.id === part.partCategoryId)?.name || '—' }}</div><div class="cell-sub">{{ brands.find(x => x.id === part.partBrandId)?.name || 'Chưa chọn hãng' }}</div></td><td><span v-if="part.supplierIds?.length">{{ part.supplierIds.map(supplierName).join(', ') }}</span><span v-else class="muted">Chưa nhập hàng</span></td><td class="text-right">{{ part.importPrice ? formatCurrency(part.importPrice) : '—' }}</td><td class="text-right cell-main">{{ formatCurrency(part.salePrice) }}</td><td class="text-right"><strong :class="{ danger: part.quantityOnHand < part.minQuantity }">{{ formatNumber(part.quantityOnHand) }}</strong> / {{ formatNumber(part.minQuantity) }} {{ part.unit }}</td><td class="text-right"><div class="inline row-actions"><button class="btn btn-secondary btn-sm" @click="openHistory(part)"><History :size="14" /> Lịch sử nhập</button><template v-if="!isEmployee"><button class="icon-btn small-icon" title="Điều chỉnh tồn" @click="openAdjustment(part)"><SlidersHorizontal :size="14" /></button><button class="icon-btn small-icon" title="Sửa" @click="openPart(part)"><Edit3 :size="14" /></button></template></div></td></tr></tbody>
        </table>
        <AppEmpty v-else-if="!loading" :icon="Package" title="Chưa có phụ tùng" message="Thêm phụ tùng vào một danh mục trước khi lập phiếu chi nhập hàng." />
      </div>
      <AppPagination :page="result.page" :total-pages="result.totalPages" :total="result.total" @change="load" />
    </section>

    <AppModal :open="partModal" :title="editing ? 'Cập nhật phụ tùng' : 'Khai báo phụ tùng'" width="740px" @close="partModal = false">
      <form id="part-form" class="form-grid" @submit.prevent="savePart">
        <div class="part-form-tabs span-2"><button type="button" :class="{ active: partFormTab === 'general' }" @click="partFormTab = 'general'">Thông tin chung</button><button type="button" :class="{ active: partFormTab === 'specifications' }" @click="partFormTab = 'specifications'">Thông số kỹ thuật <span>{{ selectedSpecificationDefinitions.length }}</span></button></div>
        <template v-if="partFormTab === 'general'">
          <div class="field"><label>Mã phụ tùng <span class="muted">(tự động)</span></label><input v-model.trim="form.code" class="input" placeholder="Ví dụ: PT-000001" /></div><div class="field"><label>Barcode</label><input v-model.trim="form.barcode" class="input" /></div>
          <div class="field span-2"><label>Tên phụ tùng *</label><input v-model.trim="form.name" class="input" required /></div>
          <div class="field"><label>Danh mục *</label><AppSearchSelect :model-value="form.partCategoryId" :options="categoryOptions.slice(1)" required :clearable="false" placeholder="Chọn danh mục" @update:model-value="selectCategory" /></div>
          <div class="field"><label>Hãng phụ tùng</label><AppSearchSelect v-model="form.partBrandId" :options="brandOptions" :clearable="true" placeholder="Chọn hãng" search-placeholder="Tìm hãng..." /></div>
          <div class="field"><label>Đơn vị *</label><AppSearchSelect v-model="form.unit" :options="unitOptions" required :clearable="false" placeholder="Chọn đơn vị" search-placeholder="Tìm đơn vị..." /></div><div class="field"><label>Giá bán</label><AppNumberInput v-model="form.salePrice" class="input" min="0" /></div>
          <div class="field"><label>Số lượng cảnh báo (min)</label><AppNumberInput v-model="form.minQuantity" class="input" min="0" /></div>
          <div class="field"><label>Thay sau số km</label><AppNumberInput v-model="form.replacementIntervalKm" class="input" min="1" placeholder="Ví dụ: 12000" /></div>
          <div class="field"><label>Thay sau số tháng</label><AppNumberInput v-model="form.replacementIntervalMonths" class="input" min="1" placeholder="Ví dụ: 24" /></div>
          <div class="info-note span-2">Có thể khai báo một hoặc cả hai chu kỳ. Hệ thống sẽ nhắc theo điều kiện đến trước kể từ lần lắp gần nhất.</div>
          <div class="field span-2"><label>Ghi chú</label><textarea v-model.trim="form.notes" class="textarea" /></div>
          <div class="info-note span-2">Giá nhập, số lượng nhập và nhà cung cấp không khai báo tại đây. Hãy lập “Phiếu nhập phụ tùng” trong mục Thu chi.</div>
        </template>
        <template v-else>
          <div v-if="selectedSpecificationDefinitions.length" class="span-2 specification-fields"><div class="specification-intro"><strong>Thông số của danh mục {{ categories.find(x => x.id === form.partCategoryId)?.name }}</strong><span>Các thông số này hỗ trợ tìm nhanh phụ tùng.</span></div><div class="form-grid"><div v-for="(definition, index) in selectedSpecificationDefinitions" :key="definition.code" class="field"><label>{{ definition.name }}<span v-if="definition.unit"> ({{ definition.unit }})</span>{{ definition.isRequired ? ' *' : '' }}</label><select v-if="definition.dataType === 'Selection'" v-model="specificationValue(index).value" class="select" :required="definition.isRequired"><option value="">Chọn {{ definition.name.toLowerCase() }}</option><option v-for="option in definition.options" :key="option" :value="option">{{ option }}</option></select><label v-else-if="definition.dataType === 'Boolean'" class="boolean-spec-input"><input type="checkbox" :checked="specificationValue(index).value === 'true'" @change="setSpecificationValue(index, ($event.target as HTMLInputElement).checked ? 'true' : 'false')" /><span>{{ specificationValue(index).value === 'true' ? 'Có' : 'Không' }}</span></label><input v-else-if="definition.dataType === 'Number'" type="number" step="any" class="input" :required="definition.isRequired" :value="specificationValue(index).value" :placeholder="`Nhập ${definition.name.toLowerCase()}`" @input="setSpecificationValue(index, ($event.target as HTMLInputElement).value)" /><input v-else v-model.trim="specificationValue(index).value" class="input" :required="definition.isRequired" :placeholder="`Nhập ${definition.name.toLowerCase()}`" /></div></div></div>
          <AppEmpty v-else class="span-2" title="Danh mục chưa có thông số" message="Có thể khai báo bộ thông số trong màn hình Danh mục hệ thống." />
        </template>
      </form>
      <template #footer><button v-if="editing" class="btn btn-secondary danger-button" :disabled="saving" @click="removePart(editing)"><Trash2 :size="15" /> Xóa</button><button class="btn btn-secondary" @click="partModal = false">Hủy</button><button class="btn btn-primary" form="part-form" :disabled="saving">Lưu phụ tùng</button></template>
    </AppModal>

    <AppModal :open="adjustmentModal" :title="`Điều chỉnh tồn: ${selectedPart?.name || ''}`" @close="adjustmentModal = false">
      <form id="adjustment-form" class="form-grid" @submit.prevent="saveAdjustment">
        <div class="field"><label>Loại điều chỉnh</label><select v-model="adjustment.type" class="select"><option value="AdjustmentIncrease">Điều chỉnh tăng (không phải mua hàng)</option><option value="AdjustmentDecrease">Điều chỉnh giảm</option><option value="RepairReturn">Hoàn trả từ sửa chữa</option></select></div>
        <div class="field"><label>Số lượng</label><AppNumberInput v-model="adjustment.quantity" class="input" min="0.01" step="0.01" required /></div>
        <div class="field span-2"><label>Lý do *</label><textarea v-model.trim="adjustment.notes" class="textarea" required /></div>
      </form>
      <template #footer><button class="btn btn-secondary" @click="adjustmentModal = false">Hủy</button><button class="btn btn-primary" form="adjustment-form" :disabled="saving">Cập nhật kho</button></template>
    </AppModal>

    <AppModal :open="historyModal" :title="`Lịch sử nhập: ${selectedPart?.name || ''}`" width="860px" @close="historyModal = false">
      <div class="table-wrap"><table v-if="historyItems.length" class="data-table"><thead><tr><th>Ngày nhập</th><th>Phiếu chi</th><th>Nhà cung cấp</th><th class="text-right">Số lượng</th><th class="text-right">Giá nhập</th><th class="text-right">Thành tiền</th></tr></thead><tbody><tr v-for="item in historyItems" :key="item.id"><td>{{ formatDate(item.transactionDate, true) }}</td><td class="mono">{{ voucherCode(item.referenceId) }}</td><td>{{ supplierName(item.supplierId) }}</td><td class="text-right">{{ formatNumber(item.quantity) }}</td><td class="text-right">{{ formatCurrency(item.unitCost) }}</td><td class="text-right cell-main">{{ formatCurrency(item.quantity * item.unitCost) }}</td></tr></tbody></table><AppEmpty v-else title="Chưa có lịch sử nhập" message="Phụ tùng này chưa được nhập bằng phiếu chi." /></div>
    </AppModal>
  </div>
</template>

<style scoped>
.breadcrumb { margin-bottom: 5px; color: var(--muted); font-size: 11px; font-weight: 750; }.breadcrumb span { padding: 0 5px; color: var(--amber); }
.low-row { background: #fffafa; }.danger { color: var(--red); }.row-actions { justify-content: flex-end; flex-wrap: nowrap; }.small-icon { width: 34px; height: 34px; }
.filter-head { display: grid; grid-template-columns: minmax(250px, 1fr) 210px 230px auto; align-items: center; gap: 10px; }.filter-head > :last-child { text-align: right; }
.info-note { padding: 12px 14px; border-radius: 10px; color: #805b09; background: var(--amber-soft); font-size: 12px; }
.spec-summary { margin-top: 4px; color: #52687a; font-size: 10px; }.specification-fields { display: grid; gap: 9px; padding: 13px; border: 1px solid var(--line); border-radius: 11px; background: #f9fbfc; }.specification-fields > strong { font-size: 12px; }
.boolean-spec-input { display: flex; min-height: 40px; align-items: center; gap: 9px; padding: 0 12px; border: 1px solid var(--line); border-radius: 9px; background: white; }
.part-link,.part-link span { display: block; }.part-link:hover .cell-main { color: var(--blue); }
.part-form-tabs { display: flex; gap: 5px; padding: 4px; border-radius: 11px; background: #eef2f5; }.part-form-tabs button { display: inline-flex; flex: 1; align-items: center; justify-content: center; gap: 7px; min-height: 38px; border: 0; border-radius: 8px; color: var(--muted); background: transparent; font-weight: 750; }.part-form-tabs button.active { color: var(--navy-950); background: white; box-shadow: 0 2px 7px rgb(10 31 51 / 8%); }.part-form-tabs span { display: grid; min-width: 20px; height: 20px; place-items: center; border-radius: 99px; color: var(--navy-900); background: var(--amber-soft); font-size: 9px; }.specification-intro strong,.specification-intro span { display: block; }.specification-intro span { margin-top: 3px; color: var(--muted); font-size: 10px; }
@media (max-width: 900px) { .filter-head { grid-template-columns: 1fr; }.filter-head > :last-child { text-align: left; } }
</style>
