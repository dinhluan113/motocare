<script setup lang="ts">
import { ArrowDownCircle, ArrowUpCircle, Plus, WalletCards } from '@lucide/vue'
import type { PagedResult } from '~/types/api'
import { formatCurrency, formatDate } from '~/utils/format'

interface CashTransaction {
  id: string
  code: string
  type: 'Income' | 'Expense'
  category: string
  transactionDate: string
  amount: number
  paymentMethod: string
  description: string
  status: string
}
const api = useApi()
const toast = useToast()
const result = ref<PagedResult<CashTransaction>>({ items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 })
const modalOpen = ref(false)
const saving = ref(false)
const form = reactive({ code: '', type: 'Expense', category: '', transactionDate: new Date().toISOString().slice(0, 10), amount: 0, paymentMethod: 'Cash', description: '', attachmentUrl: '', createdBy: '', status: 'Approved' })
const load = async (page = 1) => { result.value = await api.request('/cash-transactions', { query: { page, pageSize: 20 } }) }
const save = async () => {
  saving.value = true
  try {
    await api.request('/cash-transactions', { method: 'POST', body: { ...form, transactionDate: new Date(form.transactionDate).toISOString(), referenceType: null, referenceId: null, approvedBy: null } })
    toast.success('Đã ghi nhận thu chi', form.description); modalOpen.value = false; await load()
  } finally { saving.value = false }
}
const income = computed(() => result.value.items.filter(x => x.type === 'Income').reduce((a, x) => a + x.amount, 0))
const expense = computed(() => result.value.items.filter(x => x.type === 'Expense').reduce((a, x) => a + x.amount, 0))
onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-header"><div><h1 class="page-title">Thu chi</h1><p class="page-subtitle">Sổ quỹ vận hành, chi phí và các khoản thu ngoài hóa đơn sửa chữa.</p></div><button class="btn btn-accent" @click="modalOpen = true"><Plus :size="17" /> Ghi nhận giao dịch</button></div>
    <section class="cash-summary"><article><ArrowDownCircle :size="22" /><div><span>Tổng thu trên trang</span><strong>{{ formatCurrency(income) }}</strong></div></article><article><ArrowUpCircle :size="22" /><div><span>Tổng chi trên trang</span><strong>{{ formatCurrency(expense) }}</strong></div></article><article><WalletCards :size="22" /><div><span>Chênh lệch</span><strong>{{ formatCurrency(income - expense) }}</strong></div></article></section>
    <section class="card"><header class="card-header"><h2 class="card-title">Sổ giao dịch</h2><span class="muted">{{ result.total }} giao dịch</span></header><div class="table-wrap"><table v-if="result.items.length" class="data-table"><thead><tr><th>Mã</th><th>Ngày</th><th>Loại / danh mục</th><th>Nội dung</th><th>Phương thức</th><th class="text-right">Số tiền</th></tr></thead><tbody><tr v-for="item in result.items" :key="item.id"><td class="mono">{{ item.code }}</td><td>{{ formatDate(item.transactionDate) }}</td><td><AppBadge :tone="item.type === 'Income' ? 'success' : 'danger'">{{ item.type === 'Income' ? 'Thu' : 'Chi' }}</AppBadge><div class="cell-sub">{{ item.category }}</div></td><td class="cell-main">{{ item.description }}</td><td>{{ item.paymentMethod }}</td><td class="text-right cell-main" :class="item.type === 'Income' ? 'income' : 'expense'">{{ item.type === 'Income' ? '+' : '-' }}{{ formatCurrency(item.amount) }}</td></tr></tbody></table><AppEmpty v-else :icon="WalletCards" title="Chưa có giao dịch thu chi" message="Ghi nhận khoản thu hoặc chi đầu tiên." /></div><AppPagination :page="result.page" :total-pages="result.totalPages" :total="result.total" @change="load" /></section>
    <AppModal :open="modalOpen" title="Ghi nhận thu chi" @close="modalOpen = false"><form id="cash-form" class="form-grid" @submit.prevent="save"><div class="field"><label>Loại giao dịch</label><select v-model="form.type" class="select"><option value="Income">Khoản thu</option><option value="Expense">Khoản chi</option></select></div><div class="field"><label>Mã giao dịch *</label><input v-model.trim="form.code" class="input" required /></div><div class="field"><label>Ngày giao dịch</label><input v-model="form.transactionDate" class="input" type="date" required /></div><div class="field"><label>Danh mục *</label><input v-model.trim="form.category" class="input" required placeholder="Điện nước, nhập hàng..." /></div><div class="field"><label>Số tiền *</label><AppNumberInput v-model="form.amount" class="input" min="1" required /></div><div class="field"><label>Phương thức</label><select v-model="form.paymentMethod" class="select"><option value="Cash">Tiền mặt</option><option value="BankTransfer">Chuyển khoản</option><option value="Card">Thẻ</option></select></div><div class="field span-2"><label>Nội dung *</label><textarea v-model.trim="form.description" class="textarea" required /></div></form><template #footer><button class="btn btn-secondary" @click="modalOpen = false">Hủy</button><button class="btn btn-primary" form="cash-form" :disabled="saving">Lưu giao dịch</button></template></AppModal>
  </div>
</template>

<style scoped>
.cash-summary { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; }
.cash-summary article { display: flex; align-items: center; gap: 13px; padding: 20px; border: 1px solid var(--line); border-radius: 15px; background: white; box-shadow: var(--shadow); }
.cash-summary article:nth-child(1) svg { color: var(--teal); }.cash-summary article:nth-child(2) svg { color: var(--red); }.cash-summary article:nth-child(3) svg { color: var(--blue); }
.cash-summary span, .cash-summary strong { display: block; }
.cash-summary span { color: var(--muted); font-size: 11px; }
.cash-summary strong { color: var(--navy-950); font-size: 20px; }
.income { color: var(--teal); }.expense { color: var(--red); }
@media (max-width: 760px) { .cash-summary { grid-template-columns: 1fr; } }
</style>
