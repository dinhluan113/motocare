<script setup lang="ts">
import { Crown, Plus, Star, Trash2, UsersRound } from '@lucide/vue'
import type { LoyaltyTier } from '~/types/api'
import { entityDetailRoute } from '~/utils/entityRoute'
import { formatCurrency, formatNumber } from '~/utils/format'

interface LoyalCustomer {
  customerId: string
  customerCode: string
  fullName: string
  phone: string
  tierCode: string
  eligibleSpend: number
  availablePoints: number
}
interface LoyaltyRule {
  id: string
  name: string
  spendPerPoint: number
  redemptionValue: number
  minimumRedemptionPoints: number
  maximumRedemptionRate: number
  pointExpiryDays?: number
  effectiveFrom: string
  effectiveTo?: string
  isActive: boolean
}

const api = useApi()
const toast = useToast()
const tiers = ref<LoyaltyTier[]>([])
const rules = ref<LoyaltyRule[]>([])
const customers = ref<LoyalCustomer[]>([])
const tierOpen = ref(false)
const ruleOpen = ref(false)
const saving = ref(false)
const tierForm = reactive({ code: '', name: '', rank: 1, minEligibleSpend: 0, minEarnedPoints: 0, earnRate: 1, redemptionValue: 1000, benefitsText: '', description: '', isActive: true })
const ruleForm = reactive({ name: 'Quy tắc mặc định', spendPerPoint: 10000, redemptionValue: 1000, minimumRedemptionPoints: 10, maximumRedemptionRate: 0.5, pointExpiryDays: 365, effectiveFrom: new Date().toISOString().slice(0, 10), effectiveTo: '', isActive: true })
const load = async () => {
  const [t, r, c] = await Promise.all([
    api.request<LoyaltyTier[]>('/loyalty/tiers'),
    api.request<LoyaltyRule[]>('/loyalty/rules'),
    api.request<LoyalCustomer[]>('/reports/loyal-customers?limit=20')
  ])
  tiers.value = t; rules.value = r; customers.value = c
}
const saveTier = async () => {
  saving.value = true
  try {
    await api.request('/loyalty/tiers', { method: 'POST', body: { ...tierForm, benefits: tierForm.benefitsText.split(',').map(x => x.trim()).filter(Boolean) } })
    toast.success('Đã tạo hạng thành viên', tierForm.name); tierOpen.value = false; await load()
  } finally { saving.value = false }
}
const saveRule = async () => {
  saving.value = true
  try {
    await api.request('/loyalty/rules', { method: 'POST', body: { ...ruleForm, effectiveFrom: new Date(ruleForm.effectiveFrom).toISOString(), effectiveTo: ruleForm.effectiveTo ? new Date(ruleForm.effectiveTo).toISOString() : null } })
    toast.success('Đã tạo quy tắc loyalty', ruleForm.name); ruleOpen.value = false; await load()
  } finally { saving.value = false }
}
const removeTier = async (tier: LoyaltyTier) => {
  if (!confirm(`Xóa hạng thành viên ${tier.name}?`)) return
  await api.request(`/loyalty/tiers/${tier.id}`, { method: 'DELETE' })
  toast.success('Đã xóa hạng thành viên', tier.name)
  await load()
}
const removeRule = async (rule: LoyaltyRule) => {
  if (!confirm(`Xóa quy tắc ${rule.name}?`)) return
  await api.request(`/loyalty/rules/${rule.id}`, { method: 'DELETE' })
  toast.success('Đã xóa quy tắc loyalty', rule.name)
  await load()
}
const tierByCode = (code?: string) => tiers.value.find(tier => tier.code === code)
onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-header"><div><h1 class="page-title">Khách hàng thân thiết</h1><p class="page-subtitle">Hạng thành viên, tích/đổi điểm và danh sách khách có giá trị cao.</p></div><div class="page-actions"><button class="btn btn-secondary" @click="ruleOpen = true"><Plus :size="16" /> Quy tắc điểm</button><button class="btn btn-accent" @click="tierOpen = true"><Plus :size="16" /> Hạng thành viên</button></div></div>
    <section class="tier-grid">
      <article v-for="(tier, index) in tiers" :key="tier.id" class="tier-card" :class="`tier-${index % 3}`"><button class="icon-btn danger-button" title="Xóa hạng" @click="removeTier(tier)"><Trash2 :size="14" /></button><Crown :size="22" /><span>Cấp {{ tier.rank }}</span><AppEntityLink :to="entityDetailRoute('LoyaltyTier', tier.id)"><strong>{{ tier.name }}</strong></AppEntityLink><small>Từ {{ formatCurrency(tier.minEligibleSpend) }} · x{{ tier.earnRate }} điểm</small><div>{{ tier.benefits.join(' · ') || 'Quyền lợi tiêu chuẩn' }}</div></article>
      <AppEmpty v-if="!tiers.length" :icon="Star" title="Chưa cấu hình hạng thành viên" message="Tạo hạng đầu tiên để tự động phân loại khách hàng." />
    </section>
    <section class="loyalty-grid">
      <article class="card"><header class="card-header"><h2 class="card-title">Khách hàng nổi bật</h2><UsersRound :size="20" /></header><div class="table-wrap"><table v-if="customers.length" class="data-table"><thead><tr><th>Khách hàng</th><th>Hạng</th><th class="text-right">Chi tiêu</th><th class="text-right">Điểm</th></tr></thead><tbody><tr v-for="item in customers" :key="item.customerId"><td><NuxtLink class="cell-main customer-link" :to="`/customers/${item.customerId}`">{{ item.fullName }}</NuxtLink><div class="cell-sub">{{ item.phone }}</div></td><td><AppEntityLink :to="entityDetailRoute('LoyaltyTier', tierByCode(item.tierCode)?.id)"><AppBadge tone="warning">{{ item.tierCode }}</AppBadge></AppEntityLink></td><td class="text-right">{{ formatCurrency(item.eligibleSpend) }}</td><td class="text-right cell-main">{{ formatNumber(item.availablePoints) }}</td></tr></tbody></table><AppEmpty v-else title="Chưa có dữ liệu thành viên" message="Điểm được tạo khi khách thanh toán hóa đơn." /></div></article>
      <article class="card"><header class="card-header"><h2 class="card-title">Quy tắc đang áp dụng</h2></header><div class="rule-list"><div v-for="rule in rules" :key="rule.id" class="rule-card"><div><AppEntityLink :to="entityDetailRoute('LoyaltyRule', rule.id)"><strong>{{ rule.name }}</strong></AppEntityLink><span class="inline"><AppBadge :tone="rule.isActive ? 'success' : 'neutral'">{{ rule.isActive ? 'Đang bật' : 'Tạm tắt' }}</AppBadge><button class="icon-btn danger-button" title="Xóa quy tắc" @click="removeRule(rule)"><Trash2 :size="14" /></button></span></div><p>{{ formatCurrency(rule.spendPerPoint) }} = 1 điểm · 1 điểm = {{ formatCurrency(rule.redemptionValue) }}</p><small>Đổi tối thiểu {{ formatNumber(rule.minimumRedemptionPoints) }} điểm · Tối đa {{ rule.maximumRedemptionRate * 100 }}% hóa đơn</small></div><AppEmpty v-if="!rules.length" title="Chưa có quy tắc điểm" message="Cấu hình cách tích và sử dụng điểm loyalty." /></div></article>
    </section>

    <AppModal :open="tierOpen" title="Tạo hạng thành viên" @close="tierOpen = false"><form id="tier-form" class="form-grid" @submit.prevent="saveTier"><div class="field"><label>Mã hạng <span class="muted">(tự động)</span></label><input v-model.trim="tierForm.code" class="input" placeholder="Ví dụ: HTV-000001" /></div><div class="field"><label>Tên hạng</label><input v-model.trim="tierForm.name" class="input" required /></div><div class="field"><label>Cấp xếp hạng</label><AppNumberInput v-model="tierForm.rank" class="input" min="1" /></div><div class="field"><label>Chi tiêu tối thiểu</label><AppNumberInput v-model="tierForm.minEligibleSpend" class="input" min="0" /></div><div class="field"><label>Điểm tối thiểu</label><AppNumberInput v-model="tierForm.minEarnedPoints" class="input" min="0" /></div><div class="field"><label>Hệ số tích điểm</label><AppNumberInput v-model="tierForm.earnRate" class="input" min="0" step=".1" /></div><div class="field span-2"><label>Quyền lợi (phân cách bằng dấu phẩy)</label><input v-model="tierForm.benefitsText" class="input" /></div></form><template #footer><button class="btn btn-secondary" @click="tierOpen = false">Hủy</button><button class="btn btn-primary" form="tier-form" :disabled="saving">Tạo hạng</button></template></AppModal>
    <AppModal :open="ruleOpen" title="Tạo quy tắc tích điểm" @close="ruleOpen = false"><form id="rule-form" class="form-grid" @submit.prevent="saveRule"><div class="field span-2"><label>Tên quy tắc</label><input v-model.trim="ruleForm.name" class="input" required /></div><div class="field"><label>Chi tiêu cho 1 điểm</label><AppNumberInput v-model="ruleForm.spendPerPoint" class="input" min="1" /></div><div class="field"><label>Giá trị 1 điểm</label><AppNumberInput v-model="ruleForm.redemptionValue" class="input" min=".01" /></div><div class="field"><label>Điểm đổi tối thiểu</label><AppNumberInput v-model="ruleForm.minimumRedemptionPoints" class="input" min="1" /></div><div class="field"><label>Tỷ lệ giảm tối đa (0–1)</label><AppNumberInput v-model="ruleForm.maximumRedemptionRate" class="input" min=".01" max="1" step=".01" /></div><div class="field"><label>Ngày hiệu lực</label><input v-model="ruleForm.effectiveFrom" class="input" type="date" required /></div><div class="field"><label>Hạn điểm (ngày)</label><AppNumberInput v-model="ruleForm.pointExpiryDays" class="input" min="1" /></div></form><template #footer><button class="btn btn-secondary" @click="ruleOpen = false">Hủy</button><button class="btn btn-primary" form="rule-form" :disabled="saving">Tạo quy tắc</button></template></AppModal>
  </div>
</template>

<style scoped>
.tier-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px; }
.tier-card { display: grid; gap: 4px; padding: 22px; border-radius: 16px; box-shadow: var(--shadow); }
.tier-card > svg { margin-bottom: 8px; }
.tier-card span, .tier-card small { font-size: 11px; opacity: .72; }
.tier-card strong { font-size: 22px; }
.tier-card :deep(.entity-link) { color: inherit; }
.tier-card div { margin-top: 10px; font-size: 11px; }
.tier-0 { color: #f7f1e9; background: #4c4744; }
.tier-1 { color: #1f3342; background: #dce8ef; }
.tier-2 { color: #fff8df; background: linear-gradient(135deg, #8b630d, #d39a1f); }
.loyalty-grid { display: grid; grid-template-columns: 1.25fr .75fr; gap: 18px; }
.customer-link:hover { color: var(--blue); }
.rule-list { display: grid; gap: 12px; padding: 18px; }
.rule-card { padding: 15px; border: 1px solid var(--line); border-radius: 12px; }
.rule-card > div { display: flex; justify-content: space-between; gap: 10px; }
.rule-card p { margin: 10px 0 3px; color: var(--navy-950); font-weight: 700; }
.rule-card small { color: var(--muted); }
@media (max-width: 900px) { .tier-grid, .loyalty-grid { grid-template-columns: 1fr; } }
</style>
