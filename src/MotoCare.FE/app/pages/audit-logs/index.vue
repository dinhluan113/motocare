<script setup lang="ts">
import { Eye, History, Search } from '@lucide/vue'
import type { AuditLog, PagedResult } from '~/types/api'
import { entityDetailRoute } from '~/utils/entityRoute'
import { formatDate } from '~/utils/format'

const api = useApi()
const auth = useAuth()
const isAdmin = computed(() => auth.hasAnyRole('Admin', 'Administrator'))
const result = ref<PagedResult<AuditLog>>({ items: [], total: 0, page: 1, pageSize: 30, totalPages: 0 })
const search = ref('')
const action = ref('')
const entityType = ref('')
const selected = ref<AuditLog>()
const detailModal = ref(false)
const loading = ref(true)
const actionOptions = [{ code: '', name: 'Tất cả hành động' }, { code: 'CREATE', name: 'Thêm mới' }, { code: 'UPDATE', name: 'Cập nhật' }, { code: 'DELETE', name: 'Xóa' }, { code: 'CONFIRM', name: 'Xác nhận' }]
const entityOptions = [{ code: '', name: 'Tất cả dữ liệu' }, { code: 'customers', name: 'Khách hàng' }, { code: 'vehicles', name: 'Phương tiện' }, { code: 'repair-orders', name: 'Phiếu sửa chữa' }, { code: 'invoices', name: 'Hóa đơn' }, { code: 'coupons', name: 'Coupon' }, { code: 'parts', name: 'Phụ tùng' }, { code: 'cash-transactions', name: 'Thu chi' }, { code: 'suppliers', name: 'Nhà cung cấp' }, { code: 'users', name: 'Tài khoản' }]
const load = async (page = 1) => {
  loading.value = true
  try { result.value = await api.request('/audit-logs', { query: { search: search.value || undefined, action: action.value || undefined, entityType: entityType.value || undefined, page, pageSize: 30 } }) }
  finally { loading.value = false }
}
let timer: ReturnType<typeof setTimeout>
watch(search, () => { clearTimeout(timer); timer = setTimeout(() => load(), 350) })
watch([action, entityType], () => load())
const openDetail = (item: AuditLog) => { selected.value = item; detailModal.value = true }
const prettyJson = (value?: string) => {
  if (!value) return 'Không có dữ liệu'
  try { return JSON.stringify(JSON.parse(value), null, 2) } catch { return value }
}
const actionLabel = (value: string) => ({ CREATE: 'Thêm mới', UPDATE: 'Cập nhật', DELETE: 'Xóa', CONFIRM: 'Xác nhận' }[value] || value)
const auditUserRoute = (item: AuditLog) =>
  isAdmin.value && item.userId && item.userId.toLowerCase() !== 'system'
    ? entityDetailRoute('User', item.userId)
    : undefined
const auditEntityRoute = (item: AuditLog) => {
  if (item.entityType === 'users' && !isAdmin.value) return undefined
  if (item.entityType === 'loyalty') {
    if (item.requestPath.includes('/loyalty/tiers/')) return entityDetailRoute('LoyaltyTier', item.entityId)
    if (item.requestPath.includes('/loyalty/rules/')) return entityDetailRoute('LoyaltyRule', item.entityId)
  }
  return entityDetailRoute(item.entityType, item.entityId)
}
onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-header"><div><h1 class="page-title">Lịch sử thao tác</h1><p class="page-subtitle">Tra cứu mọi thay đổi dữ liệu, người thực hiện và nội dung trước–sau.</p></div></div>
    <section class="card"><header class="card-header audit-filters"><div class="search-box"><Search :size="17" /><input v-model="search" class="input" placeholder="Tìm người dùng, mã dữ liệu, đường dẫn..." /></div><AppSearchSelect v-model="action" :options="actionOptions" :clearable="false" /><AppSearchSelect v-model="entityType" :options="entityOptions" :clearable="false" /><span class="muted">{{ result.total }} thao tác</span></header>
      <div class="table-wrap"><table v-if="result.items.length" class="data-table"><thead><tr><th>Thời gian</th><th>Người thực hiện</th><th>Hành động</th><th>Dữ liệu</th><th>Định danh</th><th>IP</th><th class="text-right">Chi tiết</th></tr></thead><tbody><tr v-for="item in result.items" :key="item.id"><td>{{ formatDate(item.createdAt, true) }}</td><td><AppEntityLink class="cell-main" :to="auditUserRoute(item)">{{ item.userDisplayName || item.username || 'Hệ thống' }}</AppEntityLink><div class="cell-sub">{{ item.username }}</div></td><td><AppBadge :tone="item.action === 'DELETE' ? 'danger' : item.action === 'CREATE' ? 'success' : 'warning'">{{ actionLabel(item.action) }}</AppBadge></td><td>{{ item.entityType }}</td><td><AppEntityLink class="mono" :to="auditEntityRoute(item)">{{ item.entityId || '—' }}</AppEntityLink></td><td class="mono">{{ item.ipAddress || '—' }}</td><td class="text-right"><button class="icon-btn" title="Xem dữ liệu trước và sau" @click="openDetail(item)"><Eye :size="15" /></button></td></tr></tbody></table><AppEmpty v-else-if="!loading" :icon="History" title="Chưa có lịch sử" message="Các thao tác thêm, sửa, xóa sẽ được ghi nhận tại đây." /></div>
      <AppPagination :page="result.page" :total-pages="result.totalPages" :total="result.total" @change="load" />
    </section>
    <AppModal :open="detailModal" title="Chi tiết lịch sử thao tác" width="980px" @close="detailModal = false"><template v-if="selected"><div class="audit-meta"><div><span>Người thực hiện</span><strong><AppEntityLink :to="auditUserRoute(selected)">{{ selected.userDisplayName || selected.username || 'Hệ thống' }}</AppEntityLink></strong></div><div><span>Dữ liệu liên quan</span><strong><AppEntityLink :to="auditEntityRoute(selected)">{{ selected.entityType }} · {{ selected.entityId }}</AppEntityLink></strong></div><div><span>Thời gian</span><strong>{{ formatDate(selected.createdAt, true) }}</strong></div><div><span>Hành động</span><strong>{{ actionLabel(selected.action) }}</strong></div><div class="wide"><span>Đường dẫn</span><strong class="mono">{{ selected.requestPath }}</strong></div></div><div class="audit-compare"><section><h3>Dữ liệu trước</h3><pre>{{ prettyJson(selected.beforeData) }}</pre></section><section><h3>Dữ liệu sau</h3><pre>{{ prettyJson(selected.afterData) }}</pre></section></div></template><template #footer><button class="btn btn-secondary" @click="detailModal = false">Đóng</button></template></AppModal>
  </div>
</template>

<style scoped>
.audit-filters { display: grid; grid-template-columns: minmax(260px,1fr) 170px 190px auto; gap: 9px; align-items: center; }.audit-meta { display: grid; grid-template-columns: repeat(2,1fr); gap: 9px; margin-bottom: 14px; }.audit-meta > div { padding: 11px 13px; border: 1px solid var(--line); border-radius: 9px; background: #f9fbfc; }.audit-meta .wide { grid-column: 1 / -1; }.audit-meta span,.audit-meta strong { display: block; }.audit-meta span { color: var(--muted); font-size: 10px; }.audit-meta strong { margin-top: 4px; font-size: 12px; }.audit-compare { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 12px; }.audit-compare h3 { font-size: 12px; }.audit-compare pre { max-height: 480px; overflow: auto; padding: 13px; border-radius: 10px; color: #dce8f1; background: var(--navy-950); font-size: 10px; white-space: pre-wrap; word-break: break-word; } @media(max-width:850px){.audit-filters,.audit-meta,.audit-compare{grid-template-columns:1fr;}.audit-meta .wide{grid-column:auto;}}
</style>
