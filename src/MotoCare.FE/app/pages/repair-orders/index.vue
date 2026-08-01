<script setup lang="ts">
import { ClipboardList, Plus } from '@lucide/vue'
import type { PagedResult, RepairOrder, RepairOrderStatus } from '~/types/api'
import { formatCurrency, formatDate, formatNumber, statusLabel, statusTone } from '~/utils/format'

const api = useApi()
const result = ref<PagedResult<RepairOrder>>({ items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 })
const status = ref('')
const loading = ref(true)
const statuses: RepairOrderStatus[] = ['Received', 'Inspecting', 'AwaitingApproval', 'Repairing', 'AwaitingParts', 'Completed', 'Delivered', 'Cancelled']

const load = async (page = 1) => {
  loading.value = true
  try {
    result.value = await api.request('/repair-orders', { query: { status: status.value || undefined, page, pageSize: 20 } })
  } finally { loading.value = false }
}
watch(status, () => load(1))
onMounted(() => load())
</script>

<template>
  <div class="page">
    <div class="page-header">
      <div><h1 class="page-title">Phiếu sửa chữa</h1><p class="page-subtitle">Theo dõi toàn bộ vòng đời từ tiếp nhận đến bàn giao xe.</p></div>
      <NuxtLink class="btn btn-accent" to="/repair-orders/new"><Plus :size="17" /> Tạo phiếu sửa</NuxtLink>
    </div>
    <section class="card">
      <header class="card-header">
        <div class="inline">
          <span class="field-label">Trạng thái</span>
          <select v-model="status" class="select compact-select"><option value="">Tất cả</option><option v-for="item in statuses" :key="item" :value="item">{{ statusLabel(item) }}</option></select>
        </div>
        <span class="muted">{{ formatNumber(result.total) }} phiếu</span>
      </header>
      <div class="table-wrap">
        <table v-if="result.items.length" class="data-table">
          <thead><tr><th>Mã phiếu</th><th>Ngày nhận / hẹn giao</th><th>Yêu cầu khách hàng</th><th>Ưu tiên</th><th>Trạng thái</th><th class="text-right">Giá trị</th></tr></thead>
          <tbody><tr v-for="order in result.items" :key="order.id"><td><NuxtLink class="cell-link mono" :to="`/repair-orders/${order.id}`">{{ order.code }}</NuxtLink></td><td><div class="cell-main">{{ formatDate(order.receivedAt, true) }}</div><div class="cell-sub">Hẹn: {{ formatDate(order.expectedDeliveryAt, true) }}</div></td><td class="description-cell">{{ order.customerRequest }}</td><td><AppBadge :tone="order.priority === 'Urgent' ? 'danger' : order.priority === 'High' ? 'warning' : 'neutral'">{{ statusLabel(order.priority) }}</AppBadge></td><td><AppBadge :tone="statusTone(order.status)">{{ statusLabel(order.status) }}</AppBadge></td><td class="text-right cell-main">{{ formatCurrency(order.finalTotal) }}</td></tr></tbody>
        </table>
        <AppEmpty v-else-if="!loading" :icon="ClipboardList" title="Chưa có phiếu phù hợp" message="Thay đổi bộ lọc hoặc tạo phiếu tiếp nhận mới." />
        <div v-else class="card-body"><div class="loading-skeleton" style="height: 300px" /></div>
      </div>
      <AppPagination :page="result.page" :total-pages="result.totalPages" :total="result.total" @change="load" />
    </section>
  </div>
</template>

<style scoped>
.compact-select { width: 210px; }
.cell-link { color: var(--blue); font-weight: 800; }
.description-cell { max-width: 320px; overflow: hidden; text-overflow: ellipsis; }
@media (max-width: 560px) { .compact-select { width: 100%; }.card-header .inline { width: 100%; }.card-header .field-label { flex: 0 0 100%; } }
</style>
