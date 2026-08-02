<script setup lang="ts">
import { AlertTriangle, ArrowDownCircle, ArrowUpCircle, CheckCircle2, Edit3, Eye, ImagePlus, PackagePlus, Plus, RotateCcw, Search, Tags, Trash2, WalletCards } from '@lucide/vue'
import type { CashCategory, CashTransaction, PagedResult, Part, PurchaseExpenseItem, Supplier, WarehouseLocation } from '~/types/api'
import { entityDetailRoute } from '~/utils/entityRoute'
import { formatCurrency, formatDate } from '~/utils/format'

const api = useApi()
const { mediaUrl, uploadImage, deleteImage } = useMedia()
const toast = useToast()
const result = ref<PagedResult<CashTransaction>>({ items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 })
const suppliers = ref<Supplier[]>([])
const parts = ref<Part[]>([])
const warehouseLocations = ref<WarehouseLocation[]>([])
const cashCategories = ref<CashCategory[]>([])
const modalOpen = ref(false)
const transactionFormTab = ref<'general' | 'proof'>('general')
const categoryModal = ref(false)
const categoryFormModal = ref(false)
const detailModal = ref(false)
const saving = ref(false)
const confirming = ref('')
const editingCategory = ref<CashCategory>()
const selectedTransaction = ref<CashTransaction>()
const attachmentInput = ref<HTMLInputElement>()
const search = ref('')
const typeFilter = ref('')
const statusFilter = ref('')
const categoryFilter = ref('')
const fromDate = ref('')
const toDate = ref('')
const form = reactive({ code: '', type: 'Expense' as 'Income' | 'Expense', purpose: 'Other' as 'Other' | 'PartsPurchase', supplierId: '', cashCategoryId: '', category: '', transactionDate: new Date().toISOString().slice(0, 10), amount: 0, paymentMethod: 'Cash', description: '', attachmentUrl: '', status: 'New', purchaseItems: [] as PurchaseExpenseItem[] })
const categoryForm = reactive({ code: '', name: '', scope: 'Both' as 'Income' | 'Expense' | 'Both', description: '', isActive: true })
const supplierOptions = computed(() => suppliers.value.filter(x => x.isActive).map(x => ({ code: x.id, name: `${x.name} · ${x.phone}` })))
const partOptions = computed(() => parts.value.filter(x => x.isActive).map(x => ({ code: x.id, name: `${x.code} · ${x.name}${x.specifications?.length ? ` · ${x.specifications.map(s => s.value).join(' · ')}` : ''}` })))
const purchaseTotal = computed(() => form.purchaseItems.reduce((sum, x) => sum + (Number(x.quantity) || 0) * (Number(x.unitCost) || 0), 0))
const categoryOptions = computed(() => cashCategories.value
  .filter(x => x.isActive && (x.scope === 'Both' || x.scope === form.type))
  .map(x => ({ code: x.id, name: x.name })))
const categoryFilterOptions = computed(() => [
  { code: '', name: 'Tất cả danh mục' },
  ...cashCategories.value.map(x => ({ code: x.id, name: x.name }))
])

const load = async (page = 1) => {
  if (fromDate.value && toDate.value && fromDate.value > toDate.value) {
    toast.error('Khoảng ngày không hợp lệ', 'Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.')
    return
  }
  const [transactions, supplierPage, partPage, locationPage, categoryPage] = await Promise.all([
    api.request<PagedResult<CashTransaction>>('/cash-transactions', {
      query: {
        page,
        pageSize: 20,
        search: search.value || undefined,
        type: typeFilter.value || undefined,
        status: statusFilter.value || undefined,
        cashCategoryId: categoryFilter.value || undefined,
        from: fromDate.value || undefined,
        to: toDate.value || undefined
      }
    }),
    api.request<PagedResult<Supplier>>('/suppliers?pageSize=200'),
    api.request<PagedResult<Part>>('/parts?pageSize=200'),
    api.request<PagedResult<WarehouseLocation>>('/warehouse-locations?pageSize=500'),
    api.request<PagedResult<CashCategory>>('/cash-categories?pageSize=200')
  ])
  result.value = {
    ...transactions,
    items: transactions.items.map(item => ({
      ...item,
      attachmentUrl: item.attachmentUrl ? mediaUrl(item.attachmentUrl) : undefined
    }))
  }
  suppliers.value = supplierPage.items; parts.value = partPage.items; warehouseLocations.value = locationPage.items; cashCategories.value = categoryPage.items
}
const locationOptionsForPart = (partId: string) => {
  const part = parts.value.find(x => x.id === partId)
  const ids = part?.warehouseLocationIds?.length
    ? part.warehouseLocationIds
    : part?.warehouseLocationId ? [part.warehouseLocationId] : []
  return ids.map(id => warehouseLocations.value.find(x => x.id === id))
    .filter((x): x is WarehouseLocation => !!x && x.isActive && !x.isDeleted)
    .map(x => ({ code: x.id, name: `${x.code} · ${x.name}` }))
}
const selectPurchasePart = (line: PurchaseExpenseItem, partId: string) => {
  line.partId = partId
  const options = locationOptionsForPart(partId)
  const preferred = parts.value.find(x => x.id === partId)?.warehouseLocationId
  line.warehouseLocationId = options.some(option => option.code === preferred)
    ? preferred!
    : options[0]?.code || ''
}
const purchaseLocation = (line: PurchaseExpenseItem) => warehouseLocations.value
  .find(x => x.id === line.warehouseLocationId)
const resetFilters = async () => {
  search.value = ''
  typeFilter.value = ''
  statusFilter.value = ''
  categoryFilter.value = ''
  fromDate.value = ''
  toDate.value = ''
  await load(1)
}
const openForm = (purchase = false) => {
  transactionFormTab.value = 'general'
  Object.assign(form, { code: '', type: 'Expense', purpose: purchase ? 'PartsPurchase' : 'Other', supplierId: '', cashCategoryId: '', category: '', transactionDate: new Date().toISOString().slice(0, 10), amount: 0, paymentMethod: 'Cash', description: purchase ? 'Nhập phụ tùng' : '', attachmentUrl: '', status: 'New', purchaseItems: purchase ? [{ partId: '', warehouseLocationId: '', quantity: 1, unitCost: 0 }] : [] })
  if (!purchase) form.cashCategoryId = categoryOptions.value[0]?.code || ''
  modalOpen.value = true
}
watch(() => form.type, () => {
  if (form.purpose === 'Other' && !categoryOptions.value.some(x => x.code === form.cashCategoryId)) {
    form.cashCategoryId = categoryOptions.value[0]?.code || ''
  }
})
const addLine = () => form.purchaseItems.push({ partId: '', warehouseLocationId: '', quantity: 1, unitCost: 0 })
const removeLine = (index: number) => form.purchaseItems.splice(index, 1)
const save = async () => {
  const purchase = form.purpose === 'PartsPurchase'
  const missingGeneral = !form.transactionDate || !form.description.trim()
    || (purchase
      ? !form.supplierId || form.purchaseItems.some(x => !x.partId || !x.warehouseLocationId || x.quantity <= 0 || x.unitCost <= 0)
      : !form.cashCategoryId || form.amount <= 0)
  if (missingGeneral) {
    transactionFormTab.value = 'general'
    toast.error('Thiếu thông tin giao dịch', 'Vui lòng kiểm tra các trường bắt buộc trước khi lưu.')
    return
  }
  saving.value = true
  try {
    await api.request('/cash-transactions', {
      method: 'POST',
      body: {
        ...form, type: purchase ? 'Expense' : form.type, amount: purchase ? purchaseTotal.value : form.amount,
        category: purchase ? 'Nhập phụ tùng' : form.category,
        transactionDate: new Date(form.transactionDate).toISOString(), referenceType: null, referenceId: null, approvedBy: null
      }
    })
    toast.success(purchase || form.type === 'Expense' ? 'Đã tạo phiếu chi ở trạng thái New' : 'Đã ghi nhận khoản thu', form.description)
    modalOpen.value = false; await load()
  } finally { saving.value = false }
}
const confirmVoucher = async (item: CashTransaction) => {
  const hasLowProfit = item.purchaseItems?.some(x => x.isLowProfit)
  const message = hasLowProfit
    ? 'Phiếu có phụ tùng với lợi nhuận dưới 20%. Bạn vẫn muốn xác nhận và nhập kho?'
    : 'Xác nhận phiếu chi này? Sau khi xác nhận, phiếu sẽ kết thúc và không thể sửa trực tiếp.'
  if (!window.confirm(message)) return
  confirming.value = item.id
  try {
    await api.request(`/cash-transactions/${item.id}/confirm`, { method: 'POST' })
    toast.success('Đã xác nhận phiếu chi', item.code)
    if (selectedTransaction.value?.id === item.id) detailModal.value = false
    await load(result.value.page)
  } finally { confirming.value = '' }
}
const profitRate = (line: PurchaseExpenseItem) => {
  const salePrice = parts.value.find(x => x.id === line.partId)?.salePrice || 0
  return line.unitCost > 0 ? (salePrice - line.unitCost) / line.unitCost * 100 : null
}
const isLowProfit = (line: PurchaseExpenseItem) => (profitRate(line) ?? 20) < 20
const formatProfit = (line: PurchaseExpenseItem) => (profitRate(line) ?? 0).toFixed(2)
const income = computed(() => result.value.items.filter(x => x.type === 'Income' && x.status !== 'New').reduce((a, x) => a + x.amount, 0))
const expense = computed(() => result.value.items.filter(x => x.type === 'Expense' && x.status !== 'New').reduce((a, x) => a + x.amount, 0))
const supplierName = (id?: string) => suppliers.value.find(x => x.id === id)?.name || '—'
const openDetail = (item: CashTransaction) => {
  selectedTransaction.value = item
  detailModal.value = true
}
const paymentMethodName = (method: string) => method === 'Cash' ? 'Tiền mặt' : method === 'BankTransfer' ? 'Chuyển khoản' : method
const chooseAttachment = () => {
  if (attachmentInput.value) attachmentInput.value.value = ''
  attachmentInput.value?.click()
}
const readAttachment = async (event: Event) => {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  if (!file.type.startsWith('image/')) { toast.error('Tệp không hợp lệ', 'Vui lòng chọn một tệp ảnh.'); return }
  if (file.size > 4 * 1024 * 1024) { toast.error('Ảnh quá lớn', 'Dung lượng ảnh tối đa là 4 MB.'); return }
  const previous = form.attachmentUrl
  form.attachmentUrl = await uploadImage(file, 'finance')
  await deleteImage(previous)
}
const removeAttachment = async () => {
  const previous = form.attachmentUrl
  form.attachmentUrl = ''
  await deleteImage(previous)
}
const openCategoryForm = (item?: CashCategory) => {
  editingCategory.value = item
  Object.assign(categoryForm, { code: item?.code || '', name: item?.name || '', scope: item?.scope || 'Both', description: item?.description || '', isActive: item?.isActive ?? true })
  categoryFormModal.value = true
}
const saveCategory = async () => {
  saving.value = true
  try {
    await api.request(`/cash-categories${editingCategory.value ? `/${editingCategory.value.id}` : ''}`, { method: editingCategory.value ? 'PUT' : 'POST', body: categoryForm })
    toast.success('Đã lưu danh mục thu chi', categoryForm.name)
    categoryFormModal.value = false
    await load(result.value.page)
  } finally { saving.value = false }
}
const deleteCategory = async (item: CashCategory) => {
  if (!window.confirm(`Xóa danh mục “${item.name}”? Các phiếu cũ vẫn giữ tên danh mục đã ghi nhận.`)) return
  await api.request(`/cash-categories/${item.id}`, { method: 'DELETE' })
  toast.success('Đã xóa danh mục', item.name)
  await load(result.value.page)
}
onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-header"><div><h1 class="page-title">Thu chi</h1><p class="page-subtitle">Phiếu chi dùng để nhập hàng hoặc thanh toán các chi phí vận hành khác.</p></div><div class="inline"><button class="btn btn-secondary" @click="categoryModal = true"><Tags :size="17" /> Danh mục thu chi</button><button class="btn btn-secondary" @click="openForm(false)"><Plus :size="17" /> Thu / chi khác</button><button class="btn btn-accent" @click="openForm(true)"><PackagePlus :size="17" /> Phiếu nhập phụ tùng</button></div></div>
    <section class="cash-summary"><article><ArrowDownCircle :size="22" /><div><span>Tổng thu trên trang</span><strong>{{ formatCurrency(income) }}</strong></div></article><article><ArrowUpCircle :size="22" /><div><span>Tổng chi trên trang</span><strong>{{ formatCurrency(expense) }}</strong></div></article><article><WalletCards :size="22" /><div><span>Chênh lệch</span><strong>{{ formatCurrency(income - expense) }}</strong></div></article></section>
    <form class="card finance-filters" @submit.prevent="load(1)">
      <div class="filter-search"><Search :size="17" /><input v-model.trim="search" class="input" placeholder="Tìm mã phiếu, nội dung, danh mục, phụ tùng..." /></div>
      <select v-model="typeFilter" class="select" aria-label="Loại giao dịch"><option value="">Tất cả loại</option><option value="Income">Khoản thu</option><option value="Expense">Khoản chi</option></select>
      <select v-model="statusFilter" class="select" aria-label="Trạng thái"><option value="">Tất cả trạng thái</option><option value="New">New</option><option value="Confirmed">Đã xác nhận</option><option value="Approved">Đã ghi nhận</option></select>
      <AppSearchSelect v-model="categoryFilter" :options="categoryFilterOptions" :clearable="false" placeholder="Tất cả danh mục" />
      <div class="filter-date"><label>Từ ngày</label><input v-model="fromDate" class="input" type="date" /></div>
      <div class="filter-date"><label>Đến ngày</label><input v-model="toDate" class="input" type="date" /></div>
      <div class="filter-actions"><button class="btn btn-primary" type="submit">Áp dụng</button><button class="icon-btn" type="button" title="Xóa bộ lọc" @click="resetFilters"><RotateCcw :size="16" /></button></div>
    </form>
    <section class="card"><header class="card-header"><h2 class="card-title">Sổ giao dịch</h2><span class="muted">{{ result.total }} giao dịch</span></header><div class="table-wrap"><table v-if="result.items.length" class="data-table"><thead><tr><th>Mã</th><th>Ngày</th><th>Loại / danh mục</th><th>Nội dung / nhà cung cấp</th><th>Trạng thái</th><th class="text-right">Số tiền</th><th class="text-right">Thao tác</th></tr></thead><tbody><tr v-for="item in result.items" :key="item.id"><td><AppEntityLink class="mono cell-main" :to="entityDetailRoute('CashTransaction', item.id)">{{ item.code }}</AppEntityLink></td><td>{{ formatDate(item.transactionDate) }}</td><td><AppBadge :tone="item.type === 'Income' ? 'success' : 'danger'">{{ item.type === 'Income' ? 'Thu' : 'Chi' }}</AppBadge><div><AppEntityLink class="cell-sub" :to="entityDetailRoute('CashCategory', item.cashCategoryId)">{{ item.category }}</AppEntityLink></div></td><td><div class="cell-main">{{ item.description }}</div><div v-if="item.purpose === 'PartsPurchase'" class="cell-sub"><AppEntityLink :to="entityDetailRoute('Supplier', item.supplierId)">{{ supplierName(item.supplierId) }}</AppEntityLink> · {{ item.purchaseItems.length }} phụ tùng</div><AppImageGallery v-if="item.attachmentUrl" class="table-proof" :images="[item.attachmentUrl]" alt="Ảnh đính kèm" compact /><div v-if="item.purchaseItems?.some(x => x.isLowProfit)" class="low-profit-text"><AlertTriangle :size="12" /> Có giá nhập làm lợi nhuận dưới 20%</div></td><td><AppBadge :tone="item.status === 'New' ? 'warning' : 'success'">{{ item.status === 'New' ? 'New' : 'Đã xác nhận' }}</AppBadge></td><td class="text-right cell-main" :class="item.type === 'Income' ? 'income' : 'expense'">{{ item.type === 'Income' ? '+' : '-' }}{{ formatCurrency(item.amount) }}</td><td class="text-right"><div class="inline row-actions"><button class="icon-btn" title="Xem nhanh" @click="openDetail(item)"><Eye :size="15" /></button><button v-if="item.type === 'Expense' && item.status === 'New'" class="btn btn-primary btn-sm" :disabled="confirming === item.id" @click="confirmVoucher(item)"><CheckCircle2 :size="14" /> Xác nhận</button></div></td></tr></tbody></table><AppEmpty v-else :icon="WalletCards" title="Chưa có giao dịch" message="Lập phiếu thu hoặc phiếu chi đầu tiên." /></div><AppPagination :page="result.page" :total-pages="result.totalPages" :total="result.total" @change="load" /></section>

    <AppModal :open="modalOpen" :title="form.purpose === 'PartsPurchase' ? 'Phiếu nhập phụ tùng' : 'Ghi nhận thu chi'" :width="form.purpose === 'PartsPurchase' ? '920px' : '700px'" @close="modalOpen = false">
      <form id="cash-form" class="form-grid" @submit.prevent="save">
        <div class="transaction-tabs span-2"><button type="button" :class="{ active: transactionFormTab === 'general' }" @click="transactionFormTab = 'general'">Thông tin giao dịch</button><button type="button" :class="{ active: transactionFormTab === 'proof' }" @click="transactionFormTab = 'proof'">Ảnh đính kèm <span v-if="form.attachmentUrl">1</span></button></div>
        <template v-if="transactionFormTab === 'general'">
        <template v-if="form.purpose === 'PartsPurchase'">
          <div class="field"><label>Mã phiếu</label><input v-model.trim="form.code" class="input" placeholder="Để trống để tạo tự động" /></div>
          <div class="field"><label>Ngày nhập / chi tiền *</label><input v-model="form.transactionDate" class="input" type="date" required /></div>
          <div class="field"><label>Nhà cung cấp *</label><AppSearchSelect v-model="form.supplierId" :options="supplierOptions" required :clearable="false" placeholder="Chọn nhà cung cấp" search-placeholder="Tìm nhà cung cấp..." /></div>
          <div class="field"><label>Phương thức *</label><div class="radio-group"><label><input v-model="form.paymentMethod" type="radio" value="Cash" /> Tiền mặt</label><label><input v-model="form.paymentMethod" type="radio" value="BankTransfer" /> Chuyển khoản</label></div></div>
          <div class="field span-2"><label>Nội dung *</label><input v-model.trim="form.description" class="input" required /></div>
          <div class="span-2 purchase-lines">
            <div class="line-head"><strong>Phụ tùng nhập</strong><button class="btn btn-secondary btn-sm" type="button" @click="addLine"><Plus :size="14" /> Thêm dòng</button></div>
            <div v-for="(line, index) in form.purchaseItems" :key="index" class="purchase-line">
              <div class="field"><label>Phụ tùng *</label><AppSearchSelect :model-value="line.partId" :options="partOptions" required :clearable="false" placeholder="Chọn phụ tùng" @update:model-value="selectPurchasePart(line, $event)" /></div>
              <div class="field"><label>Ngăn nhập *</label><AppSearchSelect v-model="line.warehouseLocationId" :options="locationOptionsForPart(line.partId)" :disabled="!line.partId" required :clearable="false" placeholder="Chọn ngăn nhập" /></div>
              <div class="field"><label>Số lượng *</label><AppNumberInput v-model="line.quantity" class="input" min="0.01" step="0.01" required /></div>
              <div class="field"><label>Giá nhập *</label><AppNumberInput v-model="line.unitCost" class="input" min="0.01" required /></div>
              <div class="line-total"><span>Thành tiền</span><strong>{{ formatCurrency(line.quantity * line.unitCost) }}</strong></div>
              <button class="icon-btn" type="button" title="Xóa dòng" :disabled="form.purchaseItems.length === 1" @click="removeLine(index)"><Trash2 :size="16" /></button>
              <div v-if="profitRate(line) !== null" class="profit-indicator" :class="{ warning: isLowProfit(line) }"><AlertTriangle v-if="isLowProfit(line)" :size="14" /><span>Lợi nhuận dự kiến: <strong>{{ formatProfit(line) }}%</strong><template v-if="isLowProfit(line)"> — dưới mức cảnh báo 20%</template></span></div>
            </div>
            <div class="purchase-total"><span>Tổng phiếu chi</span><strong>{{ formatCurrency(purchaseTotal) }}</strong></div>
          </div>
        </template>
        <template v-else>
          <div class="field"><label>Loại giao dịch *</label><div class="radio-group"><label><input v-model="form.type" type="radio" value="Income" /> Khoản thu</label><label><input v-model="form.type" type="radio" value="Expense" /> Khoản chi</label></div></div><div class="field"><label>Mã giao dịch</label><input v-model.trim="form.code" class="input" placeholder="Để trống để tạo tự động" /></div>
          <div class="field"><label>Ngày giao dịch *</label><input v-model="form.transactionDate" class="input" type="date" required /></div><div class="field"><label>Danh mục *</label><div class="category-picker"><AppSearchSelect v-model="form.cashCategoryId" :options="categoryOptions" required :clearable="false" placeholder="Chọn danh mục" /><button class="icon-btn" type="button" title="Quản lý danh mục" @click="categoryModal = true"><Tags :size="16" /></button></div></div>
          <div class="field"><label>Số tiền *</label><AppNumberInput v-model="form.amount" class="input" min="1" required /></div><div class="field"><label>Phương thức *</label><div class="radio-group"><label><input v-model="form.paymentMethod" type="radio" value="Cash" /> Tiền mặt</label><label><input v-model="form.paymentMethod" type="radio" value="BankTransfer" /> Chuyển khoản</label></div></div>
          <div class="field span-2"><label>Nội dung *</label><textarea v-model.trim="form.description" class="textarea" required /></div>
        </template>
        </template>
        <div v-else class="field span-2 transfer-proof">
          <label>Ảnh đính kèm <span class="muted">(tùy chọn)</span></label>
          <input ref="attachmentInput" class="visually-hidden" type="file" accept="image/*" capture="environment" @change="readAttachment" />
          <AppImageGallery v-if="form.attachmentUrl" :images="[form.attachmentUrl]" alt="Ảnh đính kèm" removable @remove="removeAttachment" />
          <button v-else type="button" class="upload-proof" @click="chooseAttachment"><ImagePlus :size="24" /><strong>Chụp hoặc chọn ảnh đính kèm</strong><span>Định dạng ảnh, tối đa 4 MB</span></button>
          <button v-if="form.attachmentUrl" type="button" class="btn btn-secondary btn-sm replace-proof" @click="chooseAttachment"><ImagePlus :size="14" /> Chọn ảnh khác</button>
        </div>
      </form>
      <template #footer><button class="btn btn-secondary" @click="modalOpen = false">Hủy</button><button class="btn btn-primary" form="cash-form" :disabled="saving || (form.purpose === 'PartsPurchase' && purchaseTotal <= 0)">{{ form.purpose === 'PartsPurchase' ? 'Tạo phiếu New' : 'Lưu giao dịch' }}</button></template>
    </AppModal>

    <AppModal :open="detailModal" :title="`Chi tiết phiếu ${selectedTransaction?.code || ''}`" width="860px" @close="detailModal = false">
      <template v-if="selectedTransaction">
        <div class="transaction-detail-grid">
          <div><span>Loại giao dịch</span><strong>{{ selectedTransaction.type === 'Income' ? 'Khoản thu' : 'Khoản chi' }}</strong></div>
          <div><span>Trạng thái</span><AppBadge :tone="selectedTransaction.status === 'New' ? 'warning' : 'success'">{{ selectedTransaction.status === 'New' ? 'New' : 'Đã xác nhận' }}</AppBadge></div>
          <div><span>Ngày giao dịch</span><strong>{{ formatDate(selectedTransaction.transactionDate, true) }}</strong></div>
          <div><span>Phương thức</span><strong>{{ paymentMethodName(selectedTransaction.paymentMethod) }}</strong></div>
          <div><span>Danh mục</span><strong><AppEntityLink :to="entityDetailRoute('CashCategory', selectedTransaction.cashCategoryId)">{{ selectedTransaction.category }}</AppEntityLink></strong></div>
          <div v-if="selectedTransaction.supplierId"><span>Nhà cung cấp</span><strong><AppEntityLink :to="entityDetailRoute('Supplier', selectedTransaction.supplierId)">{{ supplierName(selectedTransaction.supplierId) }}</AppEntityLink></strong></div>
          <div v-if="selectedTransaction.referenceId"><span>Tham chiếu</span><strong><AppEntityLink :to="entityDetailRoute(selectedTransaction.referenceType, selectedTransaction.referenceId)">{{ selectedTransaction.referenceType || 'Dữ liệu liên quan' }}</AppEntityLink></strong></div>
          <div class="detail-wide"><span>Nội dung</span><strong>{{ selectedTransaction.description }}</strong></div>
          <div><span>Ngày tạo</span><strong>{{ formatDate(selectedTransaction.createdAt, true) }}</strong></div>
          <div v-if="selectedTransaction.confirmedAt"><span>Ngày xác nhận</span><strong>{{ formatDate(selectedTransaction.confirmedAt, true) }}</strong></div>
        </div>

        <div v-if="selectedTransaction.purchaseItems?.length" class="detail-section">
          <h3>Phụ tùng nhập</h3>
          <div class="table-wrap"><table class="data-table"><thead><tr><th>Phụ tùng</th><th>Vị trí nhập</th><th class="text-right">Số lượng</th><th class="text-right">Giá nhập</th><th class="text-right">Giá bán</th><th class="text-right">Lợi nhuận</th><th class="text-right">Thành tiền</th></tr></thead><tbody><tr v-for="line in selectedTransaction.purchaseItems" :key="line.id || line.partId"><td><AppEntityLink :to="entityDetailRoute('Part', line.partId)"><span class="cell-main">{{ line.partName }}</span><span class="cell-sub mono">{{ line.partCode }}</span></AppEntityLink></td><td><AppEntityLink class="mono" :to="entityDetailRoute('WarehouseLocation', purchaseLocation(line)?.id)">{{ purchaseLocation(line)?.code || 'Không lưu vị trí' }}</AppEntityLink></td><td class="text-right">{{ line.quantity }}</td><td class="text-right">{{ formatCurrency(line.unitCost) }}</td><td class="text-right">{{ formatCurrency(line.salePriceSnapshot || 0) }}</td><td class="text-right" :class="line.isLowProfit ? 'expense' : 'income'">{{ (line.profitRate || 0).toFixed(2) }}%</td><td class="text-right cell-main">{{ formatCurrency(line.lineTotal || line.quantity * line.unitCost) }}</td></tr></tbody></table></div>
        </div>

        <div v-if="selectedTransaction.attachmentUrl" class="detail-section">
          <h3>Ảnh đính kèm</h3>
          <AppImageGallery :images="[selectedTransaction.attachmentUrl]" alt="Ảnh đính kèm" />
        </div>
        <div class="detail-total"><span>Tổng số tiền</span><strong :class="selectedTransaction.type === 'Income' ? 'income' : 'expense'">{{ selectedTransaction.type === 'Income' ? '+' : '-' }}{{ formatCurrency(selectedTransaction.amount) }}</strong></div>
      </template>
      <template #footer><button class="btn btn-secondary" @click="detailModal = false">Đóng</button><button v-if="selectedTransaction?.type === 'Expense' && selectedTransaction.status === 'New'" class="btn btn-primary" :disabled="confirming === selectedTransaction.id" @click="confirmVoucher(selectedTransaction)"><CheckCircle2 :size="15" /> Xác nhận phiếu</button></template>
    </AppModal>

    <AppModal :open="categoryModal" title="Danh mục thu chi" width="760px" @close="categoryModal = false">
      <div class="category-head"><p class="muted">Danh mục được dùng làm lựa chọn khi lập phiếu thu hoặc phiếu chi.</p><button class="btn btn-primary btn-sm" @click="openCategoryForm()"><Plus :size="14" /> Thêm danh mục</button></div>
      <div class="table-wrap"><table v-if="cashCategories.length" class="data-table"><thead><tr><th>Mã</th><th>Tên danh mục</th><th>Áp dụng</th><th>Trạng thái</th><th class="text-right">Thao tác</th></tr></thead><tbody><tr v-for="item in cashCategories" :key="item.id"><td class="mono">{{ item.code }}</td><td><AppEntityLink class="cell-main" :to="entityDetailRoute('CashCategory', item.id)">{{ item.name }}</AppEntityLink><div class="cell-sub">{{ item.description || '' }}</div></td><td>{{ item.scope === 'Income' ? 'Khoản thu' : item.scope === 'Expense' ? 'Khoản chi' : 'Thu và chi' }}</td><td><AppBadge :tone="item.isActive ? 'success' : 'neutral'">{{ item.isActive ? 'Đang dùng' : 'Tạm khóa' }}</AppBadge></td><td class="text-right"><div class="inline row-actions"><button class="icon-btn" title="Sửa" @click="openCategoryForm(item)"><Edit3 :size="15" /></button><button class="icon-btn danger-button" title="Xóa" @click="deleteCategory(item)"><Trash2 :size="15" /></button></div></td></tr></tbody></table><AppEmpty v-else :icon="Tags" title="Chưa có danh mục" message="Thêm danh mục trước khi ghi nhận thu chi." /></div>
    </AppModal>

    <AppModal :open="categoryFormModal" :title="editingCategory ? 'Cập nhật danh mục' : 'Thêm danh mục thu chi'" @close="categoryFormModal = false">
      <form id="category-form" class="form-grid" @submit.prevent="saveCategory">
        <div class="field"><label>Mã <span class="muted">(tự động)</span></label><input v-model.trim="categoryForm.code" class="input" placeholder="Ví dụ: DMTG-000001" /></div><div class="field"><label>Tên danh mục *</label><input v-model.trim="categoryForm.name" class="input" required /></div>
        <div class="field span-2"><label>Áp dụng cho *</label><div class="radio-group"><label><input v-model="categoryForm.scope" type="radio" value="Income" /> Khoản thu</label><label><input v-model="categoryForm.scope" type="radio" value="Expense" /> Khoản chi</label><label><input v-model="categoryForm.scope" type="radio" value="Both" /> Cả hai</label></div></div>
        <div class="field span-2"><label>Mô tả</label><textarea v-model.trim="categoryForm.description" class="textarea" /></div>
      </form>
      <template #footer><button class="btn btn-secondary" @click="categoryFormModal = false">Hủy</button><button class="btn btn-primary" form="category-form" :disabled="saving">Lưu danh mục</button></template>
    </AppModal>
  </div>
</template>

<style scoped>
.cash-summary { display: grid; grid-template-columns: repeat(3, 1fr); gap: 14px; }.cash-summary article { display: flex; align-items: center; gap: 13px; padding: 17px; border: 1px solid var(--line); border-radius: 13px; background: white; }.cash-summary span,.cash-summary strong { display: block; }.cash-summary span { color: var(--muted); font-size: 10px; }.cash-summary strong { color: var(--navy-950); font-size: 20px; }.income { color: var(--teal); }.expense { color: var(--red); }
.finance-filters { display: grid; grid-template-columns: minmax(260px, 1.7fr) repeat(3, minmax(150px, 1fr)); align-items: end; gap: 10px; padding: 14px; }.filter-search { position: relative; }.filter-search svg { position: absolute; top: 50%; left: 12px; color: var(--muted); transform: translateY(-50%); }.filter-search .input { padding-left: 38px; }.filter-date { display: grid; gap: 4px; }.filter-date label { color: var(--muted); font-size: 10px; font-weight: 700; }.filter-actions { display: flex; align-items: center; gap: 7px; }
.purchase-lines { display: grid; gap: 10px; }.line-head,.purchase-total { display: flex; align-items: center; justify-content: space-between; }.purchase-line { display: grid; grid-template-columns: minmax(210px, 2fr) 125px 95px 135px 130px 38px; align-items: end; gap: 9px; padding: 12px; border: 1px solid var(--line); border-radius: 11px; background: #f9fbfc; }.line-location { display: grid; min-height: 40px; grid-template-columns: 16px 1fr; align-content: center; column-gap: 5px; padding: 5px 8px; border-radius: 8px; color: #805b09; background: var(--amber-soft); }.line-location svg { grid-row: 1 / 3; align-self: center; }.line-location span { color: #876e35; font-size: 9px; }.line-location strong { font-size: 10px; }.line-location.missing { color: var(--red); background: #fff0ef; }.line-total { padding-bottom: 8px; text-align: right; }.line-total span,.line-total strong { display: block; }.line-total span { color: var(--muted); font-size: 10px; }.purchase-total { padding: 14px 4px 2px; }.purchase-total strong { color: var(--navy-950); font-size: 22px; }.profit-indicator { grid-column: 1 / -1; display: flex; align-items: center; gap: 6px; color: var(--teal); font-size: 11px; }.profit-indicator.warning,.low-profit-text { color: var(--red); }.low-profit-text { display: flex; align-items: center; gap: 4px; margin-top: 4px; font-size: 10px; }
.radio-group { display: flex; min-height: 42px; align-items: center; gap: 8px; flex-wrap: wrap; }.radio-group label { display: flex; min-height: 38px; align-items: center; gap: 7px; padding: 8px 12px; border: 1px solid var(--line); border-radius: 9px; color: var(--navy-900); background: white; cursor: pointer; }.radio-group label:has(input:checked) { border-color: var(--navy-900); background: #f0f5f8; }.radio-group input { accent-color: var(--navy-900); }.category-picker { display: grid; grid-template-columns: 1fr 40px; gap: 7px; }.visually-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; clip-path: inset(50%); }.transfer-proof { display: grid; gap: 9px; }.upload-proof { display: grid; min-height: 135px; place-items: center; gap: 5px; padding: 18px; border: 1px dashed #9eb2c2; border-radius: 12px; color: var(--navy-800); background: #f7fafc; }.upload-proof span { color: var(--muted); font-size: 10px; }.replace-proof { width: max-content; }.table-proof { margin-top: 5px; }.category-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 12px; }.category-head p { margin: 0; }.row-actions { justify-content: flex-end; }.danger-button { color: var(--red); }
.transaction-tabs { display: flex; gap: 5px; padding: 4px; border-radius: 11px; background: #eef2f5; }.transaction-tabs button { display: inline-flex; flex: 1; min-height: 38px; align-items: center; justify-content: center; gap: 7px; border: 0; border-radius: 8px; color: var(--muted); background: transparent; font-weight: 750; }.transaction-tabs button.active { color: var(--navy-950); background: white; box-shadow: 0 2px 7px rgb(10 31 51 / 8%); }.transaction-tabs span { display: grid; min-width: 20px; height: 20px; place-items: center; border-radius: 99px; color: var(--navy-900); background: var(--amber-soft); font-size: 9px; }
.transaction-detail-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; }.transaction-detail-grid > div { min-height: 66px; padding: 12px 14px; border: 1px solid var(--line); border-radius: 10px; background: #f9fbfc; }.transaction-detail-grid span,.transaction-detail-grid strong { display: block; }.transaction-detail-grid span { margin-bottom: 5px; color: var(--muted); font-size: 10px; }.transaction-detail-grid strong { color: var(--navy-950); font-size: 12px; }.transaction-detail-grid .detail-wide { grid-column: 1 / -1; }.detail-section { margin-top: 18px; }.detail-section h3 { margin: 0 0 9px; color: var(--navy-950); font-size: 13px; }.detail-total { display: flex; align-items: center; justify-content: space-between; margin-top: 18px; padding: 15px 17px; border-radius: 12px; background: var(--amber-soft); }.detail-total strong { font-size: 22px; }
@media (max-width: 1000px) { .finance-filters { grid-template-columns: repeat(2, minmax(0, 1fr)); }.filter-search { grid-column: 1 / -1; } }
@media (max-width: 760px) { .cash-summary,.finance-filters { grid-template-columns: 1fr; }.filter-search { grid-column: auto; }.purchase-line { grid-template-columns: 1fr; }.line-total { text-align: left; } }
</style>
