<script setup lang="ts">
import { ArrowLeft, CalendarDays, Gauge, Ticket, UsersRound } from '@lucide/vue'
import type { Coupon, Customer, LoyaltyTier, PagedResult } from '~/types/api'
import { entityDetailRoute } from '~/utils/entityRoute'
import { formatCurrency, formatDate, formatNumber } from '~/utils/format'

type BadgeTone = 'success' | 'warning' | 'danger' | 'neutral'

const route = useRoute()
const api = useApi()
const auth = useAuth()
const isAdmin = computed(() => auth.hasAnyRole('Admin', 'Administrator'))
const coupon = ref<Coupon>()
const customers = ref<Customer[]>([])
const tiers = ref<LoyaltyTier[]>([])
const loading = ref(true)

const couponId = computed(() => String(route.params.id))
const customerById = computed(() => new Map(customers.value.map(customer => [customer.id, customer])))
const applicableCustomers = computed(() => (coupon.value?.customerIds || [])
  .map(id => customerById.value.get(id))
  .filter((customer): customer is Customer => Boolean(customer)))
const missingCustomerIds = computed(() => (coupon.value?.customerIds || [])
  .filter(id => !customerById.value.has(id)))
const remainingUses = computed(() => coupon.value?.usageLimit == null
  ? undefined
  : Math.max(0, coupon.value.usageLimit - coupon.value.usedCount))
const usageRate = computed(() => coupon.value?.usageLimit
  ? Math.min(100, Math.round(coupon.value.usedCount * 100 / coupon.value.usageLimit))
  : 0)
const discountText = computed(() => coupon.value?.discountType === 'Percentage'
  ? `${formatNumber(coupon.value.discountValue)}%`
  : formatCurrency(coupon.value?.discountValue))
const audienceLabel = computed(() => ({
  All: 'Tất cả khách hàng',
  MinimumOrder: 'Đơn hàng đạt giá trị tối thiểu',
  SpecificCustomers: 'Khách hàng được chỉ định'
}[coupon.value?.audience || 'All']))
const tierByCode = (code?: string) => tiers.value.find(tier => tier.code === code)
const couponState = computed<{ label: string, tone: BadgeTone }>(() => {
  const current = coupon.value
  if (!current) return { label: 'Không xác định', tone: 'neutral' }
  if (current.isDeleted) return { label: 'Đã xóa', tone: 'neutral' }
  if (!current.isActive) return { label: 'Tạm khóa', tone: 'neutral' }
  if (current.usageLimit != null && current.usedCount >= current.usageLimit) {
    return { label: 'Đã hết lượt', tone: 'danger' }
  }
  const now = Date.now()
  if (current.startAt && new Date(current.startAt).getTime() > now) return { label: 'Chưa bắt đầu', tone: 'warning' }
  if (current.endAt && new Date(current.endAt).getTime() < now) return { label: 'Đã hết hạn', tone: 'danger' }
  return { label: 'Đang áp dụng', tone: 'success' }
})

const loadCustomers = async () => {
  const firstPage = await api.request<PagedResult<Customer>>('/customers', {
    query: { page: 1, pageSize: 200, includeDeleted: true }
  })
  const remainingPages = await Promise.all(Array.from(
    { length: Math.max(0, firstPage.totalPages - 1) },
    (_, index) => api.request<PagedResult<Customer>>('/customers', {
      query: { page: index + 2, pageSize: 200, includeDeleted: true }
    })
  ))
  return [firstPage, ...remainingPages].flatMap(page => page.items)
}

const load = async () => {
  loading.value = true
  try {
    coupon.value = await api.request<Coupon>(`/coupons/${couponId.value}`, {
      query: { includeDeleted: true }
    })
    const [customerResult, tierResult] = await Promise.allSettled([
      loadCustomers(),
      api.request<LoyaltyTier[]>('/loyalty/tiers')
    ])
    customers.value = customerResult.status === 'fulfilled' ? customerResult.value : []
    tiers.value = tierResult.status === 'fulfilled' ? tierResult.value : []
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <NuxtLink class="back-link" :to="isAdmin ? '/coupons' : '/invoices'"><ArrowLeft :size="16" /> {{ isAdmin ? 'Danh sách coupon' : 'Danh sách hóa đơn' }}</NuxtLink>

    <template v-if="coupon">
      <div class="page-header">
        <div>
          <div class="title-line">
            <h1 class="page-title">{{ coupon.name }}</h1>
            <AppBadge :tone="couponState.tone">{{ couponState.label }}</AppBadge>
          </div>
          <p class="page-subtitle mono">{{ coupon.code }}</p>
        </div>
      </div>

      <section class="metric-grid">
        <article class="metric"><Ticket :size="20" /><div><span>Mức giảm</span><strong>{{ discountText }}</strong></div></article>
        <article class="metric"><Gauge :size="20" /><div><span>Đã sử dụng</span><strong>{{ formatNumber(coupon.usedCount) }}</strong><small v-if="coupon.usageLimit">trên {{ formatNumber(coupon.usageLimit) }} lượt</small><small v-else>không giới hạn</small></div></article>
        <article class="metric"><UsersRound :size="20" /><div><span>Đối tượng áp dụng</span><strong>{{ coupon.audience === 'SpecificCustomers' ? formatNumber(coupon.customerIds.length) : 'Tất cả' }}</strong><small>{{ audienceLabel }}</small></div></article>
        <article class="metric"><CalendarDays :size="20" /><div><span>Thời hạn</span><strong>{{ coupon.endAt ? formatDate(coupon.endAt) : 'Không giới hạn' }}</strong><small v-if="coupon.startAt">Từ {{ formatDate(coupon.startAt) }}</small></div></article>
      </section>

      <section class="detail-columns">
        <article class="card">
          <header class="card-header"><h2 class="card-title">Điều kiện coupon</h2></header>
          <div class="card-body detail-grid">
            <div><span>Mã coupon</span><strong class="mono">{{ coupon.code }}</strong></div>
            <div><span>Trạng thái</span><strong>{{ couponState.label }}</strong></div>
            <div><span>Kiểu giảm giá</span><strong>{{ coupon.discountType === 'Percentage' ? 'Theo phần trăm' : 'Theo số tiền' }}</strong></div>
            <div><span>Giá trị giảm</span><strong>{{ discountText }}</strong></div>
            <div><span>Đơn hàng tối thiểu</span><strong>{{ coupon.minimumOrderAmount ? formatCurrency(coupon.minimumOrderAmount) : 'Không yêu cầu' }}</strong></div>
            <div><span>Đối tượng</span><strong>{{ audienceLabel }}</strong></div>
            <div><span>Bắt đầu</span><strong>{{ coupon.startAt ? formatDate(coupon.startAt, true) : 'Không giới hạn' }}</strong></div>
            <div><span>Kết thúc</span><strong>{{ coupon.endAt ? formatDate(coupon.endAt, true) : 'Không giới hạn' }}</strong></div>
            <div class="span-2"><span>Mô tả</span><strong>{{ coupon.description || '—' }}</strong></div>
          </div>
        </article>

        <article class="card usage-card">
          <header class="card-header"><h2 class="card-title">Hạn mức sử dụng</h2><Gauge :size="20" /></header>
          <div class="card-body usage-body">
            <div class="usage-value"><strong>{{ formatNumber(coupon.usedCount) }}</strong><span>/ {{ coupon.usageLimit == null ? '∞' : formatNumber(coupon.usageLimit) }} lượt</span></div>
            <div v-if="coupon.usageLimit" class="progress" role="progressbar" :aria-valuenow="usageRate" aria-valuemin="0" aria-valuemax="100"><i :style="{ width: `${usageRate}%` }" /></div>
            <div class="usage-note">
              <span>Còn lại</span>
              <strong>{{ remainingUses === undefined ? 'Không giới hạn' : `${formatNumber(remainingUses)} lượt` }}</strong>
            </div>
          </div>
        </article>
      </section>

      <section class="card">
        <header class="card-header">
          <div><h2 class="card-title">Khách hàng được áp dụng</h2><span class="section-note">Danh sách khách hàng được chỉ định trực tiếp cho coupon</span></div>
          <span class="muted">{{ coupon.audience === 'SpecificCustomers' ? formatNumber(coupon.customerIds.length) : audienceLabel }}</span>
        </header>

        <div v-if="coupon.audience !== 'SpecificCustomers'" class="audience-notice">
          <UsersRound :size="22" />
          <div><strong>{{ audienceLabel }}</strong><span>Coupon không giới hạn theo một danh sách khách hàng cụ thể.</span></div>
        </div>
        <div v-else-if="applicableCustomers.length" class="table-wrap">
          <table class="data-table">
            <thead><tr><th>Khách hàng</th><th>Liên hệ</th><th>Hạng thành viên</th><th>Trạng thái</th></tr></thead>
            <tbody>
              <tr v-for="item in applicableCustomers" :key="item.id">
                <td>
                  <AppEntityLink :to="`/customers/${item.id}`" block icon>
                    <strong>{{ item.fullName }}</strong>
                    <span class="cell-sub mono">{{ item.code }}</span>
                  </AppEntityLink>
                </td>
                <td><div class="cell-main">{{ item.phone }}</div><div class="cell-sub">{{ item.email || item.address || '—' }}</div></td>
                <td><AppEntityLink :to="entityDetailRoute('LoyaltyTier', tierByCode(item.loyaltyTierCode)?.id)">{{ item.loyaltyTierCode || 'MEMBER' }}</AppEntityLink></td>
                <td><AppBadge :tone="item.isDeleted || !item.isActive ? 'neutral' : 'success'">{{ item.isDeleted ? 'Đã xóa' : item.isActive ? 'Hoạt động' : 'Tạm khóa' }}</AppBadge></td>
              </tr>
            </tbody>
          </table>
          <div v-if="missingCustomerIds.length" class="missing-note">{{ formatNumber(missingCustomerIds.length) }} khách hàng không còn dữ liệu hồ sơ để hiển thị.</div>
        </div>
        <AppEmpty v-else :icon="UsersRound" title="Chưa chỉ định khách hàng" message="Coupon chưa có khách hàng cụ thể trong phạm vi áp dụng." />
      </section>
    </template>

    <div v-else-if="loading" class="loading-skeleton" style="height: 420px" />
    <AppEmpty v-else :icon="Ticket" title="Không tìm thấy coupon" message="Coupon không tồn tại hoặc bạn không có quyền xem dữ liệu này." />
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.title-line { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.metric-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; }
.metric { display: flex; min-width: 0; align-items: center; gap: 12px; padding: 17px 19px; border: 1px solid var(--line); border-radius: var(--radius-lg); color: var(--navy-800); background: white; box-shadow: var(--shadow-sm); }
.metric span,.metric strong,.metric small { display: block; }.metric span,.metric small { color: var(--muted); font-size: 10px; }.metric strong { margin-top: 4px; overflow: hidden; color: var(--navy-950); font-size: 18px; text-overflow: ellipsis; }.metric small { margin-top: 2px; }
.detail-columns { display: grid; grid-template-columns: minmax(0, 1.4fr) minmax(280px, .6fr); gap: 18px; align-items: start; }
.detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }.detail-grid span,.detail-grid strong { display: block; }.detail-grid span { color: var(--muted); font-size: 11px; }.detail-grid strong { margin-top: 4px; color: var(--navy-950); overflow-wrap: anywhere; }.span-2 { grid-column: span 2; }
.usage-body { display: grid; gap: 15px; }.usage-value { display: flex; align-items: baseline; gap: 6px; }.usage-value strong { color: var(--navy-950); font-size: 30px; }.usage-value span { color: var(--muted); }.progress { overflow: hidden; height: 9px; border-radius: 999px; background: var(--surface-soft); }.progress i { display: block; height: 100%; border-radius: inherit; background: var(--teal); }.usage-note { display: flex; justify-content: space-between; gap: 12px; padding-top: 12px; border-top: 1px solid var(--line); }.usage-note span { color: var(--muted); }.usage-note strong { color: var(--navy-950); }
.audience-notice { display: flex; align-items: flex-start; gap: 12px; margin: 18px; padding: 18px; border-radius: 12px; color: var(--navy-800); background: var(--blue-soft); }.audience-notice strong,.audience-notice span { display: block; }.audience-notice span { margin-top: 3px; color: var(--muted); font-size: 11px; }.missing-note { padding: 12px 16px; border-top: 1px solid var(--line); color: var(--muted); font-size: 11px; }
@media (max-width: 1000px) { .metric-grid { grid-template-columns: repeat(2, 1fr); }.detail-columns { grid-template-columns: 1fr; } }
@media (max-width: 640px) { .metric-grid,.detail-grid { grid-template-columns: 1fr; }.span-2 { grid-column: auto; } }
</style>
