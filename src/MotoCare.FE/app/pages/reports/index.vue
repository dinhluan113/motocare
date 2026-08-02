<script setup lang="ts">
import { BarChart3, Download, RefreshCw, TrendingUp } from '@lucide/vue'
import type { LoyaltyTier } from '~/types/api'
import { entityDetailRoute } from '~/utils/entityRoute'
import { formatCurrency, formatDate, formatNumber } from '~/utils/format'

interface RevenueRow { period: string, revenue: number, collected: number, outstanding: number, discount: number, invoiceCount: number }
interface TopPart { partId?: string, description: string, quantity: number, revenue: number, invoiceCount: number }
interface TopVehicle { vehicleId: string, licensePlate: string, repairCount: number, totalValue: number, lastRepairAt: string }
interface LoyalCustomer { customerId: string, customerCode: string, fullName: string, phone: string, tierCode: string, eligibleSpend: number, availablePoints: number }

const api = useApi()
const loading = ref(true)
const from = ref(new Date(new Date().setMonth(new Date().getMonth() - 6)).toISOString().slice(0, 10))
const to = ref(new Date().toISOString().slice(0, 10))
const groupBy = ref('month')
const revenue = ref<RevenueRow[]>([])
const parts = ref<TopPart[]>([])
const vehicles = ref<TopVehicle[]>([])
const customers = ref<LoyalCustomer[]>([])
const tiers = ref<LoyaltyTier[]>([])
const activeTab = ref<'parts' | 'vehicles' | 'customers'>('parts')

const query = computed(() => ({ from: from.value, to: `${to.value}T23:59:59`, groupBy: groupBy.value }))
const load = async () => {
  loading.value = true
  try {
    const [r, p, v, c, t] = await Promise.all([
      api.request<RevenueRow[]>('/reports/revenue', { query: query.value }),
      api.request<TopPart[]>('/reports/top-parts', { query: { from: from.value, to: `${to.value}T23:59:59`, limit: 10 } }),
      api.request<TopVehicle[]>('/reports/top-vehicles', { query: { from: from.value, to: `${to.value}T23:59:59`, limit: 10 } }),
      api.request<LoyalCustomer[]>('/reports/loyal-customers?limit=10'),
      api.request<LoyaltyTier[]>('/loyalty/tiers')
    ])
    revenue.value = r; parts.value = p; vehicles.value = v; customers.value = c; tiers.value = t
  } finally { loading.value = false }
}
const maxRevenue = computed(() => Math.max(...revenue.value.map(x => x.revenue), 1))
const totals = computed(() => revenue.value.reduce((acc, x) => ({
  revenue: acc.revenue + x.revenue, collected: acc.collected + x.collected,
  outstanding: acc.outstanding + x.outstanding, invoices: acc.invoices + x.invoiceCount
}), { revenue: 0, collected: 0, outstanding: 0, invoices: 0 }))
const exportReport = (report: string) => api.download(`/reports/export?report=${report}&from=${from.value}&to=${to.value}T23:59:59`, `motocare-${report}-${to.value}.xlsx`)
const tierByCode = (code?: string) => tiers.value.find(tier => tier.code === code)
onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-header"><div><h1 class="page-title">Báo cáo & phân tích</h1><p class="page-subtitle">Theo dõi doanh thu, hiệu suất sửa chữa và hành vi khách hàng.</p></div><button class="btn btn-secondary" :disabled="loading" @click="load"><RefreshCw :size="16" /> Làm mới</button></div>
    <section class="filter-bar card"><div class="field"><label>Từ ngày</label><input v-model="from" class="input" type="date" /></div><div class="field"><label>Đến ngày</label><input v-model="to" class="input" type="date" /></div><div class="field"><label>Nhóm doanh thu</label><select v-model="groupBy" class="select"><option value="week">Theo tuần</option><option value="month">Theo tháng</option><option value="quarter">Theo quý</option></select></div><button class="btn btn-primary" @click="load">Áp dụng</button></section>
    <section class="report-metrics"><MetricCard label="Tổng doanh thu" :value="formatCurrency(totals.revenue)" :detail="`${formatNumber(totals.invoices)} hóa đơn`" tone="navy" :icon="TrendingUp" /><MetricCard label="Đã thu" :value="formatCurrency(totals.collected)" detail="Trong kỳ báo cáo" tone="teal" :icon="BarChart3" /><MetricCard label="Còn phải thu" :value="formatCurrency(totals.outstanding)" detail="Công nợ trong kỳ" tone="amber" :icon="TrendingUp" /></section>
    <section class="card">
      <header class="card-header"><div><h2 class="card-title">Doanh thu theo kỳ</h2><span class="section-note">{{ from }} — {{ to }}</span></div><button class="btn btn-secondary btn-sm" @click="exportReport('revenue')"><Download :size="15" /> Xuất Excel</button></header>
      <div v-if="revenue.length" class="chart">
        <div v-for="item in revenue" :key="item.period" class="chart-column"><div class="chart-value">{{ formatCurrency(item.revenue) }}</div><div class="bar-track"><div class="bar" :style="{ height: `${Math.max(5, item.revenue / maxRevenue * 100)}%` }" /></div><strong>{{ item.period }}</strong><span>{{ item.invoiceCount }} HĐ</span></div>
      </div>
      <AppEmpty v-else-if="!loading" :icon="BarChart3" title="Chưa có dữ liệu doanh thu" message="Chọn khoảng ngày khác hoặc ghi nhận hóa đơn thanh toán." />
      <div v-else class="card-body"><div class="loading-skeleton" style="height: 290px" /></div>
    </section>
    <section class="card">
      <header class="card-header"><div class="report-tabs"><button :class="{ active: activeTab === 'parts' }" @click="activeTab = 'parts'">Phụ tùng bán chạy</button><button :class="{ active: activeTab === 'vehicles' }" @click="activeTab = 'vehicles'">Xe sửa nhiều</button><button :class="{ active: activeTab === 'customers' }" @click="activeTab = 'customers'">Khách thân thiết</button></div><button class="btn btn-secondary btn-sm" @click="exportReport(activeTab === 'parts' ? 'top-parts' : activeTab === 'vehicles' ? 'top-vehicles' : 'loyal-customers')"><Download :size="15" /> Xuất Excel</button></header>
      <div class="table-wrap">
        <table v-if="activeTab === 'parts'" class="data-table"><thead><tr><th>#</th><th>Phụ tùng</th><th class="text-right">Số lượng</th><th class="text-right">Số hóa đơn</th><th class="text-right">Doanh thu</th></tr></thead><tbody><tr v-for="(item, i) in parts" :key="`${item.partId}-${i}`"><td>{{ i + 1 }}</td><td><AppEntityLink class="cell-main" :to="entityDetailRoute('Part', item.partId)">{{ item.description }}</AppEntityLink></td><td class="text-right">{{ formatNumber(item.quantity) }}</td><td class="text-right">{{ item.invoiceCount }}</td><td class="text-right cell-main">{{ formatCurrency(item.revenue) }}</td></tr></tbody></table>
        <table v-else-if="activeTab === 'vehicles'" class="data-table"><thead><tr><th>#</th><th>Biển số</th><th class="text-right">Lượt sửa</th><th>Lần gần nhất</th><th class="text-right">Tổng giá trị</th></tr></thead><tbody><tr v-for="(item, i) in vehicles" :key="item.vehicleId"><td>{{ i + 1 }}</td><td><AppEntityLink class="cell-main mono" :to="entityDetailRoute('Vehicle', item.vehicleId)">{{ item.licensePlate }}</AppEntityLink></td><td class="text-right">{{ item.repairCount }}</td><td>{{ formatDate(item.lastRepairAt) }}</td><td class="text-right cell-main">{{ formatCurrency(item.totalValue) }}</td></tr></tbody></table>
        <table v-else class="data-table"><thead><tr><th>#</th><th>Khách hàng</th><th>Hạng</th><th class="text-right">Chi tiêu</th><th class="text-right">Điểm</th></tr></thead><tbody><tr v-for="(item, i) in customers" :key="item.customerId"><td>{{ i + 1 }}</td><td><NuxtLink class="cell-main customer-link" :to="`/customers/${item.customerId}`">{{ item.fullName }}</NuxtLink><div class="cell-sub">{{ item.phone }}</div></td><td><AppEntityLink :to="entityDetailRoute('LoyaltyTier', tierByCode(item.tierCode)?.id)"><AppBadge tone="warning">{{ item.tierCode }}</AppBadge></AppEntityLink></td><td class="text-right">{{ formatCurrency(item.eligibleSpend) }}</td><td class="text-right cell-main">{{ formatNumber(item.availablePoints) }}</td></tr></tbody></table>
      </div>
    </section>
  </div>
</template>

<style scoped>
.filter-bar { display: grid; grid-template-columns: 1fr 1fr 1fr auto; align-items: end; gap: 14px; padding: 16px; }
.report-metrics { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; }
.section-note { display: block; margin-top: 3px; color: var(--muted); font-size: 11px; }
.chart { display: flex; min-height: 330px; align-items: flex-end; gap: 12px; overflow-x: auto; padding: 28px 24px 20px; }
.chart-column { display: grid; min-width: 72px; flex: 1; gap: 5px; text-align: center; }
.chart-value { overflow: hidden; color: var(--muted); font-size: 9px; text-overflow: ellipsis; white-space: nowrap; }
.bar-track { display: flex; height: 220px; align-items: flex-end; justify-content: center; border-bottom: 1px solid var(--line); background: repeating-linear-gradient(to top, #edf1f4 0 1px, transparent 1px 55px); }
.bar { width: min(42px, 70%); min-height: 5px; border-radius: 7px 7px 2px 2px; background: linear-gradient(to top, var(--navy-800), var(--blue)); transition: height .4s ease; }
.chart-column strong { color: var(--navy-950); font-size: 11px; }
.chart-column span { color: var(--muted); font-size: 9px; }
.report-tabs { display: flex; gap: 5px; }
.report-tabs button { padding: 8px 11px; border: 0; border-radius: 8px; color: var(--muted); background: transparent; font-weight: 700; }
.report-tabs button.active { color: var(--navy-950); background: var(--amber-soft); }
.customer-link:hover { color: var(--blue); }
@media (max-width: 850px) { .filter-bar, .report-metrics { grid-template-columns: 1fr; } .report-tabs { overflow-x: auto; } }
</style>
