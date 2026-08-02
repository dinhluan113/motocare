<script setup lang="ts">
import { ArrowLeft, Crown, Gift, Star, WalletCards } from '@lucide/vue'
import type { LoyaltyTier } from '~/types/api'
import { formatCurrency, formatDate, formatNumber } from '~/utils/format'

interface LoyaltyTierDetail extends LoyaltyTier {
  description?: string
}

const route = useRoute()
const api = useApi()
const tier = ref<LoyaltyTierDetail>()
const loading = ref(true)

const tierId = computed(() => String(route.params.id))

const load = async () => {
  loading.value = true
  try {
    tier.value = await api.request<LoyaltyTierDetail>(`/loyalty/tiers/${tierId.value}`, {
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

    <template v-if="tier">
      <div class="page-header">
        <div>
          <div class="title-line">
            <h1 class="page-title">{{ tier.name }}</h1>
            <AppBadge :tone="tier.isDeleted || !tier.isActive ? 'neutral' : 'success'">{{ tier.isDeleted ? 'Đã xóa' : tier.isActive ? 'Đang áp dụng' : 'Tạm ngưng' }}</AppBadge>
          </div>
          <p class="page-subtitle"><span class="mono">{{ tier.code }}</span> · Hạng cấp {{ formatNumber(tier.rank) }}</p>
        </div>
      </div>

      <section class="tier-hero">
        <div class="tier-emblem"><Crown :size="32" /></div>
        <div><span>Hạng thành viên</span><strong>{{ tier.name }}</strong><small>{{ tier.description || 'Quyền lợi được áp dụng tự động khi khách hàng đạt điều kiện.' }}</small></div>
        <div class="rank-block"><span>Cấp</span><strong>{{ formatNumber(tier.rank) }}</strong></div>
      </section>

      <section class="metric-grid">
        <article class="metric"><WalletCards :size="20" /><div><span>Chi tiêu tối thiểu</span><strong>{{ formatCurrency(tier.minEligibleSpend) }}</strong></div></article>
        <article class="metric"><Star :size="20" /><div><span>Điểm tích lũy tối thiểu</span><strong>{{ formatNumber(tier.minEarnedPoints) }} điểm</strong></div></article>
        <article class="metric"><Crown :size="20" /><div><span>Hệ số tích điểm</span><strong>x{{ formatNumber(tier.earnRate) }}</strong></div></article>
        <article class="metric"><Gift :size="20" /><div><span>Giá trị mỗi điểm</span><strong>{{ formatCurrency(tier.redemptionValue) }}</strong></div></article>
      </section>

      <section class="detail-columns">
        <article class="card">
          <header class="card-header"><h2 class="card-title">Thông tin hạng thành viên</h2></header>
          <div class="card-body detail-grid">
            <div><span>Mã hạng</span><strong class="mono">{{ tier.code }}</strong></div>
            <div><span>Tên hạng</span><strong>{{ tier.name }}</strong></div>
            <div><span>Cấp xếp hạng</span><strong>{{ formatNumber(tier.rank) }}</strong></div>
            <div><span>Trạng thái</span><strong>{{ tier.isDeleted ? 'Đã xóa' : tier.isActive ? 'Đang hoạt động' : 'Tạm ngưng' }}</strong></div>
            <div><span>Chi tiêu tối thiểu</span><strong>{{ formatCurrency(tier.minEligibleSpend) }}</strong></div>
            <div><span>Điểm tích lũy tối thiểu</span><strong>{{ formatNumber(tier.minEarnedPoints) }} điểm</strong></div>
            <div><span>Hệ số tích điểm</span><strong>x{{ formatNumber(tier.earnRate) }}</strong></div>
            <div><span>Giá trị quy đổi mỗi điểm</span><strong>{{ formatCurrency(tier.redemptionValue) }}</strong></div>
            <div class="span-2"><span>Mô tả</span><strong>{{ tier.description || '—' }}</strong></div>
            <div><span>Ngày tạo</span><strong>{{ formatDate(tier.createdAt, true) }}</strong></div>
            <div><span>Cập nhật gần nhất</span><strong>{{ formatDate(tier.updatedAt, true) }}</strong></div>
            <div class="span-2"><span>ID hệ thống</span><strong class="mono entity-id">{{ tier.id }}</strong></div>
          </div>
        </article>

        <article class="card">
          <header class="card-header"><h2 class="card-title">Quyền lợi thành viên</h2><Gift :size="20" /></header>
          <div v-if="tier.benefits.length" class="benefit-list">
            <div v-for="(benefit, index) in tier.benefits" :key="`${index}-${benefit}`"><span><Star :size="15" /></span><strong>{{ benefit }}</strong></div>
          </div>
          <AppEmpty v-else :icon="Gift" title="Chưa có quyền lợi riêng" message="Hạng thành viên này đang sử dụng quyền lợi tiêu chuẩn." />
        </article>
      </section>
    </template>

    <div v-else-if="loading" class="loading-skeleton" style="height: 420px" />
    <AppEmpty v-else :icon="Crown" title="Không tìm thấy hạng thành viên" message="Hạng thành viên không tồn tại hoặc đã bị xóa." />
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.title-line { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.tier-hero { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: 16px; padding: 24px; border-radius: var(--radius-lg); color: white; background: linear-gradient(135deg, var(--navy-950), var(--navy-700)); box-shadow: var(--shadow); }
.tier-emblem { display: grid; width: 62px; height: 62px; place-items: center; border-radius: 17px; color: var(--navy-950); background: var(--amber); }.tier-hero span,.tier-hero strong,.tier-hero small { display: block; }.tier-hero span,.tier-hero small { color: #b9cbd8; }.tier-hero span { font-size: 10px; text-transform: uppercase; }.tier-hero strong { margin: 3px 0; font-size: 24px; }.tier-hero small { font-size: 11px; }.rank-block { text-align: right; }.rank-block strong { color: var(--amber); font-size: 34px; }
.metric-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; }.metric { display: flex; min-width: 0; align-items: center; gap: 12px; padding: 17px 19px; border: 1px solid var(--line); border-radius: var(--radius-lg); color: var(--navy-800); background: white; box-shadow: var(--shadow-sm); }.metric span,.metric strong { display: block; }.metric span { color: var(--muted); font-size: 10px; }.metric strong { margin-top: 4px; overflow: hidden; color: var(--navy-950); font-size: 17px; text-overflow: ellipsis; }
.detail-columns { display: grid; grid-template-columns: minmax(0, 1.4fr) minmax(280px, .6fr); gap: 18px; align-items: start; }.detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }.detail-grid span,.detail-grid strong { display: block; }.detail-grid span { color: var(--muted); font-size: 11px; }.detail-grid strong { margin-top: 4px; color: var(--navy-950); overflow-wrap: anywhere; }.span-2 { grid-column: span 2; }.entity-id { font-size: 11px; }
.benefit-list { display: grid; gap: 10px; padding: 18px; }.benefit-list > div { display: flex; align-items: flex-start; gap: 10px; padding: 12px; border: 1px solid var(--line); border-radius: 11px; background: var(--surface-soft); }.benefit-list span { display: grid; width: 29px; height: 29px; flex: 0 0 auto; place-items: center; border-radius: 9px; color: #805b09; background: var(--amber-soft); }.benefit-list strong { padding-top: 5px; color: var(--navy-950); }
@media (max-width: 1000px) { .metric-grid { grid-template-columns: repeat(2, 1fr); }.detail-columns { grid-template-columns: 1fr; } }
@media (max-width: 640px) { .tier-hero { grid-template-columns: auto 1fr; }.rank-block { grid-column: 1 / -1; text-align: left; }.metric-grid,.detail-grid { grid-template-columns: 1fr; }.span-2 { grid-column: auto; } }
</style>
