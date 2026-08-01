<script setup lang="ts">
import {
  CheckCircle2,
  Copy,
  DatabaseZap,
  KeyRound,
  RefreshCw,
  Settings,
  ShieldAlert
} from '@lucide/vue'
import type { DemoDataResetResult, DemoDataStatus } from '~/types/api'

const api = useApi()
const toast = useToast()
const status = ref<DemoDataStatus>()
const loading = ref(true)
const resetOpen = ref(false)
const resetting = ref(false)
const acknowledged = ref(false)
const confirmation = ref('')
const result = ref<DemoDataResetResult>()

const phraseMatches = computed(() =>
  Boolean(status.value?.confirmationPhrase)
  && confirmation.value === status.value?.confirmationPhrase)

const load = async () => {
  loading.value = true
  try {
    status.value = await api.request<DemoDataStatus>('/settings/demo-data')
  } finally {
    loading.value = false
  }
}

const openReset = () => {
  confirmation.value = ''
  acknowledged.value = false
  resetOpen.value = true
}

const reset = async () => {
  if (!phraseMatches.value || !acknowledged.value) return
  resetting.value = true
  try {
    result.value = await api.request<DemoDataResetResult>('/settings/demo-data/reset', {
      method: 'POST',
      body: { confirmation: confirmation.value }
    })
    resetOpen.value = false
    toast.success('Đã tạo lại dữ liệu mẫu', 'Toàn bộ dữ liệu liên quan đã được kiểm tra và ghi nhận thành công.')
  } finally {
    resetting.value = false
  }
}

const copyAccount = async (username: string, password: string) => {
  await navigator.clipboard.writeText(`Tên đăng nhập: ${username}\nMật khẩu: ${password}`)
  toast.success('Đã sao chép tài khoản', username)
}

onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-header">
      <div>
        <div class="breadcrumb">Quản trị <span>›</span> Cài đặt nội bộ</div>
        <h1 class="page-title">Cài đặt</h1>
        <p class="page-subtitle">Công cụ chuẩn bị môi trường trình diễn trước khi bàn giao hệ thống.</p>
      </div>
      <AppBadge tone="warning">Chỉ dùng nội bộ</AppBadge>
    </div>

    <section v-if="loading" class="card loading-card">
      <RefreshCw class="spin" :size="24" />
      <span>Đang kiểm tra cấu hình môi trường…</span>
    </section>

    <AppEmpty
      v-else-if="!status?.enabled"
      :icon="Settings"
      title="Công cụ nội bộ đã được ẩn"
      message="Môi trường hiện tại không cho phép xóa và tạo lại dữ liệu mẫu."
    />

    <template v-else>
      <section class="card demo-card">
        <div class="demo-copy">
          <div class="feature-icon"><DatabaseZap :size="25" /></div>
          <div>
            <div class="inline-title">
              <h2>Tạo lại toàn bộ dữ liệu mẫu</h2>
              <AppBadge tone="danger">Có xóa dữ liệu</AppBadge>
            </div>
            <p>
              Xóa dữ liệu hiện có và dựng một xưởng xe máy mẫu với số liệu liên kết đầy đủ,
              phù hợp để trình bày quy trình hằng ngày và kiểm tra báo cáo.
            </p>
          </div>
        </div>

        <div class="scope-grid">
          <div v-for="(item, index) in status.scope" :key="item">
            <span>{{ index + 1 }}</span>
            <p>{{ item }}</p>
          </div>
        </div>

        <div class="safety-note">
          <ShieldAlert :size="21" />
          <div>
            <strong>Thao tác không thể hoàn tác</strong>
            <p>
              Tài khoản Admin đang đăng nhập được giữ nguyên để không mất quyền truy cập.
              Mọi tài khoản khác và toàn bộ dữ liệu nghiệp vụ cũ sẽ bị thay thế.
            </p>
          </div>
        </div>

        <div class="demo-actions">
          <div>
            <strong>Đã có kiểm tra toàn vẹn trước khi ghi dữ liệu</strong>
            <span>Tham chiếu, tồn kho, công nợ và số dư điểm phải khớp mới được lưu.</span>
          </div>
          <button class="btn btn-danger" @click="openReset">
            <DatabaseZap :size="17" /> Xóa cũ và tạo dữ liệu mẫu
          </button>
        </div>
      </section>

      <section v-if="result" class="card result-card">
        <header class="result-head">
          <div class="success-icon"><CheckCircle2 :size="23" /></div>
          <div>
            <h2>Dữ liệu mẫu đã sẵn sàng</h2>
            <p>Hoàn tất lúc {{ formatDate(result.completedAt, true) }}. Có thể mở Tổng quan để bắt đầu trình diễn.</p>
          </div>
          <NuxtLink class="btn btn-primary" to="/">Mở Tổng quan</NuxtLink>
        </header>

        <div class="count-grid">
          <div v-for="(count, label) in result.counts" :key="label">
            <strong>{{ formatNumber(count) }}</strong>
            <span>{{ label }}</span>
          </div>
        </div>

        <div class="account-section">
          <div class="account-title">
            <KeyRound :size="19" />
            <div><strong>Tài khoản phân quyền để trình diễn</strong><span>Mật khẩu chỉ hiển thị sau lần tạo dữ liệu này.</span></div>
          </div>
          <div class="account-grid">
            <article v-for="account in result.demoAccounts" :key="account.username">
              <div><AppBadge :tone="account.role === 'Manager' ? 'warning' : 'neutral'">{{ account.role === 'Manager' ? 'Quản lý' : 'Nhân viên' }}</AppBadge></div>
              <strong>{{ account.fullName }}</strong>
              <dl><dt>Tên đăng nhập</dt><dd class="mono">{{ account.username }}</dd><dt>Mật khẩu</dt><dd class="mono">{{ account.password }}</dd></dl>
              <button class="btn btn-secondary btn-sm" @click="copyAccount(account.username, account.password)"><Copy :size="14" /> Sao chép</button>
            </article>
          </div>
        </div>
      </section>
    </template>

    <AppModal
      :open="resetOpen"
      title="Xác nhận tạo lại dữ liệu mẫu"
      description="Hãy đọc kỹ vì toàn bộ dữ liệu cũ sẽ bị xóa."
      width="650px"
      @close="!resetting && (resetOpen = false)"
    >
      <div class="confirm-content">
        <div class="alert alert-danger">
          <ShieldAlert :size="20" />
          <div><strong>Dữ liệu cũ không thể khôi phục từ thao tác này</strong><div>Chỉ tiếp tục khi đây đúng là môi trường trình diễn hoặc kiểm thử.</div></div>
        </div>
        <label class="confirm-check">
          <input v-model="acknowledged" type="checkbox" />
          <span>Tôi hiểu mọi dữ liệu nghiệp vụ và tài khoản khác sẽ bị xóa, chỉ giữ Admin hiện tại.</span>
        </label>
        <div class="field">
          <label>Nhập chính xác câu xác nhận</label>
          <code>{{ status?.confirmationPhrase }}</code>
          <input v-model="confirmation" class="input mono" autocomplete="off" :placeholder="status?.confirmationPhrase" />
          <small :class="{ matched: phraseMatches }">{{ phraseMatches ? 'Câu xác nhận chính xác.' : 'Phân biệt chữ hoa, chữ thường và khoảng trắng.' }}</small>
        </div>
      </div>
      <template #footer>
        <button class="btn btn-secondary" :disabled="resetting" @click="resetOpen = false">Hủy</button>
        <button class="btn btn-danger" :disabled="resetting || !acknowledged || !phraseMatches" @click="reset">
          <RefreshCw v-if="resetting" class="spin" :size="16" />
          <DatabaseZap v-else :size="16" />
          {{ resetting ? 'Đang tạo dữ liệu…' : 'Xác nhận xóa và tạo lại' }}
        </button>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.breadcrumb { margin-bottom: 7px; color: var(--muted); font-size: 11px; font-weight: 700; }.breadcrumb span { padding: 0 5px; }.loading-card { display: flex; min-height: 180px; align-items: center; justify-content: center; gap: 10px; color: var(--muted); }.demo-card { overflow: hidden; }.demo-copy { display: grid; grid-template-columns: 52px 1fr; gap: 15px; padding: 24px; }.feature-icon,.success-icon { display: grid; width: 48px; height: 48px; place-items: center; border-radius: 13px; color: #9b2c2c; background: var(--red-soft); }.inline-title { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }.inline-title h2,.result-head h2 { margin: 0; color: var(--navy-950); font-size: 19px; }.demo-copy p,.result-head p,.safety-note p,.demo-actions span,.account-title span { margin: 6px 0 0; color: var(--muted); font-size: 12px; }.scope-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1px; border-block: 1px solid var(--line); background: var(--line); }.scope-grid > div { display: flex; min-height: 78px; align-items: center; gap: 10px; padding: 14px 18px; background: #fafcfd; }.scope-grid span { display: grid; min-width: 27px; height: 27px; place-items: center; border-radius: 8px; color: var(--navy-900); background: var(--amber-soft); font-size: 11px; font-weight: 800; }.scope-grid p { margin: 0; color: var(--navy-800); font-size: 12px; font-weight: 650; }.safety-note { display: flex; gap: 11px; margin: 20px 24px; padding: 14px; border: 1px solid #f1c7c7; border-radius: 11px; color: #9b2c2c; background: #fff8f8; }.safety-note p { color: #8a4a4a; }.demo-actions { display: flex; align-items: center; justify-content: space-between; gap: 18px; padding: 0 24px 24px; }.demo-actions strong,.demo-actions span { display: block; }.demo-actions strong { color: var(--navy-900); font-size: 12px; }.result-card { overflow: hidden; }.result-head { display: grid; grid-template-columns: 48px 1fr auto; align-items: center; gap: 14px; padding: 22px 24px; border-bottom: 1px solid var(--line); }.success-icon { color: #157a6e; background: #e6f6f3; }.count-grid { display: grid; grid-template-columns: repeat(5, 1fr); border-bottom: 1px solid var(--line); }.count-grid div { padding: 17px; border-right: 1px solid var(--line); }.count-grid div:nth-child(5n) { border-right: 0; }.count-grid strong,.count-grid span { display: block; }.count-grid strong { color: var(--navy-950); font-size: 20px; }.count-grid span { margin-top: 3px; color: var(--muted); font-size: 10px; }.account-section { padding: 22px 24px 24px; }.account-title { display: flex; align-items: center; gap: 9px; margin-bottom: 13px; color: var(--navy-900); }.account-title strong,.account-title span { display: block; }.account-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px; }.account-grid article { padding: 16px; border: 1px solid var(--line); border-radius: 12px; background: #fafcfd; }.account-grid article > strong { display: block; margin: 10px 0; color: var(--navy-950); }.account-grid dl { display: grid; grid-template-columns: 110px 1fr; gap: 5px; margin: 0 0 12px; font-size: 11px; }.account-grid dt { color: var(--muted); }.account-grid dd { margin: 0; color: var(--navy-900); font-weight: 700; }.confirm-content { display: grid; gap: 18px; }.confirm-check { display: flex; align-items: flex-start; gap: 9px; padding: 13px; border: 1px solid var(--line); border-radius: 10px; color: var(--navy-900); font-size: 12px; cursor: pointer; }.confirm-check input { margin-top: 2px; accent-color: var(--red); }.field code { width: max-content; padding: 5px 8px; border-radius: 6px; color: var(--navy-900); background: #f0f3f5; font-size: 12px; }.field small { color: var(--muted); }.field small.matched { color: var(--teal); font-weight: 700; }.spin { animation: spin 900ms linear infinite; } @keyframes spin { to { transform: rotate(360deg); } }
@media (max-width: 900px) { .scope-grid { grid-template-columns: repeat(2, 1fr); }.count-grid { grid-template-columns: repeat(3, 1fr); }.count-grid div:nth-child(5n) { border-right: 1px solid var(--line); }.demo-actions,.result-head { align-items: stretch; grid-template-columns: 48px 1fr; }.result-head .btn { grid-column: 1 / -1; }.demo-actions { flex-direction: column; }.demo-actions .btn { width: 100%; } }
@media (max-width: 620px) { .scope-grid,.account-grid,.count-grid { grid-template-columns: 1fr; }.demo-copy { grid-template-columns: 1fr; }.scope-grid > div,.count-grid div { border-right: 0; }.result-head { grid-template-columns: 1fr; }.account-grid dl { grid-template-columns: 1fr; }.account-grid dd { margin-bottom: 5px; } }
</style>
