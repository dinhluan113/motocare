<script setup lang="ts">
import { ArrowLeft, Banknote, ClipboardList, Printer, RotateCcw, XCircle } from '@lucide/vue'
import type { Invoice } from '~/types/api'
import { formatCurrency, formatDate, formatNumber, statusLabel, statusTone } from '~/utils/format'

const route = useRoute()
const api = useApi()
const toast = useToast()
const invoice = ref<Invoice>()
const loading = ref(true)
const saving = ref(false)
const paymentOpen = ref(false)
const refundOpen = ref(false)
const cancelOpen = ref(false)
const payment = reactive({ amount: 0, method: 'Cash', loyaltyPoints: 0, referenceCode: '', notes: '' })
const refundReason = ref('')
const cancelReason = ref('')

const invoiceId = computed(() => String(route.params.id))
const load = async () => {
  loading.value = true
  try { invoice.value = await api.request(`/invoices/${invoiceId.value}`) }
  finally { loading.value = false }
}
const openPayment = () => {
  Object.assign(payment, { amount: invoice.value?.remainingAmount || 0, method: 'Cash', loyaltyPoints: 0, referenceCode: '', notes: '' })
  paymentOpen.value = true
}
const addPayment = async () => {
  saving.value = true
  try {
    invoice.value = await api.request(`/invoices/${invoiceId.value}/payments`, {
      method: 'POST',
      body: { ...payment, idempotencyKey: crypto.randomUUID() }
    })
    toast.success('Đã ghi nhận thanh toán', formatCurrency(payment.amount))
    paymentOpen.value = false
  } finally { saving.value = false }
}
const refund = async () => {
  saving.value = true
  try {
    const updated = await api.request<Invoice>(`/invoices/${invoiceId.value}/refund`, { method: 'POST', body: { reason: refundReason.value } })
    invoice.value = updated
    toast.success('Đã hoàn tiền hóa đơn', updated.code)
    refundOpen.value = false
  } finally { saving.value = false }
}
const cancelInvoice = async () => {
  saving.value = true
  try {
    const updated = await api.request<Invoice>(`/invoices/${invoiceId.value}/cancel`, { method: 'POST', body: { reason: cancelReason.value } })
    invoice.value = updated
    toast.success('Đã hủy hóa đơn', updated.code)
    cancelOpen.value = false
  } finally { saving.value = false }
}
onMounted(load)
const printInvoice = () => window.print()
</script>

<template>
  <div class="page invoice-page">
    <NuxtLink to="/invoices" class="back-link no-print"><ArrowLeft :size="16" /> Danh sách hóa đơn</NuxtLink>
    <div v-if="invoice" class="page-header no-print">
      <div><div class="inline"><h1 class="page-title mono">{{ invoice.code }}</h1><AppBadge :tone="statusTone(invoice.paymentStatus)">{{ statusLabel(invoice.paymentStatus) }}</AppBadge></div><p class="page-subtitle">Ngày lập {{ formatDate(invoice.issueDate, true) }}</p></div>
      <div class="page-actions"><NuxtLink class="btn btn-secondary" :to="`/repair-orders/${invoice.repairOrderId}`"><ClipboardList :size="17" /> Xem phiếu sửa chữa</NuxtLink><button class="btn btn-secondary" @click="printInvoice"><Printer :size="17" /> In hóa đơn</button><button v-if="invoice.paymentStatus === 'Unpaid'" class="btn btn-secondary" @click="cancelOpen = true"><XCircle :size="17" /> Hủy hóa đơn</button><button v-if="invoice.paidAmount > 0 && invoice.paymentStatus !== 'Refunded'" class="btn btn-secondary" @click="refundOpen = true"><RotateCcw :size="17" /> Hoàn tiền</button><button v-if="invoice.remainingAmount > 0" class="btn btn-accent" @click="openPayment"><Banknote :size="17" /> Thanh toán</button></div>
    </div>

    <article v-if="invoice" class="invoice-paper">
      <header class="invoice-head">
        <div class="invoice-brand"><span>MC</span><div><strong>MOTOCARE</strong><small>Motorcycle Workshop</small></div></div>
        <div class="invoice-number"><small>HÓA ĐƠN BÁN HÀNG</small><strong>{{ invoice.code }}</strong><span>{{ formatDate(invoice.issueDate, true) }}</span></div>
      </header>
      <section class="customer-block"><div><span>Khách hàng</span><strong>{{ invoice.customerName }}</strong></div><div><span>Điện thoại</span><strong>{{ invoice.customerPhone }}</strong></div><div><span>Địa chỉ</span><strong>{{ invoice.customerAddress || '—' }}</strong></div></section>
      <div class="invoice-table-wrap">
        <table class="invoice-table">
          <thead><tr><th>#</th><th>Nội dung</th><th class="text-right">SL</th><th class="text-right">Đơn giá</th><th class="text-right">Giảm</th><th class="text-right">Thành tiền</th></tr></thead>
          <tbody>
            <tr v-for="(item, index) in invoice.items" :key="item.id">
              <td class="line-index" data-label="#">{{ index + 1 }}</td>
              <td class="line-description" data-label="Nội dung"><strong>{{ item.description }}</strong><span>{{ item.itemType === 'Part' ? 'Phụ tùng' : 'Dịch vụ' }}</span></td>
              <td class="line-quantity text-right" data-label="Số lượng">{{ item.itemType === 'Service' ? '—' : formatNumber(item.quantity) }}</td>
              <td class="line-price text-right" data-label="Đơn giá">{{ formatCurrency(item.unitPrice) }}</td>
              <td class="line-discount text-right" data-label="Giảm">{{ item.discountType === 'Percentage' ? `${item.discountValue}%` : formatCurrency(item.discountAmount) }}</td>
              <td class="line-total text-right" data-label="Thành tiền"><strong>{{ formatCurrency(item.lineTotal) }}</strong></td>
            </tr>
          </tbody>
        </table>
      </div>
      <section class="invoice-footer">
        <div class="payment-history"><h3>Lịch sử thanh toán</h3><div v-if="invoice.payments.length"><div v-for="item in invoice.payments" :key="item.id" class="payment-row"><span>{{ formatDate(item.paidAt, true) }} · {{ item.method }}</span><strong>{{ formatCurrency(item.amount) }}</strong></div></div><p v-else>Chưa có giao dịch thanh toán.</p></div>
        <div class="totals"><div><span>Tạm tính</span><strong>{{ formatCurrency(invoice.subtotal) }}</strong></div><div><span>Tổng giảm giá</span><strong>-{{ formatCurrency(invoice.discountAmount + invoice.loyaltyDiscountAmount) }}</strong></div><div v-if="invoice.couponCode"><span>Trong đó coupon {{ invoice.couponCode }}</span><strong>{{ formatCurrency(invoice.couponDiscountAmount) }}</strong></div><div><span>Thuế</span><strong>{{ formatCurrency(invoice.taxAmount) }}</strong></div><div class="grand-total"><span>Tổng cộng</span><strong>{{ formatCurrency(invoice.totalAmount) }}</strong></div><div><span>Đã thanh toán</span><strong>{{ formatCurrency(invoice.paidAmount) }}</strong></div><div class="remaining"><span>Còn phải thu</span><strong>{{ formatCurrency(invoice.remainingAmount) }}</strong></div></div>
      </section>
      <div class="invoice-note">Cảm ơn quý khách đã tin tưởng MotoCare. Vui lòng giữ hóa đơn để đối chiếu bảo hành.</div>
    </article>
    <div v-else-if="loading" class="loading-skeleton" style="height: 600px" />

    <AppModal :open="paymentOpen" title="Ghi nhận thanh toán" description="Có thể dùng điểm loyalty đồng thời với phương thức thanh toán." @close="paymentOpen = false">
      <form id="payment-form" class="form-grid" @submit.prevent="addPayment">
        <div class="field"><label>Số tiền</label><AppNumberInput v-model="payment.amount" class="input" min="0" :max="invoice?.remainingAmount" required /></div><div class="field"><label>Phương thức</label><select v-model="payment.method" class="select"><option value="Cash">Tiền mặt</option><option value="BankTransfer">Chuyển khoản</option><option value="Card">Thẻ</option><option value="EWallet">Ví điện tử</option></select></div>
        <div class="field"><label>Điểm loyalty sử dụng</label><AppNumberInput v-model="payment.loyaltyPoints" class="input" min="0" /></div><div class="field"><label>Mã tham chiếu</label><input v-model.trim="payment.referenceCode" class="input" /></div>
        <div class="field span-2"><label>Ghi chú</label><textarea v-model="payment.notes" class="textarea" /></div>
      </form>
      <template #footer><button class="btn btn-secondary" @click="paymentOpen = false">Hủy</button><button class="btn btn-accent" form="payment-form" :disabled="saving">Xác nhận thanh toán</button></template>
    </AppModal>

    <AppModal :open="refundOpen" title="Hoàn tiền hóa đơn" description="Thao tác này sẽ đảo điểm loyalty đã phát sinh." @close="refundOpen = false">
      <form id="refund-form" @submit.prevent="refund"><div class="field"><label>Lý do hoàn tiền *</label><textarea v-model.trim="refundReason" class="textarea" minlength="5" required /></div></form>
      <template #footer><button class="btn btn-secondary" @click="refundOpen = false">Hủy</button><button class="btn btn-danger" form="refund-form" :disabled="saving">Xác nhận hoàn tiền</button></template>
    </AppModal>

    <AppModal :open="cancelOpen" title="Hủy hóa đơn" description="Coupon của hóa đơn chưa thanh toán sẽ được hoàn lại một lượt." @close="cancelOpen = false">
      <form id="cancel-form" @submit.prevent="cancelInvoice"><div class="field"><label>Lý do hủy *</label><textarea v-model.trim="cancelReason" class="textarea" minlength="5" required /></div></form>
      <template #footer><button class="btn btn-secondary" @click="cancelOpen = false">Đóng</button><button class="btn btn-danger" form="cancel-form" :disabled="saving">Xác nhận hủy</button></template>
    </AppModal>
  </div>
</template>

<style scoped>
.invoice-page { width: 100%; max-width: 1050px; margin: 0 auto; }
.invoice-page > * { min-width: 0; }
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.invoice-paper { min-width: 0; padding: 42px; border: 1px solid var(--line); border-radius: 18px; background: white; box-shadow: var(--shadow); }
.invoice-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 20px; padding-bottom: 28px; border-bottom: 2px solid var(--navy-900); }
.invoice-brand { display: flex; align-items: center; gap: 12px; }
.invoice-brand > span { display: grid; width: 48px; height: 48px; place-items: center; border-radius: 12px; color: var(--navy-950); background: var(--amber); font-weight: 900; }
.invoice-brand strong, .invoice-brand small { display: block; }
.invoice-brand strong { color: var(--navy-950); font-size: 20px; }
.invoice-brand small { color: var(--muted); font-size: 10px; letter-spacing: .1em; text-transform: uppercase; }
.invoice-number { text-align: right; }
.invoice-number > * { display: block; }
.invoice-number small { color: var(--muted); font-weight: 800; letter-spacing: .08em; }
.invoice-number strong { margin: 4px 0; color: var(--navy-950); font-size: 20px; }
.invoice-number span { color: var(--muted); }
.customer-block { display: grid; grid-template-columns: 1fr 1fr 1.5fr; gap: 24px; margin: 26px 0; padding: 18px; border-radius: 12px; background: var(--surface-soft); }
.customer-block span, .customer-block strong { display: block; }
.customer-block span { color: var(--muted); font-size: 10px; font-weight: 700; text-transform: uppercase; }
.customer-block strong { margin-top: 4px; color: var(--navy-950); }
.invoice-table-wrap { width: 100%; min-width: 0; max-width: 100%; overflow-x: auto; overscroll-behavior-inline: contain; -webkit-overflow-scrolling: touch; }
.invoice-table { width: 100%; border-collapse: collapse; }
.invoice-table th { padding: 11px 12px; border-bottom: 1px solid var(--line); color: var(--muted); font-size: 10px; text-align: left; text-transform: uppercase; }
.invoice-table td { padding: 14px 12px; border-bottom: 1px solid var(--line); }
.invoice-table td span { display: block; color: var(--muted); font-size: 10px; }
.invoice-footer { display: grid; grid-template-columns: 1fr 340px; gap: 45px; margin-top: 26px; }
.payment-history h3 { margin: 0 0 10px; color: var(--navy-950); font-size: 13px; }
.payment-history p { color: var(--muted); font-size: 12px; }
.payment-row, .totals > div { display: flex; justify-content: space-between; gap: 15px; padding: 7px 0; color: var(--muted); font-size: 12px; }
.payment-row strong, .totals strong { color: var(--navy-950); }
.grand-total { margin-top: 6px; border-top: 1px solid var(--line); font-size: 15px !important; }
.grand-total strong { font-size: 18px; }
.remaining { color: var(--red) !important; font-weight: 800; }
.remaining strong { color: var(--red); }
.invoice-note { margin-top: 30px; padding-top: 18px; border-top: 1px dashed var(--line); color: var(--muted); font-size: 11px; text-align: center; }
@media (max-width: 720px) { .invoice-paper { padding: 22px; } .invoice-head { align-items: flex-start; flex-direction: column; }.invoice-number { text-align: left; }.customer-block, .invoice-footer { grid-template-columns: minmax(0, 1fr); }.invoice-footer { gap: 28px; }.payment-row, .totals > div { min-width: 0; }.payment-row span, .totals span { overflow-wrap: anywhere; } }
@media (max-width: 560px) {
  .invoice-table-wrap { overflow: visible; }
  .invoice-table { min-width: 0; }
  .invoice-table thead { display: none; }
  .invoice-table tbody, .invoice-table tr, .invoice-table td { display: block; width: 100%; }
  .invoice-table tr { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px 16px; padding: 15px 0; border-bottom: 1px solid var(--line); }
  .invoice-table td { min-width: 0; padding: 0; border: 0; text-align: left; overflow-wrap: anywhere; }
  .invoice-table td::before { display: block; margin-bottom: 3px; color: var(--muted); font-size: 9px; font-weight: 750; text-transform: uppercase; content: attr(data-label); }
  .invoice-table .line-index { display: none; }
  .invoice-table .line-description { grid-column: 1 / -1; padding-bottom: 7px; border-bottom: 1px dashed var(--line); }
  .invoice-table .line-total { color: var(--navy-950); }
}
@media (max-width: 480px) { .invoice-paper { padding: 16px 14px; border-radius: 14px; }.invoice-brand strong,.invoice-number strong { font-size: 17px; }.customer-block { gap: 14px; margin: 18px 0; padding: 14px; }.invoice-footer { gap: 24px; }.payment-row { align-items: flex-start; flex-direction: column; gap: 2px; }.totals > div { align-items: flex-start; }.totals > div > :last-child { flex: 0 0 auto; text-align: right; } }
@media print {
  .no-print, :global(.sidebar), :global(.topbar), :global(.toast-stack) { display: none !important; }
  :global(.main-column) { margin: 0 !important; }
  :global(.content) { padding: 0 !important; }
  .invoice-paper { border: 0; box-shadow: none; }
}
</style>
