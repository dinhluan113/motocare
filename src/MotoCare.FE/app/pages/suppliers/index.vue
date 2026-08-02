<script setup lang="ts">
import { Edit3, PackageSearch, Plus, Search, Trash2, Truck } from '@lucide/vue'
import type { PagedResult, Supplier } from '~/types/api'
import { entityDetailRoute } from '~/utils/entityRoute'
import { emptyAddressDetails, formatAddressDetails, normalizeAddressDetails } from '~/utils/location'
import { formatCurrency, formatDate, formatNumber } from '~/utils/format'

interface SupplierStock {
  id: string
  name: string
  totalQuantityOnHand: number
  items: Array<{ partId: string, partCode: string, partName: string, quantityOnHand: number, lastUnitCost: number, lastReceiptAt?: string }>
}

const api = useApi()
const toast = useToast()
const result = ref<PagedResult<Supplier>>({ items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 })
const search = ref('')
const loading = ref(true)
const saving = ref(false)
const modalOpen = ref(false)
const stockModal = ref(false)
const editing = ref<Supplier>()
const stock = ref<SupplierStock>()
const form = reactive({ code: '', name: '', phone: '', taxCode: '', address: '', addressDetails: emptyAddressDetails(), notes: '', isActive: true })

const load = async (page = 1) => {
  loading.value = true
  try { result.value = await api.request('/suppliers', { query: { search: search.value || undefined, page, pageSize: 20 } }) }
  finally { loading.value = false }
}
let timer: ReturnType<typeof setTimeout>
watch(search, () => { clearTimeout(timer); timer = setTimeout(() => load(), 350) })

const openForm = (item?: Supplier) => {
  editing.value = item
  Object.assign(form, {
    code: item?.code || '', name: item?.name || '', phone: item?.phone || '', taxCode: item?.taxCode || '',
    address: item?.address || '', addressDetails: normalizeAddressDetails(item?.addressDetails, item?.address),
    notes: item?.notes || '', isActive: item?.isActive ?? true
  })
  modalOpen.value = true
}
const save = async () => {
  saving.value = true
  try {
    form.address = formatAddressDetails(form.addressDetails)
    await api.request(`/suppliers${editing.value ? `/${editing.value.id}` : ''}`, {
      method: editing.value ? 'PUT' : 'POST', body: form
    })
    toast.success('Đã lưu nhà cung cấp', form.name)
    modalOpen.value = false
    await load(result.value.page)
  } finally { saving.value = false }
}
const remove = async () => {
  if (!editing.value || !confirm(`Xóa nhà cung cấp ${editing.value.name}?`)) return
  await api.request(`/suppliers/${editing.value.id}`, { method: 'DELETE' })
  toast.success('Đã xóa nhà cung cấp', editing.value.name)
  modalOpen.value = false
  await load(result.value.page)
}
const viewStock = async (item: Supplier) => {
  stock.value = await api.request(`/suppliers/${item.id}/stock`)
  stockModal.value = true
}
onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-header">
      <div><h1 class="page-title">Nhà cung cấp</h1><p class="page-subtitle">Điểm bắt đầu của luồng Nhà cung cấp → Danh mục → Phụ tùng.</p></div>
      <button class="btn btn-accent" @click="openForm()"><Plus :size="17" /> Thêm nhà cung cấp</button>
    </div>
    <section class="card">
      <header class="card-header"><div class="search-box"><Search :size="17" /><input v-model="search" class="input" placeholder="Tìm tên, mã, điện thoại, mã số thuế..." /></div><span class="muted">{{ formatNumber(result.total) }} nhà cung cấp</span></header>
      <div class="table-wrap">
        <table v-if="result.items.length" class="data-table">
          <thead><tr><th>Nhà cung cấp</th><th>Liên hệ</th><th>Địa chỉ</th><th>Mã số thuế</th><th>Trạng thái</th><th class="text-right">Thao tác</th></tr></thead>
          <tbody><tr v-for="item in result.items" :key="item.id"><td><AppEntityLink block :to="entityDetailRoute('Supplier', item.id)"><span class="cell-main">{{ item.name }}</span><span class="cell-sub mono">{{ item.code }}</span></AppEntityLink></td><td>{{ item.phone }}</td><td>{{ item.address || '—' }}</td><td class="mono">{{ item.taxCode || '—' }}</td><td><AppBadge :tone="item.isActive ? 'success' : 'neutral'">{{ item.isActive ? 'Hoạt động' : 'Tạm khóa' }}</AppBadge></td><td class="text-right"><div class="inline row-actions"><button class="btn btn-secondary btn-sm" @click="viewStock(item)"><PackageSearch :size="14" /> Xem tồn</button><button class="icon-btn small-icon" title="Sửa" @click="openForm(item)"><Edit3 :size="14" /></button></div></td></tr></tbody>
        </table>
        <AppEmpty v-else-if="!loading" :icon="Truck" title="Chưa có nhà cung cấp" message="Khai báo nhà cung cấp trước khi lập Phiếu nhập phụ tùng." />
      </div>
      <AppPagination :page="result.page" :total-pages="result.totalPages" :total="result.total" @change="load" />
    </section>

    <AppModal :open="modalOpen" :title="editing ? 'Cập nhật nhà cung cấp' : 'Thêm nhà cung cấp'" width="820px" @close="modalOpen = false">
      <form id="supplier-form" class="form-grid" @submit.prevent="save">
        <div class="field"><label>Mã nhà cung cấp <span class="muted">(tự động)</span></label><input v-model.trim="form.code" class="input" placeholder="Ví dụ: NCC-000001" /></div>
        <div class="field"><label>Tên nhà cung cấp *</label><input v-model.trim="form.name" class="input" required /></div>
        <div class="field"><label>Số điện thoại *</label><input v-model.trim="form.phone" class="input" required /></div>
        <div class="field"><label>Mã số thuế <span class="muted">(tùy chọn)</span></label><input v-model.trim="form.taxCode" class="input" /></div>
        <AppAddressFields v-model="form.addressDetails" />
        <div class="field span-2"><label>Ghi chú</label><textarea v-model.trim="form.notes" class="textarea" /></div>
      </form>
      <template #footer><button v-if="editing" class="btn btn-secondary danger-button" :disabled="saving" @click="remove"><Trash2 :size="15" /> Xóa</button><button class="btn btn-secondary" @click="modalOpen = false">Hủy</button><button class="btn btn-primary" form="supplier-form" :disabled="saving">Lưu nhà cung cấp</button></template>
    </AppModal>

    <AppModal :open="stockModal" :title="`Tồn kho từ ${stock?.name || ''}`" width="760px" @close="stockModal = false">
      <div class="stock-total"><span>Tổng số lượng tồn</span><strong>{{ formatNumber(stock?.totalQuantityOnHand || 0) }}</strong></div>
      <div class="table-wrap"><table v-if="stock?.items.length" class="data-table"><thead><tr><th>Phụ tùng</th><th class="text-right">Tồn</th><th class="text-right">Giá nhập gần nhất</th><th>Lần nhập gần nhất</th></tr></thead><tbody><tr v-for="item in stock.items" :key="item.partId"><td><NuxtLink class="part-link" :to="`/inventory/${item.partId}`" @click="stockModal = false"><span class="cell-main">{{ item.partName }}</span><span class="cell-sub mono">{{ item.partCode }}</span></NuxtLink></td><td class="text-right cell-main">{{ formatNumber(item.quantityOnHand) }}</td><td class="text-right">{{ formatCurrency(item.lastUnitCost) }}</td><td>{{ item.lastReceiptAt ? formatDate(item.lastReceiptAt) : '—' }}</td></tr></tbody></table><AppEmpty v-else title="Chưa có tồn kho" message="Nhà cung cấp này chưa có phụ tùng được nhập qua phiếu chi." /></div>
    </AppModal>
  </div>
</template>

<style scoped>
.row-actions { justify-content: flex-end; flex-wrap: nowrap; }
.small-icon { width: 34px; height: 34px; }
.stock-total { display: flex; align-items: center; justify-content: space-between; margin-bottom: 14px; padding: 14px 16px; border-radius: 12px; background: var(--amber-soft); }
.stock-total strong { color: var(--navy-950); font-size: 24px; }
.part-link { display: block; }
.part-link:hover .cell-main { color: var(--blue); }
</style>
