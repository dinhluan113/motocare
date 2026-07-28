<script setup lang="ts">
import { Eye, EyeOff, LockKeyhole, Wrench } from '@lucide/vue'

definePageMeta({ layout: false })

const route = useRoute()
const auth = useAuth()
const toast = useToast()
const username = ref('admin')
const password = ref('Admin@123456')
const showPassword = ref(false)
const loading = ref(false)

const submit = async () => {
  loading.value = true
  try {
    await auth.login(username.value, password.value)
    const realtime = useRealtimeNotifications()
    await Promise.allSettled([realtime.load(), realtime.connect()])
    toast.success('Đăng nhập thành công', 'Chào mừng trở lại MotoCare.')
    await navigateTo(String(route.query.redirect || '/'))
  } catch (error: any) {
    toast.error(
      'Không thể đăng nhập',
      error?.data?.message || 'Vui lòng kiểm tra tên đăng nhập và mật khẩu.'
    )
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main class="login-page">
    <section class="login-story">
      <div class="story-logo">
        <Wrench :size="24" />
        <span>MotoCare</span>
      </div>
      <div class="story-copy">
        <p class="eyebrow">WORKSHOP OPERATING SYSTEM</p>
        <h1>Lorem Ipsum.<br><em>Lorem ipsum dolor sit amet.</em></h1>
        <p>
          Nullam lacinia ultrices est eu tincidunt. Maecenas malesuada nunc sem, quis pharetra sapien ullamcorper ut.
        </p>
      </div>
      <div class="story-metrics">
        <div>
          <strong>Realtime</strong>
          <span>Tiến độ sửa chữa</span>
        </div>
        <div>
          <strong>01 sed</strong>
          <span>Dữ liệu tập trung</span>
        </div>
        <div>
          <strong>360°</strong>
          <span>Hồ sơ khách hàng</span>
        </div>
      </div>
    </section>

    <section class="login-panel">
      <form class="login-card" @submit.prevent="submit">
        <div class="mobile-logo">
          <Wrench :size="21" />
          <strong>MotoCare</strong>
        </div>
        <div>
          <p class="eyebrow">XIN CHÀO</p>
          <h2>Đăng nhập hệ thống</h2>
          <p class="description">Sử dụng tài khoản được cấp để tiếp tục ca làm việc.</p>
        </div>

        <div class="field">
          <label for="username">Tên đăng nhập</label>
          <input id="username" v-model.trim="username" class="input" autocomplete="username" required>
        </div>

        <div class="field">
          <label for="password">Mật khẩu</label>
          <div class="password-field">
            <LockKeyhole :size="17" />
            <input id="password" v-model="password" :type="showPassword ? 'text' : 'password'"
              autocomplete="current-password" required>
            <button type="button" :aria-label="showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'"
              @click="showPassword = !showPassword">
              <EyeOff v-if="showPassword" :size="17" />
              <Eye v-else :size="17" />
            </button>
          </div>
        </div>

        <button class="btn btn-accent submit" :disabled="loading">
          <span v-if="loading" class="spinner" />
          {{ loading ? 'Đang xác thực...' : 'Đăng nhập' }}
        </button>

        <p class="login-note">
          Tài khoản phát triển đã được điền sẵn. Hãy đổi mật khẩu sau lần đăng nhập đầu tiên.
        </p>
      </form>
    </section>
  </main>
</template>

<style scoped>
.login-page {
  display: grid;
  min-height: 100vh;
  grid-template-columns: minmax(420px, 1.05fr) minmax(420px, 0.95fr);
  background: white;
}

.login-story {
  position: relative;
  display: flex;
  overflow: hidden;
  flex-direction: column;
  justify-content: space-between;
  padding: clamp(32px, 5vw, 72px);
  color: white;
  background:
    radial-gradient(circle at 78% 18%, rgb(245 158 11 / 22%), transparent 16rem),
    linear-gradient(145deg, #0b2136, #143b59);
}

.login-story::before,
.login-story::after {
  position: absolute;
  border: 1px solid rgb(255 255 255 / 8%);
  border-radius: 50%;
  content: '';
}

.login-story::before {
  right: -15%;
  bottom: 8%;
  width: 420px;
  height: 420px;
}

.login-story::after {
  right: 2%;
  bottom: 20%;
  width: 260px;
  height: 260px;
}

.story-logo,
.mobile-logo {
  display: flex;
  align-items: center;
  gap: 11px;
  color: var(--amber);
}

.story-logo span {
  color: white;
  font-size: 21px;
  font-weight: 800;
}

.story-copy {
  position: relative;
  z-index: 1;
  max-width: 620px;
}

.eyebrow {
  margin: 0 0 12px;
  color: var(--amber);
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 0.18em;
}

.story-copy h1 {
  margin: 0;
  font-size: clamp(2.6rem, 5vw, 5.4rem);
  line-height: 0.98;
  letter-spacing: -0.06em;
}

.story-copy h1 em {
  color: var(--amber);
  font-style: normal;
}

.story-copy>p:last-child {
  max-width: 520px;
  margin: 24px 0 0;
  color: #aac0d2;
  font-size: 15px;
}

.story-metrics {
  position: relative;
  z-index: 1;
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  border-top: 1px solid rgb(255 255 255 / 10%);
}

.story-metrics div {
  padding: 18px 16px 0 0;
}

.story-metrics strong,
.story-metrics span {
  display: block;
}

.story-metrics strong {
  color: white;
  font-size: 15px;
}

.story-metrics span {
  margin-top: 3px;
  color: #86a2b9;
  font-size: 10px;
}

.login-panel {
  display: grid;
  place-items: center;
  padding: 32px;
  background:
    radial-gradient(circle at 100% 0, var(--amber-soft), transparent 20rem),
    #fbfcfd;
}

.login-card {
  display: grid;
  width: min(430px, 100%);
  gap: 19px;
  padding: 36px;
  border: 1px solid var(--line);
  border-radius: 22px;
  background: white;
  box-shadow: 0 30px 80px rgb(10 31 51 / 12%);
}

.login-card h2 {
  margin: 0;
  color: var(--navy-950);
  font-size: 1.75rem;
  letter-spacing: -0.04em;
}

.description {
  margin: 6px 0 0;
  color: var(--muted);
  font-size: 13px;
}

.mobile-logo {
  display: none;
}

.password-field {
  position: relative;
  display: flex;
  align-items: center;
}

.password-field>svg {
  position: absolute;
  left: 12px;
  color: #8795a3;
}

.password-field input {
  width: 100%;
  height: 42px;
  padding: 0 42px 0 38px;
  border: 1px solid #cfd8e1;
  border-radius: 10px;
  outline: none;
}

.password-field input:focus {
  border-color: var(--blue);
  box-shadow: 0 0 0 3px rgb(47 127 179 / 12%);
}

.password-field button {
  position: absolute;
  right: 8px;
  display: grid;
  width: 30px;
  height: 30px;
  place-items: center;
  border: 0;
  color: #748290;
  background: transparent;
}

.submit {
  width: 100%;
  margin-top: 2px;
}

.spinner {
  width: 16px;
  height: 16px;
  border: 2px solid rgb(16 42 67 / 25%);
  border-top-color: var(--navy-900);
  border-radius: 50%;
  animation: spin 0.7s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.login-note {
  margin: 0;
  color: #8a96a3;
  font-size: 10px;
  text-align: center;
}

@media (max-width: 900px) {
  .login-page {
    grid-template-columns: 1fr;
  }

  .login-story {
    display: none;
  }

  .login-panel {
    min-height: 100vh;
    padding: 20px;
  }

  .mobile-logo {
    display: flex;
  }

  .mobile-logo strong {
    color: var(--navy-950);
    font-size: 18px;
  }
}
</style>
