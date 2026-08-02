<script setup lang="ts">
import { ArrowLeft, CalendarDays, CreditCard, MapPin, PackageSearch, ReceiptText, Truck, WalletCards } from '@lucide/vue'
import type { CashTransaction, PagedResult, Part, PurchaseExpenseItem, Supplier, WarehouseLocation } from '~/types/api'
import { entityDetailRoute } from '~/utils/entityRoute'
import { formatCurrency, formatDate, formatNumber } from '~/utils/format'

const route = useRoute()
const api = useApi()
const { mediaUrl } = useMedia()
const transaction = ref<CashTransaction>()
const suppliers = ref<Supplier[]>([])
const parts = ref<Part[]>([])
const warehouseLocations = ref<WarehouseLocation[]>([])
const loading = ref(true)

const transactionId = computed(() => String(route.params.id))
const supplier = computed(() => suppliers.value.find(item => item.id === transaction.value?.supplierId))
const attachmentUrl = computed(() => transaction.value?.attachmentUrl ? mediaUrl(transaction.value.attachmentUrl) : '')
const purchaseQuantity = computed(() => (transaction.value?.purchaseItems || []).reduce(
  (total, line) => total + Number(line.quantity || 0),
  0
))

const transactionTypeLabel = (type: CashTransaction['type']) => type === 'Income' ? 'Khoản thu' : 'Khoản chi'
const purposeLabel = (purpose: CashTransaction['purpose']) => purpose === 'PartsPurchase' ? 'Nhập phụ tùng' : 'Thu / chi khác'
const paymentMethodLabel = (method: string) => ({
  Cash: 'Tiền mặt',
  BankTransfer: 'Chuyển khoản',
  Card: 'Thẻ',
  EWallet: 'Ví điện tử'
}[method] || method || '—')
const statusLabel = (status: CashTransaction['status']) => ({
  New: 'Chờ xác nhận',
  Confirmed: 'Đã xác nhận',
  Approved: 'Đã ghi nhận',
  Cancelled: 'Đã hủy'
}[status] || status)
const statusTone = (status: CashTransaction['status']): 'success' | 'warning' | 'danger' | 'neutral' =>
  status === 'New' ? 'warning' : status === 'Cancelled' ? 'danger' : status === 'Confirmed' || status === 'Approved' ? 'success' : 'neutral'

const partForLine = (line: PurchaseExpenseItem) => parts.value.find(part => part.id === line.partId)
const locationIdForLine = (line: PurchaseExpenseItem) => line.warehouseLocationId
const locationForLine = (line: PurchaseExpenseItem) => warehouseLocations.value.find(
  location => location.id === locationIdForLine(line)
)
const partNameForLine = (line: PurchaseExpenseItem) => line.partName || partForLine(line)?.name || 'Phụ tùng không còn tồn tại'
const partCodeForLine = (line: PurchaseExpenseItem) => line.partCode || partForLine(line)?.code || line.partId
const salePriceForLine = (line: PurchaseExpenseItem) => line.salePriceSnapshot ?? partForLine(line)?.salePrice ?? 0
const profitRateForLine = (line: PurchaseExpenseItem) => {
  if (line.profitRate != null) return line.profitRate
  const salePrice = salePriceForLine(line)
  return line.unitCost > 0 ? (salePrice - line.unitCost) / line.unitCost * 100 : 0
}
const isLowProfitLine = (line: PurchaseExpenseItem) => line.isLowProfit ?? profitRateForLine(line) < 20
const lineTotal = (line: PurchaseExpenseItem) => line.lineTotal ?? line.quantity * line.unitCost

const load = async () => {
  loading.value = true
  try {
    transaction.value = await api.request<CashTransaction>(`/cash-transactions/${transactionId.value}`, { query: { includeDeleted: true } })
    const [supplierResult, partResult, locationResult] = await Promise.allSettled([
      api.request<PagedResult<Supplier>>('/suppliers', { query: { pageSize: 200, includeDeleted: true } }),
      api.request<PagedResult<Part>>('/parts', { query: { pageSize: 200, includeDeleted: true } }),
      api.request<PagedResult<WarehouseLocation>>('/warehouse-locations', { query: { pageSize: 500, includeDeleted: true } })
    ])
    suppliers.value = supplierResult.status === 'fulfilled' ? supplierResult.value.items : []
    parts.value = partResult.status === 'fulfilled' ? partResult.value.items : []
    warehouseLocations.value = locationResult.status === 'fulfilled' ? locationResult.value.items : []
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <NuxtLink class="back-link" to="/finance"><ArrowLeft :size="16" /> Sổ thu chi</NuxtLink>

    <template v-if="transaction">
      <div class="page-header">
        <div>
          <div class="title-line">
            <h1 class="page-title mono">{{ transaction.code }}</h1>
            <AppBadge :tone="statusTone(transaction.status)">{{ statusLabel(transaction.status) }}</AppBadge>
            <AppBadge v-if="transaction.isDeleted" tone="neutral">Đã xóa</AppBadge>
          </div>
          <p class="page-subtitle">{{ transaction.description }}</p>
        </div>
      </div>

      <section class="metric-grid">
        <article class="metric" :class="transaction.type === 'Income' ? 'income-metric' : 'expense-metric'"><WalletCards :size="20" /><div><span>Tổng số tiền</span><strong>{{ transaction.type === 'Income' ? '+' : '-' }}{{ formatCurrency(transaction.amount) }}</strong></div></article>
        <article class="metric"><ReceiptText :size="20" /><div><span>Loại phiếu</span><strong>{{ purposeLabel(transaction.purpose) }}</strong></div></article>
        <article class="metric"><PackageSearch :size="20" /><div><span>Phụ tùng nhập</span><strong>{{ formatNumber(transaction.purchaseItems?.length || 0) }} mặt hàng</strong><small>{{ formatNumber(purchaseQuantity) }} tổng số lượng</small></div></article>
        <article class="metric"><CalendarDays :size="20" /><div><span>Ngày giao dịch</span><strong>{{ formatDate(transaction.transactionDate) }}</strong></div></article>
      </section>

      <section class="card">
        <header class="card-header"><h2 class="card-title">Thông tin giao dịch</h2></header>
        <div class="card-body detail-grid">
          <div><span>Loại giao dịch</span><strong>{{ transactionTypeLabel(transaction.type) }}</strong></div>
          <div><span>Trạng thái</span><strong>{{ statusLabel(transaction.status) }}</strong></div>
          <div><span><CalendarDays :size="14" /> Ngày giao dịch</span><strong>{{ formatDate(transaction.transactionDate, true) }}</strong></div>
          <div><span><CreditCard :size="14" /> Phương thức</span><strong>{{ paymentMethodLabel(transaction.paymentMethod) }}</strong></div>
          <div><span>Danh mục</span><strong><AppEntityLink :to="entityDetailRoute('CashCategory', transaction.cashCategoryId)">{{ transaction.category || '—' }}</AppEntityLink></strong></div>
          <div v-if="transaction.supplierId"><span><Truck :size="14" /> Nhà cung cấp</span><strong><AppEntityLink :to="`/suppliers/${transaction.supplierId}`" icon>{{ supplier?.name || transaction.supplierId }}</AppEntityLink></strong></div>
          <div v-if="transaction.referenceId"><span>Tham chiếu</span><strong><AppEntityLink :to="entityDetailRoute(transaction.referenceType, transaction.referenceId)" icon>{{ transaction.referenceType || 'Dữ liệu liên quan' }}</AppEntityLink></strong></div>
          <div class="span-2"><span>Nội dung</span><strong>{{ transaction.description || '—' }}</strong></div>
          <div><span>Ngày tạo</span><strong>{{ formatDate(transaction.createdAt, true) }}</strong></div>
          <div><span>Ngày cập nhật</span><strong>{{ formatDate(transaction.updatedAt, true) }}</strong></div>
          <div v-if="transaction.confirmedAt"><span>Ngày xác nhận</span><strong>{{ formatDate(transaction.confirmedAt, true) }}</strong></div>
        </div>
      </section>

      <section v-if="transaction.purchaseItems?.length" class="card">
        <header class="card-header"><div><h2 class="card-title">Phụ tùng nhập kho</h2><span class="section-note">Chi tiết mặt hàng và vị trí nhận hàng</span></div><span class="muted">{{ formatNumber(transaction.purchaseItems.length) }} mặt hàng</span></header>
        <div class="table-wrap">
          <table class="data-table">
            <thead><tr><th>Phụ tùng</th><th>Vị trí nhập</th><th class="text-right">Số lượng</th><th class="text-right">Giá nhập</th><th class="text-right">Giá bán</th><th class="text-right">Lợi nhuận</th><th class="text-right">Thành tiền</th></tr></thead>
            <tbody>
              <tr v-for="line in transaction.purchaseItems" :key="line.id || `${line.partId}-${line.warehouseLocationId || ''}`">
                <td><AppEntityLink :to="line.partId ? `/inventory/${line.partId}` : undefined" block icon><span class="cell-main">{{ partNameForLine(line) }}</span><span class="cell-sub mono">{{ partCodeForLine(line) }}</span></AppEntityLink></td>
                <td>
                  <AppEntityLink v-if="locationIdForLine(line)" :to="`/warehouse-locations/${locationIdForLine(line)}`" block icon>
                    <span class="cell-main mono"><MapPin :size="13" /> {{ locationForLine(line)?.code || locationIdForLine(line) }}</span>
                    <span class="cell-sub">{{ locationForLine(line)?.name || 'Vị trí kho' }}</span>
                  </AppEntityLink>
                  <span v-else class="muted">Không lưu vị trí</span>
                </td>
                <td class="text-right">{{ formatNumber(line.quantity) }}</td>
                <td class="text-right">{{ formatCurrency(line.unitCost) }}</td>
                <td class="text-right">{{ formatCurrency(salePriceForLine(line)) }}</td>
                <td class="text-right profit" :class="isLowProfitLine(line) ? 'low' : 'good'">{{ profitRateForLine(line).toFixed(2) }}%</td>
                <td class="text-right cell-main">{{ formatCurrency(lineTotal(line)) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section v-if="attachmentUrl" class="card attachment-card">
        <header class="card-header"><h2 class="card-title">Ảnh đính kèm</h2></header>
        <div class="card-body"><AppImageGallery :images="[attachmentUrl]" alt="Ảnh đính kèm phiếu thu chi" /></div>
      </section>

      <section class="total-card" :class="transaction.type === 'Income' ? 'income-total' : 'expense-total'">
        <div><span>Tổng số tiền</span><small>{{ transactionTypeLabel(transaction.type) }} · {{ paymentMethodLabel(transaction.paymentMethod) }}</small></div>
        <strong>{{ transaction.type === 'Income' ? '+' : '-' }}{{ formatCurrency(transaction.amount) }}</strong>
      </section>
    </template>

    <div v-else-if="loading" class="loading-skeleton" style="height: 420px" />
    <AppEmpty v-else title="Không tìm thấy giao dịch" message="Phiếu thu chi không tồn tại hoặc bạn không có quyền xem dữ liệu này." />
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.title-line { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.metric-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; }
.metric { display: flex; min-width: 0; align-items: center; gap: 12px; padding: 17px 19px; border: 1px solid var(--line); border-radius: var(--radius-lg); color: var(--navy-800); background: white; box-shadow: var(--shadow-sm); }.metric span,.metric strong,.metric small { display: block; }.metric span,.metric small { color: var(--muted); font-size: 10px; }.metric strong { margin-top: 4px; overflow: hidden; color: var(--navy-950); font-size: 17px; text-overflow: ellipsis; }.metric small { margin-top: 2px; }.income-metric strong { color: var(--teal); }.expense-metric strong { color: var(--red); }
.detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }.detail-grid span,.detail-grid strong { display: block; }.detail-grid span { display: flex; align-items: center; gap: 5px; color: var(--muted); font-size: 11px; }.detail-grid strong { margin-top: 4px; color: var(--navy-950); }.span-2 { grid-column: span 2; }
.profit { font-weight: 800; }.profit.good { color: var(--teal); }.profit.low { color: var(--red); }.attachment-card { overflow: hidden; }
.total-card { display: flex; align-items: center; justify-content: space-between; gap: 18px; padding: 19px 22px; border-radius: var(--radius-lg); }.total-card span,.total-card small { display: block; }.total-card span { color: var(--navy-950); font-weight: 800; }.total-card small { margin-top: 3px; color: var(--muted); }.total-card strong { font-size: 25px; }.income-total { background: var(--teal-soft); }.income-total strong { color: var(--teal); }.expense-total { background: var(--amber-soft); }.expense-total strong { color: var(--red); }
@media (max-width: 1000px) { .metric-grid { grid-template-columns: repeat(2, 1fr); } }
@media (max-width: 640px) { .metric-grid,.detail-grid { grid-template-columns: 1fr; }.span-2 { grid-column: auto; }.total-card { align-items: flex-start; flex-direction: column; }.total-card strong { font-size: 21px; } }
</style>
