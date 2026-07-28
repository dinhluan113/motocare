<script setup lang="ts">
import {
  AlertTriangle,
  Bike,
  CircleDollarSign,
  Clock3,
  PackageX,
  Plus,
  ReceiptText,
  Wrench
} from '@lucide/vue'
import type { PagedResult, Part, RepairOrder } from '~/types/api'
import { formatCurrency, formatDate, formatNumber, statusLabel, statusTone } from '~/utils/format'

interface Dashboard {
  repairOrders: {
    repairing: number
    awaitingParts: number
    waitingDelivery: number
    overdue: number
  }
  finance: {
    revenueToday: number
    collectedToday: number
    outstandingToday: number
  }
}

const api = useApi()
const loading = ref(true)
const dashboard = ref<Dashboard>()
const orders = ref<RepairOrder[]>([])
const lowStock = ref<Part[]>([])

const load = async () => {
  loading.value = true
  try {
    const [summary, recent, stock] = await Promise.all([
      api.request<Dashboard>('/dashboard'),
      api.request<PagedResult<RepairOrder>>('/repair-orders?page=1&pageSize=6'),
      api.request<Part[]>('/inventory/low-stock')
    ])
    dashboard.value = summary
    orders.value = recent.items
    lowStock.value = stock.slice(0, 5)
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1 class="page-title">Trung tâm vận hành</h1>
        <p class="page-subtitle">
          Tình hình xưởng hôm nay, công việc cần xử lý và các cảnh báo quan trọng.
        </p>
      </div>
      <div class="page-actions">
        <NuxtLink class="btn btn-secondary" to="/customers">
          <Bike :size="17" /> Thêm khách & xe
        </NuxtLink>
        <NuxtLink class="btn btn-accent" to="/repair-orders/new">
          <Plus :size="17" /> Tiếp nhận sửa chữa
        </NuxtLink>
      </div>
    </div>

    <section v-if="loading" class="dashboard-metrics">
      <div v-for="n in 4" :key="n" class="loading-skeleton metric-placeholder" />
    </section>
    <section v-else class="dashboard-metrics">
      <MetricCard
        label="Đang sửa chữa"
        :value="dashboard?.repairOrders.repairing || 0"
        detail="Phiếu đang trong xưởng"
        tone="navy"
        :icon="Wrench"
      />
      <MetricCard
        label="Chờ phụ tùng"
        :value="dashboard?.repairOrders.awaitingParts || 0"
        detail="Cần theo dõi tiến độ nhập"
        tone="amber"
        :icon="Clock3"
      />
      <MetricCard
        label="Chờ giao xe"
        :value="dashboard?.repairOrders.waitingDelivery || 0"
        detail="Đã hoàn thành kỹ thuật"
        tone="teal"
        :icon="Bike"
      />
      <MetricCard
        label="Doanh thu hôm nay"
        :value="formatCurrency(dashboard?.finance.revenueToday || 0)"
        :detail="`Đã thu ${formatCurrency(dashboard?.finance.collectedToday || 0)}`"
        tone="blue"
        :icon="CircleDollarSign"
      />
    </section>

    <div
      v-if="dashboard?.repairOrders.overdue"
      class="alert alert-danger"
    >
      <AlertTriangle :size="19" />
      <div>
        <strong>{{ dashboard.repairOrders.overdue }} phiếu đã quá ngày hẹn</strong>
        <div>Kiểm tra tiến độ và chủ động thông báo cho khách hàng.</div>
      </div>
    </div>

    <section class="dashboard-grid">
      <article class="card">
        <header class="card-header">
          <div>
            <h2 class="card-title">Phiếu sửa chữa gần đây</h2>
            <span class="section-note">Xếp theo thời điểm tiếp nhận mới nhất</span>
          </div>
          <NuxtLink class="btn btn-ghost btn-sm" to="/repair-orders">Xem tất cả</NuxtLink>
        </header>
        <div class="table-wrap">
          <table v-if="orders.length" class="data-table">
            <thead>
              <tr>
                <th>Mã phiếu</th>
                <th>Tiếp nhận</th>
                <th>Trạng thái</th>
                <th class="text-right">Dự kiến</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="order in orders" :key="order.id">
                <td>
                  <NuxtLink class="cell-link mono" :to="`/repair-orders/${order.id}`">
                    {{ order.code }}
                  </NuxtLink>
                </td>
                <td>{{ formatDate(order.receivedAt, true) }}</td>
                <td>
                  <AppBadge :tone="statusTone(order.status)">{{ statusLabel(order.status) }}</AppBadge>
                </td>
                <td class="text-right cell-main">{{ formatCurrency(order.finalTotal) }}</td>
              </tr>
            </tbody>
          </table>
          <AppEmpty
            v-else
            title="Chưa có phiếu sửa chữa"
            message="Tạo phiếu đầu tiên khi khách mang xe đến."
          />
        </div>
      </article>

      <article class="card">
        <header class="card-header">
          <div>
            <h2 class="card-title">Cảnh báo tồn kho</h2>
            <span class="section-note">Phụ tùng chạm hoặc thấp hơn định mức</span>
          </div>
          <PackageX :size="20" class="warning-icon" />
        </header>
        <div v-if="lowStock.length" class="stock-list">
          <NuxtLink
            v-for="part in lowStock"
            :key="part.id"
            to="/inventory"
            class="stock-row"
          >
            <div>
              <strong>{{ part.name }}</strong>
              <span class="mono">{{ part.code }} · {{ part.location || 'Chưa có vị trí' }}</span>
            </div>
            <div class="stock-count">
              <strong>{{ formatNumber(part.quantityOnHand) }}</strong>
              <span>tối thiểu {{ formatNumber(part.minQuantity) }}</span>
            </div>
          </NuxtLink>
        </div>
        <AppEmpty
          v-else
          :icon="ReceiptText"
          title="Tồn kho đang ổn định"
          message="Không có phụ tùng nào dưới định mức."
        />
      </article>
    </section>
  </div>
</template>

<style scoped>
.dashboard-metrics {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
}

.metric-placeholder { min-height: 138px; }

.dashboard-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.65fr) minmax(310px, 0.75fr);
  gap: 18px;
}

.section-note {
  display: block;
  margin-top: 3px;
  color: var(--muted);
  font-size: 11px;
}

.cell-link {
  color: var(--blue);
  font-weight: 800;
}

.warning-icon { color: var(--amber); }

.stock-list { padding: 8px 18px; }

.stock-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 15px;
  padding: 13px 2px;
  border-bottom: 1px solid var(--line);
}

.stock-row:last-child { border-bottom: 0; }
.stock-row strong, .stock-row span { display: block; }
.stock-row > div:first-child strong { color: var(--navy-950); }
.stock-row span { margin-top: 3px; color: var(--muted); font-size: 11px; }
.stock-count { text-align: right; }
.stock-count strong { color: var(--red); font-size: 18px; }

@media (max-width: 1180px) {
  .dashboard-metrics { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .dashboard-grid { grid-template-columns: 1fr; }
}

@media (max-width: 600px) {
  .dashboard-metrics { grid-template-columns: 1fr; }
}
</style>
