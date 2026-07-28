<script setup lang="ts">
import { ReceiptText } from '@lucide/vue'
import type { Invoice, InvoicePaymentStatus, PagedResult } from '~/types/api'
import { formatCurrency, formatDate, formatNumber, statusLabel, statusTone } from '~/utils/format'

const api = useApi()
const result = ref<PagedResult<Invoice>>({ items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 })
const status = ref('')
const loading = ref(true)
const statuses: InvoicePaymentStatus[] = ['Unpaid', 'PartiallyPaid', 'Paid', 'Refunded', 'Cancelled']
const load = async (page = 1) => {
  loading.value = true
  try { result.value = await api.request('/invoices', { query: { status: status.value || undefined, page, pageSize: 20 } }) }
  finally { loading.value = false }
}
watch(status, () => load(1))
onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-header"><div><h1 class="page-title">Hóa đơn</h1><p class="page-subtitle">Theo dõi công nợ, thanh toán và in hóa đơn bán hàng.</p></div></div>
    <section class="card">
      <header class="card-header"><div class="inline"><span class="field-label">Thanh toán</span><select v-model="status" class="select status-select"><option value="">Tất cả</option><option v-for="item in statuses" :key="item" :value="item">{{ statusLabel(item) }}</option></select></div><span class="muted">{{ formatNumber(result.total) }} hóa đơn</span></header>
      <div class="table-wrap">
        <table v-if="result.items.length" class="data-table">
          <thead><tr><th>Hóa đơn</th><th>Khách hàng</th><th>Ngày lập</th><th>Trạng thái</th><th class="text-right">Tổng tiền</th><th class="text-right">Còn nợ</th></tr></thead>
          <tbody><tr v-for="invoice in result.items" :key="invoice.id"><td><NuxtLink class="cell-link mono" :to="`/invoices/${invoice.id}`">{{ invoice.code }}</NuxtLink><div class="cell-sub mono">Phiếu {{ invoice.repairOrderId.slice(-8) }}</div></td><td><div class="cell-main">{{ invoice.customerName }}</div><div class="cell-sub">{{ invoice.customerPhone }}</div></td><td>{{ formatDate(invoice.issueDate, true) }}</td><td><AppBadge :tone="statusTone(invoice.paymentStatus)">{{ statusLabel(invoice.paymentStatus) }}</AppBadge></td><td class="text-right cell-main">{{ formatCurrency(invoice.totalAmount) }}</td><td class="text-right" :class="{ debt: invoice.remainingAmount > 0 }">{{ formatCurrency(invoice.remainingAmount) }}</td></tr></tbody>
        </table>
        <AppEmpty v-else-if="!loading" :icon="ReceiptText" title="Chưa có hóa đơn" message="Hóa đơn được tạo từ chi tiết phiếu sửa chữa." />
        <div v-else class="card-body"><div class="loading-skeleton" style="height: 280px" /></div>
      </div>
      <AppPagination :page="result.page" :total-pages="result.totalPages" :total="result.total" @change="load" />
    </section>
  </div>
</template>

<style scoped>
.status-select { width: 220px; }
.cell-link { color: var(--blue); font-weight: 800; }
.debt { color: var(--red); font-weight: 800; }
</style>
