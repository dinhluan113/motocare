<script setup lang="ts">
import { ArrowLeft, Boxes, CalendarDays, MapPin, PackageSearch, Phone, ReceiptText, Truck } from '@lucide/vue'
import type { CashTransaction, PagedResult, Supplier } from '~/types/api'
import { formatCurrency, formatDate, formatNumber } from '~/utils/format'

interface SupplierStock {
  id: string
  name: string
  totalQuantityOnHand: number
  items: Array<{
    partId: string
    partCode: string
    partName: string
    quantityOnHand: number
    lastUnitCost: number
    lastReceiptAt?: string
  }>
}

const route = useRoute()
const api = useApi()
const auth = useAuth()
const isEmployee = computed(() => auth.hasAnyRole('Employee'))
const supplier = ref<Supplier>()
const stock = ref<SupplierStock>()
const purchaseVouchers = ref<CashTransaction[]>([])
const loading = ref(true)

const supplierId = computed(() => String(route.params.id))
const totalStockValue = computed(() => (stock.value?.items || []).reduce(
  (total, item) => total + item.quantityOnHand * item.lastUnitCost,
  0
))
const confirmedVoucherCount = computed(() => purchaseVouchers.value.filter(
  voucher => voucher.status === 'Confirmed' || voucher.status === 'Approved'
).length)

const voucherStatusLabel = (status: CashTransaction['status']) => ({
  New: 'Chờ xác nhận',
  Confirmed: 'Đã xác nhận',
  Approved: 'Đã ghi nhận',
  Cancelled: 'Đã hủy'
}[status] || status)
const voucherStatusTone = (status: CashTransaction['status']): 'success' | 'warning' | 'danger' | 'neutral' =>
  status === 'New' ? 'warning' : status === 'Cancelled' ? 'danger' : status === 'Confirmed' || status === 'Approved' ? 'success' : 'neutral'

const loadPurchaseVouchers = async () => {
  if (isEmployee.value) return []
  const firstPage = await api.request<PagedResult<CashTransaction>>('/cash-transactions', {
    query: { supplierId: supplierId.value, page: 1, pageSize: 200 }
  })
  const remainingPages = await Promise.all(Array.from(
    { length: Math.max(0, firstPage.totalPages - 1) },
    (_, index) => api.request<PagedResult<CashTransaction>>('/cash-transactions', {
      query: { supplierId: supplierId.value, page: index + 2, pageSize: 200 }
    })
  ))
  return [firstPage, ...remainingPages].flatMap(page => page.items)
}

const load = async () => {
  loading.value = true
  try {
    const currentSupplier = await api.request<Supplier>(`/suppliers/${supplierId.value}`, { query: { includeDeleted: true } })
    supplier.value = currentSupplier
    const [stockResult, transactionResult] = await Promise.allSettled([
      api.request<SupplierStock>(`/suppliers/${supplierId.value}/stock`, {
        query: { includeDeleted: true }
      }),
      loadPurchaseVouchers()
    ])
    stock.value = stockResult.status === 'fulfilled'
      ? stockResult.value
      : { id: currentSupplier.id, name: currentSupplier.name, totalQuantityOnHand: 0, items: [] }
    purchaseVouchers.value = (transactionResult.status === 'fulfilled' ? transactionResult.value : [])
      .sort((left, right) => new Date(right.transactionDate).getTime() - new Date(left.transactionDate).getTime())
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <NuxtLink class="back-link" :to="isEmployee ? '/inventory' : '/suppliers'"><ArrowLeft :size="16" /> {{ isEmployee ? 'Kho phụ tùng' : 'Danh sách nhà cung cấp' }}</NuxtLink>

    <template v-if="supplier">
      <div class="page-header">
        <div>
          <div class="title-line">
            <h1 class="page-title">{{ supplier.name }}</h1>
            <AppBadge :tone="supplier.isDeleted || !supplier.isActive ? 'neutral' : 'success'">
              {{ supplier.isDeleted ? 'Đã xóa' : supplier.isActive ? 'Đang hợp tác' : 'Tạm khóa' }}
            </AppBadge>
          </div>
          <p class="page-subtitle mono">{{ supplier.code }}</p>
        </div>
      </div>

      <section class="metric-grid">
        <article class="metric"><Boxes :size="20" /><div><span>Số loại phụ tùng</span><strong>{{ formatNumber(stock?.items.length || 0) }}</strong></div></article>
        <article class="metric"><PackageSearch :size="20" /><div><span>Tổng tồn từ nhà cung cấp</span><strong>{{ formatNumber(stock?.totalQuantityOnHand || 0) }}</strong></div></article>
        <article class="metric"><ReceiptText :size="20" /><div><span>Phiếu nhập</span><strong>{{ formatNumber(purchaseVouchers.length) }}</strong><small>{{ formatNumber(confirmedVoucherCount) }} phiếu đã xác nhận</small></div></article>
        <article class="metric"><Truck :size="20" /><div><span>Giá trị tồn ước tính</span><strong>{{ formatCurrency(totalStockValue) }}</strong></div></article>
      </section>

      <section class="card">
        <header class="card-header"><h2 class="card-title">Thông tin nhà cung cấp</h2></header>
        <div class="card-body detail-grid">
          <div><span>Mã nhà cung cấp</span><strong class="mono">{{ supplier.code }}</strong></div>
          <div><span><Phone :size="14" /> Số điện thoại</span><strong>{{ supplier.phone || '—' }}</strong></div>
          <div><span>Mã số thuế</span><strong class="mono">{{ supplier.taxCode || '—' }}</strong></div>
          <div><span>Trạng thái</span><strong>{{ supplier.isActive && !supplier.isDeleted ? 'Đang hoạt động' : 'Ngừng hoạt động' }}</strong></div>
          <div class="span-2"><span><MapPin :size="14" /> Địa chỉ</span><strong>{{ supplier.address || '—' }}</strong></div>
          <div class="span-2"><span>Ghi chú</span><strong>{{ supplier.notes || '—' }}</strong></div>
        </div>
      </section>

      <section class="card">
        <header class="card-header"><div><h2 class="card-title">Phụ tùng đang tồn</h2><span class="section-note">Các mặt hàng đã nhập từ nhà cung cấp này</span></div><span class="muted">{{ formatNumber(stock?.items.length || 0) }} mặt hàng</span></header>
        <div v-if="stock?.items.length" class="table-wrap">
          <table class="data-table">
            <thead><tr><th>Phụ tùng</th><th class="text-right">Tồn hiện tại</th><th class="text-right">Giá nhập gần nhất</th><th class="text-right">Giá trị tồn</th><th>Lần nhập gần nhất</th></tr></thead>
            <tbody>
              <tr v-for="item in stock.items" :key="item.partId">
                <td><AppEntityLink :to="`/inventory/${item.partId}`" block icon><span class="cell-main">{{ item.partName }}</span><span class="cell-sub mono">{{ item.partCode }}</span></AppEntityLink></td>
                <td class="text-right cell-main">{{ formatNumber(item.quantityOnHand) }}</td>
                <td class="text-right">{{ formatCurrency(item.lastUnitCost) }}</td>
                <td class="text-right cell-main">{{ formatCurrency(item.quantityOnHand * item.lastUnitCost) }}</td>
                <td>{{ item.lastReceiptAt ? formatDate(item.lastReceiptAt, true) : '—' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <AppEmpty v-else title="Chưa có tồn kho" message="Nhà cung cấp này chưa có phụ tùng được nhập vào kho." />
      </section>

      <section class="card">
        <header class="card-header"><div><h2 class="card-title">Lịch sử phiếu nhập</h2><span class="section-note">Các phiếu thu chi gắn với nhà cung cấp</span></div><span class="muted">{{ formatNumber(purchaseVouchers.length) }} phiếu</span></header>
        <div v-if="purchaseVouchers.length" class="table-wrap">
          <table class="data-table">
            <thead><tr><th>Mã phiếu</th><th>Ngày nhập</th><th>Nội dung</th><th>Trạng thái</th><th class="text-right">Mặt hàng</th><th class="text-right">Tổng tiền</th></tr></thead>
            <tbody>
              <tr v-for="voucher in purchaseVouchers" :key="voucher.id">
                <td><AppEntityLink :to="`/finance/${voucher.id}`" icon><strong class="mono">{{ voucher.code }}</strong></AppEntityLink></td>
                <td><span class="date-cell"><CalendarDays :size="14" /> {{ formatDate(voucher.transactionDate, true) }}</span></td>
                <td>{{ voucher.description || voucher.category }}</td>
                <td><AppBadge :tone="voucherStatusTone(voucher.status)">{{ voucherStatusLabel(voucher.status) }}</AppBadge></td>
                <td class="text-right">{{ formatNumber(voucher.purchaseItems?.length || 0) }}</td>
                <td class="text-right cell-main">{{ formatCurrency(voucher.amount) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <AppEmpty v-else title="Chưa có phiếu nhập" message="Chưa ghi nhận phiếu nhập nào từ nhà cung cấp này." />
      </section>
    </template>

    <div v-else-if="loading" class="loading-skeleton" style="height: 420px" />
    <AppEmpty v-else title="Không tìm thấy nhà cung cấp" message="Nhà cung cấp không tồn tại hoặc bạn không có quyền xem dữ liệu này." />
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.title-line { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.metric-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; }
.metric { display: flex; min-width: 0; align-items: center; gap: 12px; padding: 17px 19px; border: 1px solid var(--line); border-radius: var(--radius-lg); color: var(--navy-800); background: white; box-shadow: var(--shadow-sm); }
.metric span,.metric strong,.metric small { display: block; }.metric span,.metric small { color: var(--muted); font-size: 10px; }.metric strong { margin-top: 4px; overflow: hidden; color: var(--navy-950); font-size: 18px; text-overflow: ellipsis; }.metric small { margin-top: 2px; }
.detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }.detail-grid span,.detail-grid strong { display: block; }.detail-grid span { display: flex; align-items: center; gap: 5px; color: var(--muted); font-size: 11px; }.detail-grid strong { margin-top: 4px; color: var(--navy-950); }.span-2 { grid-column: span 2; }
.date-cell { display: inline-flex; align-items: center; gap: 5px; white-space: nowrap; }
@media (max-width: 1000px) { .metric-grid { grid-template-columns: repeat(2, 1fr); } }
@media (max-width: 640px) { .metric-grid,.detail-grid { grid-template-columns: 1fr; }.span-2 { grid-column: auto; } }
</style>
