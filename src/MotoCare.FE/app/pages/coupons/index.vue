<script setup lang="ts">
import { Edit3, Plus, TicketPercent, Trash2 } from '@lucide/vue'
import type { Coupon, Customer, PagedResult } from '~/types/api'
import { entityDetailRoute } from '~/utils/entityRoute'
import { formatCurrency, formatDate, formatNumber } from '~/utils/format'

const api = useApi()
const toast = useToast()
const result = ref<PagedResult<Coupon>>({ items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 })
const customers = ref<Customer[]>([])
const search = ref('')
const modalOpen = ref(false)
const saving = ref(false)
const editing = ref<Coupon>()
const form = reactive({
  code: '', name: '', audience: 'All' as Coupon['audience'], minimumOrderAmount: 0,
  customerIds: [] as string[], discountType: 'Amount' as Coupon['discountType'],
  discountValue: 0, usageLimit: 0, startAt: '', endAt: '', description: '', isActive: true
})

const load = async (page = 1) => {
  const [coupons, customerPage] = await Promise.all([
    api.request<PagedResult<Coupon>>('/coupons', { query: { search: search.value || undefined, page, pageSize: 20 } }),
    api.request<PagedResult<Customer>>('/customers?pageSize=200')
  ])
  result.value = coupons
  customers.value = customerPage.items
}
let timer: ReturnType<typeof setTimeout>
watch(search, () => { clearTimeout(timer); timer = setTimeout(() => load(1), 300) })
const toLocal = (value?: string) => value ? new Date(value).toISOString().slice(0, 16) : ''
const openForm = (item?: Coupon) => {
  editing.value = item
  Object.assign(form, {
    code: item?.code || '', name: item?.name || '', audience: item?.audience || 'All',
    minimumOrderAmount: item?.minimumOrderAmount || 0, customerIds: [...(item?.customerIds || [])],
    discountType: item?.discountType || 'Amount', discountValue: item?.discountValue || 0,
    usageLimit: item?.usageLimit || 0, startAt: toLocal(item?.startAt), endAt: toLocal(item?.endAt),
    description: item?.description || '', isActive: item?.isActive ?? true
  })
  modalOpen.value = true
}
const save = async () => {
  saving.value = true
  try {
    const body = {
      ...form,
      code: form.code.trim(),
      usageLimit: form.usageLimit > 0 ? form.usageLimit : null,
      minimumOrderAmount: form.audience === 'MinimumOrder' ? form.minimumOrderAmount : 0,
      customerIds: form.audience === 'SpecificCustomers' ? form.customerIds : [],
      startAt: form.startAt ? new Date(form.startAt).toISOString() : null,
      endAt: form.endAt ? new Date(form.endAt).toISOString() : null
    }
    await api.request(`/coupons${editing.value ? `/${editing.value.id}` : ''}`, {
      method: editing.value ? 'PUT' : 'POST', body
    })
    toast.success('Đã lưu coupon', form.name)
    modalOpen.value = false
    await load(result.value.page)
  } finally { saving.value = false }
}
const remove = async (item: Coupon) => {
  if (!confirm(`Đánh dấu coupon ${item.code} là hết hạn? Coupon sẽ không còn được áp dụng.`)) return
  await api.request(`/coupons/${item.id}`, { method: 'DELETE' })
  toast.success('Coupon đã hết hạn', item.code)
  await load(result.value.page)
}
const audienceLabel = (value: Coupon['audience']) => value === 'All' ? 'Tất cả khách hàng' : value === 'MinimumOrder' ? 'Đơn tối thiểu' : 'Khách hàng chỉ định'
onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-header"><div><h1 class="page-title">Coupon</h1><p class="page-subtitle">Quản lý điều kiện áp dụng, mức giảm, thời hạn và số lượt sử dụng.</p></div><button class="btn btn-accent" @click="openForm()"><Plus :size="17" /> Thêm coupon</button></div>
    <section class="card">
      <header class="card-header"><div class="search-box"><TicketPercent :size="17" /><input v-model="search" class="input" placeholder="Tìm mã hoặc tên coupon..." /></div><span class="muted">{{ formatNumber(result.total) }} coupon</span></header>
      <div class="table-wrap"><table v-if="result.items.length" class="data-table"><thead><tr><th>Coupon</th><th>Điều kiện</th><th>Mức giảm</th><th>Thời gian</th><th class="text-right">Đã dùng / Giới hạn</th><th>Trạng thái</th><th class="text-right">Thao tác</th></tr></thead><tbody><tr v-for="item in result.items" :key="item.id"><td><AppEntityLink block :to="entityDetailRoute('Coupon', item.id)"><span class="cell-main">{{ item.name }}</span><span class="cell-sub mono">{{ item.code }}</span></AppEntityLink></td><td>{{ audienceLabel(item.audience) }}<div v-if="item.audience === 'MinimumOrder'" class="cell-sub">Từ {{ formatCurrency(item.minimumOrderAmount) }}</div><div v-if="item.audience === 'SpecificCustomers'" class="cell-sub">{{ item.customerIds.length }} khách hàng</div></td><td class="cell-main">{{ item.discountType === 'Percentage' ? `${item.discountValue}%` : formatCurrency(item.discountValue) }}</td><td>{{ item.startAt ? formatDate(item.startAt) : 'Không giới hạn' }} → {{ item.endAt ? formatDate(item.endAt) : 'Không giới hạn' }}</td><td class="text-right">{{ formatNumber(item.usedCount) }} / {{ item.usageLimit ? formatNumber(item.usageLimit) : '∞' }}</td><td><AppBadge :tone="item.isActive ? 'success' : 'neutral'">{{ item.isActive ? 'Đang dùng' : 'Tạm khóa' }}</AppBadge></td><td class="text-right"><div class="inline row-actions"><button class="icon-btn" title="Sửa" @click="openForm(item)"><Edit3 :size="15" /></button><button class="icon-btn danger-button" title="Xóa" @click="remove(item)"><Trash2 :size="15" /></button></div></td></tr></tbody></table><AppEmpty v-else :icon="TicketPercent" title="Chưa có coupon" message="Tạo coupon đầu tiên để áp dụng khi xuất hóa đơn." /></div>
      <AppPagination :page="result.page" :total-pages="result.totalPages" :total="result.total" @change="load" />
    </section>

    <AppModal :open="modalOpen" :title="editing ? 'Cập nhật coupon' : 'Thêm coupon'" width="780px" @close="modalOpen = false">
      <form id="coupon-form" class="form-grid" @submit.prevent="save">
        <div class="field"><label>Mã coupon <span class="muted">(tự động nếu trống)</span></label><input v-model.trim="form.code" class="input mono" placeholder="Ví dụ: CP-000001 hoặc GIAM20" /></div><div class="field"><label>Tên coupon *</label><input v-model.trim="form.name" class="input" required /></div>
        <div class="field"><label>Đối tượng áp dụng *</label><select v-model="form.audience" class="select" required><option value="All">Tất cả khách hàng</option><option value="MinimumOrder">Theo giá trị đơn tối thiểu</option><option value="SpecificCustomers">Danh sách khách hàng cụ thể</option></select></div>
        <div v-if="form.audience === 'MinimumOrder'" class="field"><label>Giá trị đơn tối thiểu *</label><AppNumberInput v-model="form.minimumOrderAmount" class="input" min="1" required /></div>
        <div v-if="form.audience === 'SpecificCustomers'" class="field span-2"><label>Khách hàng được áp dụng *</label><select v-model="form.customerIds" class="select customer-select" multiple required><option v-for="customer in customers" :key="customer.id" :value="customer.id">{{ customer.fullName }} · {{ customer.phone }}</option></select><small class="muted">Giữ Ctrl để chọn nhiều khách hàng.</small></div>
        <div class="field"><label>Kiểu giảm *</label><select v-model="form.discountType" class="select"><option value="Amount">Số tiền</option><option value="Percentage">Phần trăm (%)</option></select></div><div class="field"><label>Giá trị giảm *</label><AppNumberInput v-model="form.discountValue" class="input" min="0.01" :max="form.discountType === 'Percentage' ? 100 : undefined" required /></div>
        <div class="field"><label>Giới hạn lượt dùng</label><AppNumberInput v-model="form.usageLimit" class="input" min="0" placeholder="0 = không giới hạn" /></div><div class="field"><label>Trạng thái</label><select v-model="form.isActive" class="select"><option :value="true">Đang dùng</option><option :value="false">Tạm khóa</option></select></div>
        <div class="field"><label>Bắt đầu</label><input v-model="form.startAt" class="input" type="datetime-local" /></div><div class="field"><label>Kết thúc</label><input v-model="form.endAt" class="input" type="datetime-local" /></div>
        <div class="field span-2"><label>Mô tả</label><textarea v-model.trim="form.description" class="textarea" /></div>
      </form>
      <template #footer><button class="btn btn-secondary" @click="modalOpen = false">Hủy</button><button class="btn btn-primary" form="coupon-form" :disabled="saving">Lưu coupon</button></template>
    </AppModal>
  </div>
</template>

<style scoped>
.row-actions { justify-content: flex-end; }.customer-select { min-height: 130px; }
</style>
