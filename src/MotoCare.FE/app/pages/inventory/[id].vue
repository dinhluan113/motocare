<script setup lang="ts">
import { AlertTriangle, ArrowLeft, Barcode, Boxes, MapPin, Package, Pencil, Tags, Truck } from '@lucide/vue'
import type { InventoryTransaction, PagedResult, Part, PartBrand, PartCategory, Supplier } from '~/types/api'
import { formatCurrency, formatDate, formatNumber } from '~/utils/format'

const route = useRoute()
const api = useApi()
const toast = useToast()
const part = ref<Part>()
const categories = ref<PartCategory[]>([])
const brands = ref<PartBrand[]>([])
const suppliers = ref<Supplier[]>([])
const transactions = ref<InventoryTransaction[]>([])
const loading = ref(true)
const editModalOpen = ref(false)
const saving = ref(false)
interface SpecificationValue { code: string, name: string, unit?: string, value: string }
const form = reactive({
  code: '', barcode: '', name: '', partBrandId: '', partCategoryId: '', unit: '',
  specifications: [] as SpecificationValue[], salePrice: 0, minQuantity: 0,
  location: '', notes: '', isActive: true
})

const partId = computed(() => String(route.params.id))
const categoryName = computed(() => categories.value.find(x => x.id === part.value?.partCategoryId)?.name || '—')
const brandName = computed(() => brands.value.find(x => x.id === part.value?.partBrandId)?.name || 'Chưa chọn hãng')
const supplierNames = computed(() => (part.value?.supplierIds || [])
  .map(id => suppliers.value.find(x => x.id === id)?.name)
  .filter(Boolean)
  .join(', ') || 'Chưa nhập từ nhà cung cấp')
const isLowStock = computed(() => !!part.value && part.value.quantityOnHand < part.value.minQuantity)
const brandOptions = computed(() => brands.value.filter(x => !x.isDeleted).map(x => ({ code: x.id, name: x.name })))
const categoryOptions = computed(() => categories.value.filter(x => !x.isDeleted).map(x => ({ code: x.id, name: x.name })))
const selectedSpecificationDefinitions = computed(() =>
  categories.value.find(x => x.id === form.partCategoryId)?.specificationDefinitions || [])
const unitOptions = [
  'Cái', 'Chiếc', 'Bộ', 'Cặp', 'Hộp', 'Chai', 'Bình', 'Tuýp',
  'Gói', 'Túi', 'Cuộn', 'Mét', 'Lít', 'Thanh', 'Tấm'
].map(name => ({ code: name, name }))

const movementLabel = (type: InventoryTransaction['type']) => ({
  Receipt: 'Nhập kho',
  RepairIssue: 'Xuất sửa chữa',
  RepairReturn: 'Hoàn trả sửa chữa',
  AdjustmentIncrease: 'Điều chỉnh tăng',
  AdjustmentDecrease: 'Điều chỉnh giảm'
}[type] || type)
const isIncrease = (type: InventoryTransaction['type']) =>
  type === 'Receipt' || type === 'RepairReturn' || type === 'AdjustmentIncrease'
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
    unit: part.value.unit,
    specifications: definitions.map(definition => ({
      code: definition.code,
      name: definition.name,
      unit: definition.unit,
      value: part.value?.specifications?.find(x => x.code === definition.code)?.value || ''
    })),
    salePrice: part.value.salePrice,
    minQuantity: part.value.minQuantity,
    location: part.value.location || '',
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
    value: form.specifications.find(x => x.code === definition.code)?.value || ''
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

const load = async () => {
  loading.value = true
  try {
    const [currentPart, categoryPage, brandPage, supplierPage, transactionPage] = await Promise.all([
      api.request<Part>(`/parts/${partId.value}`, { query: { includeDeleted: true } }),
      api.request<PagedResult<PartCategory>>('/part-categories?pageSize=200&includeDeleted=true'),
      api.request<PagedResult<PartBrand>>('/part-brands?pageSize=200&includeDeleted=true'),
      api.request<PagedResult<Supplier>>('/suppliers?pageSize=200&includeDeleted=true'),
      api.request<PagedResult<InventoryTransaction>>('/inventory/transactions', {
        query: { partId: partId.value, pageSize: 200 }
      })
    ])
    part.value = currentPart
    categories.value = categoryPage.items
    brands.value = brandPage.items
    suppliers.value = supplierPage.items
    transactions.value = transactionPage.items
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
        <button v-if="!part.isDeleted" class="btn btn-primary" @click="openEdit"><Pencil :size="17" /> Cập nhật phụ tùng</button>
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
            <div><span><Tags :size="14" /> Danh mục</span><strong>{{ categoryName }}</strong></div>
            <div><span><Boxes :size="14" /> Hãng phụ tùng</span><strong>{{ brandName }}</strong></div>
            <div><span><Barcode :size="14" /> Barcode</span><strong class="mono">{{ part.barcode || '—' }}</strong></div>
            <div><span>Đơn vị tính</span><strong>{{ part.unit }}</strong></div>
            <div><span><MapPin :size="14" /> Vị trí kho</span><strong>{{ part.location || 'Chưa khai báo' }}</strong></div>
            <div><span><Truck :size="14" /> Nhà cung cấp đã nhập</span><strong>{{ supplierNames }}</strong></div>
            <div class="span-2"><span>Ghi chú</span><strong>{{ part.notes || '—' }}</strong></div>
          </div>
        </article>

        <article class="card">
          <header class="card-header"><h2 class="card-title">Thông số kỹ thuật</h2><span class="muted">{{ part.specifications?.length || 0 }} thông số</span></header>
          <div v-if="part.specifications?.length" class="spec-list">
            <div v-for="specification in part.specifications" :key="specification.code">
              <span>{{ specification.name }}</span>
              <strong>{{ specification.value }}<small v-if="specification.unit"> {{ specification.unit }}</small></strong>
            </div>
          </div>
          <AppEmpty v-else title="Chưa có thông số kỹ thuật" message="Phụ tùng này chưa được khai báo thông số theo danh mục." />
        </article>
      </section>

      <section class="card">
        <header class="card-header"><div><h2 class="card-title">Lịch sử nhập – xuất kho</h2><span class="section-note">Toàn bộ biến động tồn kho của phụ tùng</span></div><span class="muted">{{ formatNumber(transactions.length) }} giao dịch</span></header>
        <div class="table-wrap">
          <table v-if="transactions.length" class="data-table">
            <thead><tr><th>Thời gian</th><th>Loại giao dịch</th><th>Nhà cung cấp</th><th>Tham chiếu</th><th>Ghi chú</th><th class="text-right">Số lượng</th><th class="text-right">Đơn giá</th></tr></thead>
            <tbody>
              <tr v-for="transaction in transactions" :key="transaction.id">
                <td>{{ formatDate(transaction.transactionDate, true) }}</td>
                <td><AppBadge :tone="isIncrease(transaction.type) ? 'success' : 'warning'">{{ movementLabel(transaction.type) }}</AppBadge></td>
                <td>{{ supplierName(transaction.supplierId) }}</td>
                <td><div>{{ transaction.referenceType || '—' }}</div><div v-if="transaction.referenceId" class="cell-sub mono">{{ transaction.referenceId }}</div></td>
                <td>{{ transaction.notes || '—' }}</td>
                <td class="text-right movement" :class="isIncrease(transaction.type) ? 'increase' : 'decrease'">{{ isIncrease(transaction.type) ? '+' : '−' }}{{ formatNumber(transaction.quantity) }}</td>
                <td class="text-right">{{ transaction.unitCost ? formatCurrency(transaction.unitCost) : '—' }}</td>
              </tr>
            </tbody>
          </table>
          <AppEmpty v-else title="Chưa có biến động kho" message="Phụ tùng này chưa phát sinh giao dịch nhập hoặc xuất kho." />
        </div>
      </section>
    </template>

    <div v-else-if="loading" class="loading-skeleton" style="height: 420px" />

    <AppModal :open="editModalOpen" title="Cập nhật phụ tùng" width="760px" @close="editModalOpen = false">
      <form id="part-detail-form" class="form-grid" @submit.prevent="savePart">
        <div class="field"><label>Mã phụ tùng</label><input v-model.trim="form.code" class="input" /></div>
        <div class="field"><label>Barcode</label><input v-model.trim="form.barcode" class="input" /></div>
        <div class="field span-2"><label>Tên phụ tùng *</label><input v-model.trim="form.name" class="input" required /></div>
        <div class="field"><label>Danh mục *</label><AppSearchSelect :model-value="form.partCategoryId" :options="categoryOptions" required :clearable="false" placeholder="Chọn danh mục" @update:model-value="selectCategory" /></div>
        <div class="field"><label>Hãng phụ tùng</label><AppSearchSelect v-model="form.partBrandId" :options="brandOptions" :clearable="true" placeholder="Chọn hãng" search-placeholder="Tìm hãng..." /></div>
        <div class="field"><label>Đơn vị *</label><AppSearchSelect v-model="form.unit" :options="unitOptions" required :clearable="false" placeholder="Chọn đơn vị" search-placeholder="Tìm đơn vị..." /></div>
        <div class="field"><label>Giá bán</label><AppNumberInput v-model="form.salePrice" class="input" min="0" /></div>
        <div class="field"><label>Số lượng cảnh báo (min)</label><AppNumberInput v-model="form.minQuantity" class="input" min="0" /></div>
        <div class="field"><label>Vị trí kho</label><input v-model.trim="form.location" class="input" placeholder="Kệ, ngăn hoặc khu vực lưu trữ" /></div>
        <template v-if="selectedSpecificationDefinitions.length">
          <div class="form-section span-2">Thông số kỹ thuật</div>
          <div v-for="(definition, index) in selectedSpecificationDefinitions" :key="definition.code" class="field">
            <label>{{ definition.name }}<span v-if="definition.unit"> ({{ definition.unit }})</span>{{ definition.isRequired ? ' *' : '' }}</label>
            <input v-model.trim="specificationValue(index).value" class="input" :required="definition.isRequired" />
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
.stock-card strong { margin-top: 5px; color: var(--navy-950); font-size: 21px; }
.stock-card small { margin-top: 2px; color: var(--muted); }
.stock-card strong.danger { color: var(--red); }
.detail-layout { display: grid; grid-template-columns: minmax(0, 1.35fr) minmax(300px, .65fr); gap: 18px; }
.card-title { display: flex; align-items: center; gap: 8px; }
.detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }
.detail-grid span,.detail-grid strong { display: block; }
.detail-grid span { display: flex; align-items: center; gap: 5px; color: var(--muted); font-size: 11px; }
.detail-grid strong { margin-top: 4px; color: var(--navy-950); }
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
