<script setup lang="ts">
import { AlertTriangle, Edit3, Package, PackagePlus, Plus, Search } from '@lucide/vue'
import type { PagedResult, Part, PartBrand } from '~/types/api'
import { formatCurrency, formatNumber } from '~/utils/format'

const api = useApi()
const toast = useToast()
const result = ref<PagedResult<Part>>({ items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 })
const brands = ref<PartBrand[]>([])
const search = ref('')
const loading = ref(true)
const saving = ref(false)
const partModal = ref(false)
const movementModal = ref(false)
const editing = ref<Part>()
const selectedPart = ref<Part>()
const form = reactive({
  code: '', barcode: '', name: '', partBrandId: '', unit: 'Cái',
  importPrice: 0, stockPrice: 0, salePrice: 0, quantityOnHand: 0,
  minQuantity: 0, location: '', notes: '', isActive: true
})
const movement = reactive({ type: 'Receipt', quantity: 1, unitCost: 0, notes: '' })
const brandOptions = computed(() => brands.value.map(brand => ({
  code: brand.id,
  name: brand.name
})))

const load = async (page = 1) => {
  loading.value = true
  try {
    const [partsPage, brandPage] = await Promise.all([
      api.request<PagedResult<Part>>('/parts', { query: { search: search.value || undefined, page, pageSize: 20 } }),
      api.request<PagedResult<PartBrand>>('/part-brands?pageSize=200')
    ])
    result.value = partsPage
    brands.value = brandPage.items
  } finally { loading.value = false }
}

let timer: ReturnType<typeof setTimeout>
watch(search, () => { clearTimeout(timer); timer = setTimeout(() => load(1), 350) })

const openPart = (part?: Part) => {
  editing.value = part
  Object.assign(form, part || {
    code: '', barcode: '', name: '', partBrandId: brands.value[0]?.id || '', unit: 'Cái',
    importPrice: 0, stockPrice: 0, salePrice: 0, quantityOnHand: 0,
    minQuantity: 0, location: '', notes: '', isActive: true
  })
  partModal.value = true
}

const savePart = async () => {
  saving.value = true
  try {
    if (editing.value) await api.request(`/parts/${editing.value.id}`, { method: 'PUT', body: form })
    else await api.request('/parts', { method: 'POST', body: form })
    toast.success('Đã lưu phụ tùng', form.name)
    partModal.value = false
    await load(result.value.page)
  } finally { saving.value = false }
}

const openMovement = (part: Part) => {
  selectedPart.value = part
  Object.assign(movement, { type: 'Receipt', quantity: 1, unitCost: part.importPrice, notes: '' })
  movementModal.value = true
}

const saveMovement = async () => {
  saving.value = true
  try {
    await api.request('/inventory/movements', {
      method: 'POST',
      body: { ...movement, partId: selectedPart.value?.id, referenceType: 'Manual', referenceId: null }
    })
    toast.success('Đã cập nhật tồn kho', selectedPart.value?.name)
    movementModal.value = false
    await load(result.value.page)
  } finally { saving.value = false }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-header">
      <div><h1 class="page-title">Kho phụ tùng</h1><p class="page-subtitle">Danh mục, giá vốn, giá bán, tồn thực tế và cảnh báo định mức.</p></div>
      <button class="btn btn-accent" @click="openPart()"><Plus :size="17" /> Thêm phụ tùng</button>
    </div>
    <div v-if="result.items.some(x => x.quantityOnHand < x.minQuantity)" class="alert alert-warning"><AlertTriangle :size="19" /><div><strong>Có phụ tùng dưới định mức</strong><div>Các dòng màu đỏ cần được bổ sung tồn kho.</div></div></div>
    <section class="card">
      <header class="card-header"><div class="search-box"><Search :size="17" /><input v-model="search" class="input" placeholder="Tìm mã, tên, barcode..." /></div><span class="muted">{{ formatNumber(result.total) }} phụ tùng</span></header>
      <div class="table-wrap">
        <table v-if="result.items.length" class="data-table">
          <thead><tr><th>Phụ tùng</th><th>Hãng / vị trí</th><th class="text-right">Giá nhập</th><th class="text-right">Giá bán</th><th class="text-right">Tồn / Min</th><th class="text-right">Thao tác</th></tr></thead>
          <tbody><tr v-for="part in result.items" :key="part.id" :class="{ 'low-row': part.quantityOnHand < part.minQuantity }"><td><div class="cell-main">{{ part.name }}</div><div class="cell-sub mono">{{ part.code }}<span v-if="part.barcode"> · {{ part.barcode }}</span></div></td><td><div class="cell-main">{{ brands.find(x => x.id === part.partBrandId)?.name || '—' }}</div><div class="cell-sub">{{ part.location || 'Chưa xếp vị trí' }}</div></td><td class="text-right">{{ formatCurrency(part.importPrice) }}</td><td class="text-right cell-main">{{ formatCurrency(part.salePrice) }}</td><td class="text-right"><strong :class="{ danger: part.quantityOnHand < part.minQuantity }">{{ formatNumber(part.quantityOnHand) }}</strong> / {{ formatNumber(part.minQuantity) }} {{ part.unit }}</td><td class="text-right"><div class="inline row-actions"><button class="btn btn-secondary btn-sm" @click="openMovement(part)"><PackagePlus :size="14" /> Nhập/xuất</button><button class="icon-btn small-icon" title="Sửa" @click="openPart(part)"><Edit3 :size="14" /></button></div></td></tr></tbody>
        </table>
        <AppEmpty v-else-if="!loading" :icon="Package" title="Chưa có phụ tùng" message="Thêm danh mục đầu tiên để quản lý tồn kho." />
        <div v-else class="card-body"><div class="loading-skeleton" style="height: 280px" /></div>
      </div>
      <AppPagination :page="result.page" :total-pages="result.totalPages" :total="result.total" @change="load" />
    </section>

    <AppModal :open="partModal" :title="editing ? 'Cập nhật phụ tùng' : 'Thêm phụ tùng'" width="740px" @close="partModal = false">
      <form id="part-form" class="form-grid" @submit.prevent="savePart">
        <div class="field"><label>Mã phụ tùng *</label><input v-model.trim="form.code" class="input" required /></div><div class="field"><label>Barcode</label><input v-model.trim="form.barcode" class="input" /></div>
        <div class="field span-2"><label>Tên phụ tùng *</label><input v-model.trim="form.name" class="input" required /></div>
        <div class="field"><label>Hãng phụ tùng *</label><AppSearchSelect v-model="form.partBrandId" :options="brandOptions" placeholder="Chọn hãng phụ tùng" search-placeholder="Tìm hãng phụ tùng..." required :clearable="false" /></div><div class="field"><label>Đơn vị</label><input v-model.trim="form.unit" class="input" required /></div>
        <div class="field"><label>Giá nhập</label><AppNumberInput v-model="form.importPrice" class="input" min="0" /></div><div class="field"><label>Giá kho</label><AppNumberInput v-model="form.stockPrice" class="input" min="0" /></div>
        <div class="field"><label>Giá bán</label><AppNumberInput v-model="form.salePrice" class="input" min="0" /></div><div class="field"><label>Vị trí kho</label><input v-model.trim="form.location" class="input" /></div>
        <div class="field"><label>Tồn ban đầu</label><AppNumberInput v-model="form.quantityOnHand" class="input" min="0" :disabled="!!editing" /></div><div class="field"><label>Số lượng cảnh báo (min)</label><AppNumberInput v-model="form.minQuantity" class="input" min="0" /></div>
      </form>
      <template #footer><button class="btn btn-secondary" @click="partModal = false">Hủy</button><button class="btn btn-primary" form="part-form" :disabled="saving">Lưu phụ tùng</button></template>
    </AppModal>

    <AppModal :open="movementModal" :title="`Nhập/xuất: ${selectedPart?.name || ''}`" @close="movementModal = false">
      <form id="movement-form" class="form-grid" @submit.prevent="saveMovement">
        <div class="field"><label>Loại giao dịch</label><select v-model="movement.type" class="select"><option value="Receipt">Nhập kho</option><option value="AdjustmentIncrease">Điều chỉnh tăng</option><option value="AdjustmentDecrease">Điều chỉnh giảm</option><option value="RepairReturn">Hoàn trả từ sửa chữa</option></select></div>
        <div class="field"><label>Số lượng</label><AppNumberInput v-model="movement.quantity" class="input" min="0.01" step="0.01" required /></div>
        <div class="field span-2"><label>Đơn giá vốn</label><AppNumberInput v-model="movement.unitCost" class="input" min="0" /></div>
        <div class="field span-2"><label>Ghi chú</label><textarea v-model.trim="movement.notes" class="textarea" /></div>
      </form>
      <template #footer><button class="btn btn-secondary" @click="movementModal = false">Hủy</button><button class="btn btn-primary" form="movement-form" :disabled="saving">Cập nhật kho</button></template>
    </AppModal>
  </div>
</template>

<style scoped>
.low-row { background: #fffafa; }
.danger { color: var(--red); }
.row-actions { justify-content: flex-end; flex-wrap: nowrap; }
.small-icon { width: 34px; height: 34px; }
</style>
