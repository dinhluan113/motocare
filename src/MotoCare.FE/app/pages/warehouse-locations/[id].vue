<script setup lang="ts">
import { ArrowLeft, Boxes, MapPin, PackageOpen, Warehouse } from '@lucide/vue'
import type { PagedResult, Part, WarehouseLocation } from '~/types/api'
import { formatCurrency, formatNumber } from '~/utils/format'

const route = useRoute()
const api = useApi()
const auth = useAuth()
const isEmployee = computed(() => auth.hasAnyRole('Employee'))
const location = ref<WarehouseLocation>()
const parts = ref<Part[]>([])
const loading = ref(true)
const locationId = computed(() => String(route.params.id))

const visibleParts = computed(() => [...parts.value]
  .filter(part => !part.isDeleted)
  .sort((a, b) => a.name.localeCompare(b.name, 'vi')))

const quantityAtLocation = (part: Part) => part.warehouseStocks?.find(stock =>
  stock.warehouseLocationId === locationId.value)?.quantityOnHand
  ?? (part.warehouseLocationId === locationId.value && !part.warehouseStocks?.length
    ? part.quantityOnHand
    : 0)

const totalQuantity = computed(() => visibleParts.value.reduce((total, part) =>
  total + quantityAtLocation(part), 0))
const totalStockValue = computed(() => visibleParts.value.reduce((total, part) =>
  total + quantityAtLocation(part) * (part.stockPrice || part.importPrice || 0), 0))

const loadParts = async () => {
  const firstPage = await api.request<PagedResult<Part>>('/parts', {
    query: { warehouseLocationId: locationId.value, page: 1, pageSize: 200 }
  })
  const remainingPages = await Promise.all(Array.from(
    { length: Math.max(0, firstPage.totalPages - 1) },
    (_, index) => api.request<PagedResult<Part>>('/parts', {
      query: { warehouseLocationId: locationId.value, page: index + 2, pageSize: 200 }
    })
  ))
  return [firstPage, ...remainingPages].flatMap(page => page.items)
}

const load = async () => {
  loading.value = true
  try {
    location.value = await api.request<WarehouseLocation>(`/warehouse-locations/${locationId.value}`, {
      query: { includeDeleted: true }
    })
    try {
      parts.value = await loadParts()
    } catch {
      parts.value = []
    }
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <NuxtLink class="back-link" :to="isEmployee ? '/inventory' : '/warehouse-locations'"><ArrowLeft :size="16" /> {{ isEmployee ? 'Kho phụ tùng' : 'Quay lại quản lý kho' }}</NuxtLink>

    <template v-if="location">
      <div class="page-header">
        <div>
          <div class="title-line"><h1 class="page-title">{{ location.name }}</h1><AppBadge :tone="location.isDeleted || !location.isActive ? 'neutral' : 'success'">{{ location.isDeleted ? 'Đã xóa' : location.isActive ? 'Đang sử dụng' : 'Tạm khóa' }}</AppBadge></div>
          <p class="page-subtitle"><span class="mono">{{ location.code }}</span> · Kệ {{ location.rack }} · Tầng {{ location.level }} · Ngăn {{ location.bin }}</p>
        </div>
      </div>

      <div class="metric-grid">
        <article class="metric"><MapPin :size="19" /><div><span>Mã vị trí</span><strong class="mono">{{ location.code }}</strong></div></article>
        <article class="metric"><Boxes :size="19" /><div><span>Số loại phụ tùng</span><strong>{{ formatNumber(visibleParts.length) }}</strong></div></article>
        <article class="metric"><PackageOpen :size="19" /><div><span>Tổng số lượng tại ngăn</span><strong>{{ formatNumber(totalQuantity) }}</strong></div></article>
        <article class="metric"><Warehouse :size="19" /><div><span>Giá trị tồn ước tính</span><strong>{{ formatCurrency(totalStockValue) }}</strong></div></article>
      </div>

      <section class="card location-summary">
        <div><span>Kệ</span><strong>{{ location.rack }}</strong></div>
        <div><span>Tầng</span><strong>{{ location.level }}</strong></div>
        <div><span>Ngăn / ô</span><strong>{{ location.bin }}</strong></div>
        <div><span>Ghi chú</span><strong>{{ location.description || 'Không có ghi chú' }}</strong></div>
      </section>

      <section class="card">
        <header class="card-header"><div><h2>Phụ tùng trong ngăn</h2><span class="section-note">Số lượng thực tế được phân bổ tại {{ location.code }}</span></div><span class="muted">{{ formatNumber(visibleParts.length) }} mặt hàng</span></header>
        <div v-if="visibleParts.length" class="table-wrap">
          <table class="data-table">
            <thead><tr><th>Mã phụ tùng</th><th>Tên phụ tùng</th><th>Đơn vị</th><th class="text-right">Tồn tại ngăn</th><th class="text-right">Tổng tồn</th><th class="text-right">Giá vốn</th><th class="text-right">Thành tiền</th><th class="text-right">Thao tác</th></tr></thead>
            <tbody>
              <tr v-for="part in visibleParts" :key="part.id">
                <td class="mono"><NuxtLink :to="`/inventory/${part.id}`">{{ part.code }}</NuxtLink></td>
                <td class="cell-main"><NuxtLink :to="`/inventory/${part.id}`">{{ part.name }}</NuxtLink></td>
                <td>{{ part.unit }}</td>
                <td class="text-right"><strong>{{ formatNumber(quantityAtLocation(part)) }}</strong></td>
                <td class="text-right">{{ formatNumber(part.quantityOnHand) }}</td>
                <td class="text-right">{{ formatCurrency(part.stockPrice || part.importPrice || 0) }}</td>
                <td class="text-right cell-main">{{ formatCurrency(quantityAtLocation(part) * (part.stockPrice || part.importPrice || 0)) }}</td>
                <td class="text-right"><NuxtLink class="btn btn-secondary btn-sm" :to="`/inventory/${part.id}`">Xem chi tiết</NuxtLink></td>
              </tr>
            </tbody>
          </table>
        </div>
        <AppEmpty v-else title="Ngăn đang trống" message="Chưa có phụ tùng nào được phân bổ tại vị trí này." />
      </section>
    </template>

    <div v-else-if="loading" class="loading-skeleton" style="height: 420px" />
    <AppEmpty v-else :icon="Warehouse" title="Không tìm thấy vị trí kho" message="Vị trí không tồn tại hoặc bạn không có quyền xem dữ liệu này." />
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.title-line { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.metric-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; }
.metric { display: flex; align-items: center; gap: 12px; padding: 17px 19px; border: 1px solid var(--line); border-radius: var(--radius-lg); color: var(--navy-800); background: white; box-shadow: var(--shadow-sm); }
.metric span,.metric strong { display: block; }.metric span { color: var(--muted); font-size: 11px; }.metric strong { margin-top: 4px; color: var(--navy-950); font-size: 18px; }
.location-summary { display: grid; grid-template-columns: repeat(3, minmax(100px, .45fr)) minmax(220px, 2fr); gap: 18px; padding: 18px; }
.location-summary span,.location-summary strong { display: block; }.location-summary span { color: var(--muted); font-size: 11px; }.location-summary strong { margin-top: 4px; color: var(--navy-950); }
.data-table a:not(.btn):hover { color: var(--teal); text-decoration: underline; }
@media (max-width: 1000px) { .metric-grid { grid-template-columns: repeat(2, 1fr); }.location-summary { grid-template-columns: repeat(3, 1fr); }.location-summary > div:last-child { grid-column: span 3; } }
@media (max-width: 560px) { .metric-grid,.location-summary { grid-template-columns: 1fr; }.location-summary > div:last-child { grid-column: auto; } }
</style>
