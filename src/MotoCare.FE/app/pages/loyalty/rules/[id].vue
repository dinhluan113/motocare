<script setup lang="ts">
import { ArrowLeft, CalendarDays, Coins, Gauge, RefreshCw, ShieldCheck } from '@lucide/vue'
import type { BaseDocument } from '~/types/api'
import { formatCurrency, formatDate, formatNumber } from '~/utils/format'

interface LoyaltyRuleDetail extends BaseDocument {
  name: string
  spendPerPoint: number
  redemptionValue: number
  minimumRedemptionPoints: number
  maximumRedemptionRate: number
  pointExpiryDays?: number | null
  effectiveFrom: string
  effectiveTo?: string | null
  isActive: boolean
}

type BadgeTone = 'success' | 'warning' | 'danger' | 'neutral'

const route = useRoute()
const api = useApi()
const rule = ref<LoyaltyRuleDetail>()
const loading = ref(true)

const ruleId = computed(() => String(route.params.id))
const maximumRedemptionPercent = computed(() => (rule.value?.maximumRedemptionRate || 0) * 100)
const ruleState = computed<{ label: string, tone: BadgeTone }>(() => {
  const current = rule.value
  if (!current) return { label: 'Không xác định', tone: 'neutral' }
  if (current.isDeleted) return { label: 'Đã xóa', tone: 'neutral' }
  if (!current.isActive) return { label: 'Tạm ngưng', tone: 'neutral' }
  const now = Date.now()
  if (new Date(current.effectiveFrom).getTime() > now) return { label: 'Chưa hiệu lực', tone: 'warning' }
  if (current.effectiveTo && new Date(current.effectiveTo).getTime() < now) return { label: 'Đã hết hiệu lực', tone: 'danger' }
  return { label: 'Đang áp dụng', tone: 'success' }
})

const load = async () => {
  loading.value = true
  try {
    rule.value = await api.request<LoyaltyRuleDetail>(`/loyalty/rules/${ruleId.value}`, {
      query: { includeDeleted: true }
    })
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <NuxtLink class="back-link" to="/loyalty"><ArrowLeft :size="16" /> Quay lại khách hàng thân thiết</NuxtLink>

    <template v-if="rule">
      <div class="page-header">
        <div>
          <div class="title-line">
            <h1 class="page-title">{{ rule.name }}</h1>
            <AppBadge :tone="ruleState.tone">{{ ruleState.label }}</AppBadge>
          </div>
          <p class="page-subtitle">Quy tắc tích và sử dụng điểm loyalty</p>
        </div>
      </div>

      <section class="metric-grid">
        <article class="metric"><Coins :size="20" /><div><span>Chi tiêu cho 1 điểm</span><strong>{{ formatCurrency(rule.spendPerPoint) }}</strong></div></article>
        <article class="metric"><RefreshCw :size="20" /><div><span>Giá trị quy đổi 1 điểm</span><strong>{{ formatCurrency(rule.redemptionValue) }}</strong></div></article>
        <article class="metric"><Gauge :size="20" /><div><span>Điểm đổi tối thiểu</span><strong>{{ formatNumber(rule.minimumRedemptionPoints) }} điểm</strong></div></article>
        <article class="metric"><ShieldCheck :size="20" /><div><span>Giảm tối đa trên hóa đơn</span><strong>{{ formatNumber(maximumRedemptionPercent) }}%</strong></div></article>
      </section>

      <section class="detail-columns">
        <article class="card">
          <header class="card-header"><h2 class="card-title">Thông tin quy tắc</h2></header>
          <div class="card-body detail-grid">
            <div class="span-2"><span>Tên quy tắc</span><strong>{{ rule.name }}</strong></div>
            <div><span>Chi tiêu để nhận 1 điểm</span><strong>{{ formatCurrency(rule.spendPerPoint) }}</strong></div>
            <div><span>Giá trị quy đổi 1 điểm</span><strong>{{ formatCurrency(rule.redemptionValue) }}</strong></div>
            <div><span>Điểm đổi tối thiểu</span><strong>{{ formatNumber(rule.minimumRedemptionPoints) }} điểm</strong></div>
            <div><span>Tỷ lệ giảm tối đa</span><strong>{{ formatNumber(maximumRedemptionPercent) }}% giá trị hóa đơn</strong></div>
            <div><span>Thời hạn điểm</span><strong>{{ rule.pointExpiryDays ? `${formatNumber(rule.pointExpiryDays)} ngày` : 'Không hết hạn' }}</strong></div>
            <div><span>Trạng thái cấu hình</span><strong>{{ rule.isActive ? 'Đang bật' : 'Tạm tắt' }}</strong></div>
            <div><span>Ngày bắt đầu hiệu lực</span><strong>{{ formatDate(rule.effectiveFrom, true) }}</strong></div>
            <div><span>Ngày kết thúc hiệu lực</span><strong>{{ rule.effectiveTo ? formatDate(rule.effectiveTo, true) : 'Không giới hạn' }}</strong></div>
            <div><span>Ngày tạo</span><strong>{{ formatDate(rule.createdAt, true) }}</strong></div>
            <div><span>Cập nhật gần nhất</span><strong>{{ formatDate(rule.updatedAt, true) }}</strong></div>
            <div class="span-2"><span>ID hệ thống</span><strong class="mono entity-id">{{ rule.id }}</strong></div>
          </div>
        </article>

        <div class="side-stack">
          <article class="card formula-card">
            <header class="card-header"><h2 class="card-title">Cách tích điểm</h2><Coins :size="20" /></header>
            <div class="formula-body"><strong>{{ formatCurrency(rule.spendPerPoint) }}</strong><span>chi tiêu hợp lệ</span><i /> <strong>1 điểm</strong><span>loyalty</span></div>
          </article>
          <article class="card formula-card">
            <header class="card-header"><h2 class="card-title">Cách đổi điểm</h2><RefreshCw :size="20" /></header>
            <div class="formula-body"><strong>1 điểm</strong><span>loyalty</span><i /> <strong>{{ formatCurrency(rule.redemptionValue) }}</strong><span>giá trị giảm</span></div>
          </article>
          <article class="card validity-card">
            <header class="card-header"><h2 class="card-title">Hiệu lực</h2><CalendarDays :size="20" /></header>
            <div class="card-body"><span>Từ {{ formatDate(rule.effectiveFrom) }}</span><strong>{{ rule.effectiveTo ? `Đến ${formatDate(rule.effectiveTo)}` : 'Không có ngày kết thúc' }}</strong><small>Trạng thái hiện tại: {{ ruleState.label }}</small></div>
          </article>
        </div>
      </section>
    </template>

    <div v-else-if="loading" class="loading-skeleton" style="height: 420px" />
    <AppEmpty v-else :icon="ShieldCheck" title="Không tìm thấy quy tắc loyalty" message="Quy tắc không tồn tại hoặc đã bị xóa." />
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.title-line { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.metric-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; }.metric { display: flex; min-width: 0; align-items: center; gap: 12px; padding: 17px 19px; border: 1px solid var(--line); border-radius: var(--radius-lg); color: var(--navy-800); background: white; box-shadow: var(--shadow-sm); }.metric span,.metric strong { display: block; }.metric span { color: var(--muted); font-size: 10px; }.metric strong { margin-top: 4px; overflow: hidden; color: var(--navy-950); font-size: 17px; text-overflow: ellipsis; }
.detail-columns { display: grid; grid-template-columns: minmax(0, 1.35fr) minmax(280px, .65fr); gap: 18px; align-items: start; }.detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }.detail-grid span,.detail-grid strong { display: block; }.detail-grid span { color: var(--muted); font-size: 11px; }.detail-grid strong { margin-top: 4px; color: var(--navy-950); overflow-wrap: anywhere; }.span-2 { grid-column: span 2; }.entity-id { font-size: 11px; }
.side-stack { display: grid; gap: 14px; }.formula-body { display: grid; grid-template-columns: 1fr auto 1fr; align-items: center; gap: 3px 12px; padding: 18px; }.formula-body strong,.formula-body span { display: block; text-align: center; }.formula-body strong { color: var(--navy-950); font-size: 16px; }.formula-body span { color: var(--muted); font-size: 10px; }.formula-body i { grid-row: span 2; width: 24px; height: 1px; background: var(--line); }.validity-card .card-body span,.validity-card .card-body strong,.validity-card .card-body small { display: block; }.validity-card .card-body span,.validity-card .card-body small { color: var(--muted); font-size: 11px; }.validity-card .card-body strong { margin: 4px 0 8px; color: var(--navy-950); }
@media (max-width: 1000px) { .metric-grid { grid-template-columns: repeat(2, 1fr); }.detail-columns { grid-template-columns: 1fr; } }
@media (max-width: 640px) { .metric-grid,.detail-grid { grid-template-columns: 1fr; }.span-2 { grid-column: auto; } }
</style>
