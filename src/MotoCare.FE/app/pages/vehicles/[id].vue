<script setup lang="ts">
import { ArrowLeft, Bike, CalendarDays, ClipboardList, Gauge, History, UserRound } from '@lucide/vue'
import type { Customer, PagedResult, RepairOrder, Vehicle, VehicleBrand, VehicleModel } from '~/types/api'
import { formatCurrency, formatDate, formatNumber, statusLabel, statusTone } from '~/utils/format'

const route = useRoute()
const api = useApi()
const vehicle = ref<Vehicle>()
const customer = ref<Customer>()
const model = ref<VehicleModel>()
const brand = ref<VehicleBrand>()
const repairOrders = ref<RepairOrder[]>([])
const loading = ref(true)

const vehicleId = computed(() => String(route.params.id))
const completedOrderCount = computed(() => repairOrders.value.filter(order =>
  order.status === 'Completed' || order.status === 'Delivered').length)
const totalRepairValue = computed(() => repairOrders.value.reduce((total, order) =>
  total + order.finalTotal, 0))
const latestOrder = computed(() => repairOrders.value[0])

const loadRepairOrders = async (id: string) => {
  const firstPage = await api.request<PagedResult<RepairOrder>>('/repair-orders', {
    query: { vehicleId: id, page: 1, pageSize: 200 }
  })
  const remainingPages = await Promise.all(Array.from(
    { length: Math.max(0, firstPage.totalPages - 1) },
    (_, index) => api.request<PagedResult<RepairOrder>>('/repair-orders', {
      query: { vehicleId: id, page: index + 2, pageSize: 200 }
    })
  ))
  return [firstPage, ...remainingPages].flatMap(page => page.items)
}

const load = async () => {
  loading.value = true
  try {
    const currentVehicle = await api.request<Vehicle>(`/vehicles/${vehicleId.value}`, {
      query: { includeDeleted: true }
    })
    vehicle.value = currentVehicle

    const [currentCustomer, currentModel, currentOrders] = await Promise.all([
      api.request<Customer>(`/customers/${currentVehicle.customerId}`, {
        query: { includeDeleted: true }
      }),
      currentVehicle.vehicleModelId
        ? api.request<VehicleModel>(`/vehicle-models/${currentVehicle.vehicleModelId}`, {
            query: { includeDeleted: true }
          })
        : Promise.resolve(undefined),
      loadRepairOrders(currentVehicle.id)
    ])

    customer.value = currentCustomer
    model.value = currentModel
    repairOrders.value = currentOrders
    brand.value = currentModel
      ? await api.request<VehicleBrand>(`/vehicle-brands/${currentModel.brandId}`, {
          query: { includeDeleted: true }
        })
      : undefined
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <NuxtLink class="back-link" :to="customer ? `/customers/${customer.id}` : '/customers'">
      <ArrowLeft :size="16" /> Quay lại hồ sơ khách hàng
    </NuxtLink>

    <template v-if="vehicle">
      <div class="page-header">
        <div>
          <div class="title-line">
            <h1 class="page-title mono">{{ vehicle.licensePlate }}</h1>
            <AppBadge :tone="vehicle.isDeleted || !vehicle.isActive ? 'neutral' : 'success'">
              {{ vehicle.isDeleted ? 'Đã xóa' : vehicle.isActive ? 'Đang sử dụng' : 'Tạm ngưng' }}
            </AppBadge>
          </div>
          <p class="page-subtitle vehicle-summary">
            <AppEntityLink :to="brand ? `/catalogs/vehicle-brands/${brand.id}` : undefined">{{ brand?.name || 'Chưa rõ hãng' }}</AppEntityLink>
            <span>·</span>
            <AppEntityLink :to="model ? `/catalogs/vehicle-models/${model.id}` : undefined">{{ model?.name || 'Chưa rõ dòng xe' }}</AppEntityLink>
            <span v-if="vehicle.manufactureYear">· {{ vehicle.manufactureYear }}</span>
          </p>
        </div>
      </div>

      <section class="metric-grid">
        <article class="metric"><Gauge :size="20" /><div><span>ODO hiện tại</span><strong>{{ formatNumber(vehicle.odometer || 0) }} km</strong></div></article>
        <article class="metric"><History :size="20" /><div><span>Số lần sửa chữa</span><strong>{{ formatNumber(repairOrders.length) }}</strong><small>{{ formatNumber(completedOrderCount) }} phiếu hoàn tất</small></div></article>
        <article class="metric"><CalendarDays :size="20" /><div><span>Lần tiếp nhận gần nhất</span><strong>{{ latestOrder ? formatDate(latestOrder.receivedAt) : '—' }}</strong></div></article>
        <article class="metric"><ClipboardList :size="20" /><div><span>Tổng giá trị sửa chữa</span><strong>{{ formatCurrency(totalRepairValue) }}</strong></div></article>
      </section>

      <section class="detail-columns">
        <article class="card">
          <header class="card-header"><h2 class="card-title">Thông tin phương tiện</h2></header>
          <div class="card-body detail-grid">
            <div><span>Biển số</span><strong class="mono">{{ vehicle.licensePlate }}</strong></div>
            <div><span>Chủ xe</span><strong><AppEntityLink v-if="customer" :to="`/customers/${customer.id}`" icon>{{ customer.fullName }}</AppEntityLink><template v-else>—</template></strong></div>
            <div><span>Dòng xe</span><strong><AppEntityLink v-if="model" :to="`/catalogs/vehicle-models/${model.id}`" icon>{{ model.name }}</AppEntityLink><template v-else>—</template></strong></div>
            <div><span>Hãng xe</span><strong><AppEntityLink v-if="brand" :to="`/catalogs/vehicle-brands/${brand.id}`" icon>{{ brand.name }}</AppEntityLink><template v-else>—</template></strong></div>
            <div><span>Loại / phân khối</span><strong>{{ model?.vehicleType || '—' }}<template v-if="model?.engineCapacityCc"> · {{ formatNumber(model.engineCapacityCc) }} cc</template></strong></div>
            <div><span>Năm sản xuất / màu xe</span><strong>{{ vehicle.manufactureYear || '—' }} · {{ vehicle.color || '—' }}</strong></div>
            <div><span>Số khung</span><strong class="mono">{{ vehicle.frameNumber || '—' }}</strong></div>
            <div><span>Số máy</span><strong class="mono">{{ vehicle.engineNumber || '—' }}</strong></div>
            <div><span>Ngày mua</span><strong>{{ formatDate(vehicle.purchaseDate) }}</strong></div>
            <div><span>Trạng thái</span><strong>{{ vehicle.isActive && !vehicle.isDeleted ? 'Đang hoạt động' : 'Ngừng hoạt động' }}</strong></div>
            <div class="span-2"><span>Ghi chú</span><strong>{{ vehicle.notes || '—' }}</strong></div>
          </div>
        </article>

        <article class="card owner-card">
          <header class="card-header"><h2 class="card-title">Chủ sở hữu</h2><UserRound :size="20" /></header>
          <div v-if="customer" class="card-body owner-body">
            <div class="owner-icon"><UserRound :size="25" /></div>
            <div>
              <AppEntityLink :to="`/customers/${customer.id}`" block icon>
                <strong>{{ customer.fullName }}</strong>
                <span class="mono">{{ customer.code }}</span>
              </AppEntityLink>
            </div>
            <dl>
              <div><dt>Điện thoại</dt><dd>{{ customer.phone }}</dd></div>
              <div><dt>Email</dt><dd>{{ customer.email || '—' }}</dd></div>
              <div><dt>Địa chỉ</dt><dd>{{ customer.address || '—' }}</dd></div>
            </dl>
          </div>
        </article>
      </section>

      <section class="card">
        <header class="card-header"><div><h2 class="card-title">Lịch sử sửa chữa</h2><span class="section-note">Các phiếu sửa chữa gắn với phương tiện này</span></div><span class="muted">{{ formatNumber(repairOrders.length) }} phiếu</span></header>
        <div v-if="repairOrders.length" class="table-wrap">
          <table class="data-table">
            <thead><tr><th>Mã phiếu</th><th>Ngày nhận</th><th>Yêu cầu khách hàng</th><th>Trạng thái</th><th class="text-right">Giá trị</th></tr></thead>
            <tbody>
              <tr v-for="order in repairOrders" :key="order.id">
                <td><AppEntityLink :to="`/repair-orders/${order.id}`" icon><strong class="mono">{{ order.code }}</strong></AppEntityLink></td>
                <td>{{ formatDate(order.receivedAt, true) }}</td>
                <td class="request-cell">{{ order.customerRequest }}</td>
                <td><AppBadge :tone="statusTone(order.status)">{{ statusLabel(order.status) }}</AppBadge></td>
                <td class="text-right cell-main">{{ formatCurrency(order.finalTotal) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <AppEmpty v-else :icon="Bike" title="Chưa có lịch sử sửa chữa" message="Phương tiện này chưa có phiếu tiếp nhận nào." />
      </section>
    </template>

    <div v-else-if="loading" class="loading-skeleton" style="height: 420px" />
    <AppEmpty v-else :icon="Bike" title="Không tìm thấy phương tiện" message="Phương tiện không tồn tại hoặc bạn không có quyền xem dữ liệu này." />
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.title-line { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.vehicle-summary { display: flex; align-items: center; gap: 5px; flex-wrap: wrap; }
.metric-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; }
.metric { display: flex; min-width: 0; align-items: center; gap: 12px; padding: 17px 19px; border: 1px solid var(--line); border-radius: var(--radius-lg); color: var(--navy-800); background: white; box-shadow: var(--shadow-sm); }
.metric span,.metric strong,.metric small { display: block; }.metric span,.metric small { color: var(--muted); font-size: 10px; }.metric strong { margin-top: 4px; overflow: hidden; color: var(--navy-950); font-size: 18px; text-overflow: ellipsis; }.metric small { margin-top: 2px; }
.detail-columns { display: grid; grid-template-columns: minmax(0, 1.5fr) minmax(280px, .7fr); gap: 18px; align-items: start; }
.detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }.detail-grid span,.detail-grid strong { display: block; }.detail-grid span { color: var(--muted); font-size: 11px; }.detail-grid strong { margin-top: 4px; color: var(--navy-950); overflow-wrap: anywhere; }.span-2 { grid-column: span 2; }
.owner-body { display: grid; grid-template-columns: auto minmax(0, 1fr); align-items: center; gap: 13px; }.owner-icon { display: grid; width: 48px; height: 48px; place-items: center; border-radius: 13px; color: var(--navy-800); background: var(--blue-soft); }.owner-body strong,.owner-body span { display: block; }.owner-body span { margin-top: 2px; color: var(--muted); font-size: 11px; }.owner-body dl { display: grid; grid-column: 1 / -1; gap: 10px; margin: 6px 0 0; }.owner-body dl div { padding-top: 10px; border-top: 1px solid var(--line); }.owner-body dt { color: var(--muted); font-size: 10px; }.owner-body dd { margin: 3px 0 0; color: var(--navy-950); overflow-wrap: anywhere; }
.request-cell { max-width: 360px; overflow: hidden; text-overflow: ellipsis; }
@media (max-width: 1000px) { .metric-grid { grid-template-columns: repeat(2, 1fr); }.detail-columns { grid-template-columns: 1fr; } }
@media (max-width: 640px) { .metric-grid,.detail-grid { grid-template-columns: 1fr; }.span-2 { grid-column: auto; } }
</style>
