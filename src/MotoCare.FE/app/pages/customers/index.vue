<script setup lang="ts">
import { Edit3, Plus, Search, Trash2, UserRound, Users } from '@lucide/vue'
import type { Customer, PagedResult } from '~/types/api'
import { formatNumber } from '~/utils/format'

const api = useApi()
const auth = useAuth()
const toast = useToast()
const isEmployee = computed(() => auth.hasAnyRole('Employee'))
const result = ref<PagedResult<Customer>>({ items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 })
const loading = ref(true)
const saving = ref(false)
const modalOpen = ref(false)
const search = ref('')
const editingId = ref<string>()
const form = reactive({
  fullName: '',
  phone: '',
  email: '',
  addressDetails: emptyAddressDetails(),
  dateOfBirth: '',
  gender: '',
  taxCode: '',
  notes: '',
  isActive: true
})

const load = async (page = result.value.page) => {
  loading.value = true
  try {
    result.value = await api.request<PagedResult<Customer>>('/customers', {
      query: { search: search.value || undefined, page, pageSize: 20 }
    })
  } finally {
    loading.value = false
  }
}

let timer: ReturnType<typeof setTimeout>
watch(search, () => {
  clearTimeout(timer)
  timer = setTimeout(() => load(1), 350)
})

const openCreate = () => {
  editingId.value = undefined
  Object.assign(form, {
    fullName: '', phone: '', email: '', addressDetails: emptyAddressDetails(), dateOfBirth: '',
    gender: '', taxCode: '', notes: '', isActive: true
  })
  modalOpen.value = true
}

const openEdit = (customer: Customer) => {
  editingId.value = customer.id
  Object.assign(form, {
    fullName: customer.fullName,
    phone: customer.phone,
    email: customer.email || '',
    addressDetails: normalizeAddressDetails(customer.addressDetails, customer.address),
    dateOfBirth: (customer as any).dateOfBirth?.slice(0, 10) || '',
    gender: (customer as any).gender || '',
    taxCode: customer.taxCode || '',
    notes: customer.notes || '',
    isActive: customer.isActive
  })
  modalOpen.value = true
}

const save = async () => {
  saving.value = true
  try {
    const payload = {
      ...form,
      address: formatAddressDetails(form.addressDetails),
      dateOfBirth: form.dateOfBirth || null
    }
    if (editingId.value) {
      await api.request(`/customers/${editingId.value}`, { method: 'PUT', body: payload })
    } else {
      await api.request('/customers', { method: 'POST', body: payload })
    }
    toast.success('Đã lưu khách hàng', `${form.fullName} đã được cập nhật.`)
    modalOpen.value = false
    await load(editingId.value ? result.value.page : 1)
  } finally {
    saving.value = false
  }
}

const remove = async () => {
  if (!editingId.value || !confirm(`Xóa khách hàng ${form.fullName}?`)) return
  await api.request(`/customers/${editingId.value}`, { method: 'DELETE' })
  toast.success('Đã xóa khách hàng', form.fullName)
  modalOpen.value = false
  await load(result.value.page)
}

onMounted(() => load(1))
</script>

<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1 class="page-title">Khách hàng & xe</h1>
        <p class="page-subtitle">Hồ sơ liên hệ, phương tiện, lịch sử sửa chữa và điểm loyalty.</p>
      </div>
      <button class="btn btn-accent" @click="openCreate">
        <Plus :size="17" /> Thêm khách hàng
      </button>
    </div>

    <section class="card">
      <header class="card-header">
        <div class="search-box">
          <Search :size="17" />
          <input v-model="search" class="input" placeholder="Tìm theo tên, mã, số điện thoại..." />
        </div>
        <span class="muted">{{ formatNumber(result.total) }} khách hàng</span>
      </header>
      <div class="table-wrap">
        <table v-if="result.items.length" class="data-table">
          <thead>
            <tr>
              <th>Khách hàng</th>
              <th>Liên hệ</th>
              <th>Hạng / điểm</th>
              <th>Trạng thái</th>
              <th class="text-right">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="customer in result.items" :key="customer.id">
              <td>
                <div class="identity">
                  <span><UserRound :size="17" /></span>
                  <div>
                    <NuxtLink class="cell-main link" :to="`/customers/${customer.id}`">
                      {{ customer.fullName }}
                    </NuxtLink>
                    <div class="cell-sub mono">{{ customer.code }}</div>
                  </div>
                </div>
              </td>
              <td>
                <div class="cell-main">{{ customer.phone }}</div>
                <div class="cell-sub">{{ customer.email || customer.address || '—' }}</div>
              </td>
              <td>
                <div class="cell-main">{{ customer.loyaltyTierCode || 'MEMBER' }}</div>
                <div class="cell-sub">{{ formatNumber(customer.loyaltyPointBalance || 0) }} điểm</div>
              </td>
              <td>
                <AppBadge :tone="customer.isActive ? 'success' : 'neutral'">
                  {{ customer.isActive ? 'Hoạt động' : 'Tạm khóa' }}
                </AppBadge>
              </td>
              <td class="text-right">
                <NuxtLink class="btn btn-secondary btn-sm" :to="`/customers/${customer.id}`">
                  <Edit3 :size="14" /> Sửa
                </NuxtLink>
              </td>
            </tr>
          </tbody>
        </table>
        <AppEmpty
          v-else-if="!loading"
          :icon="Users"
          title="Không tìm thấy khách hàng"
          message="Thử từ khóa khác hoặc thêm hồ sơ khách hàng mới."
        />
        <div v-else class="loading-list">
          <div v-for="n in 6" :key="n" class="loading-skeleton" />
        </div>
      </div>
      <AppPagination
        :page="result.page"
        :total-pages="result.totalPages"
        :total="result.total"
        @change="load"
      />
    </section>

    <AppModal
      :open="modalOpen"
      :title="editingId ? 'Cập nhật khách hàng' : 'Thêm khách hàng'"
      description="Thông tin này được dùng xuyên suốt phiếu sửa chữa và hóa đơn."
      width="760px"
      @close="modalOpen = false"
    >
      <form id="customer-form" class="form-grid" @submit.prevent="save">
        <div class="field">
          <label>Họ và tên *</label>
          <input v-model.trim="form.fullName" class="input" required maxlength="150" />
        </div>
        <div class="field">
          <label>Số điện thoại *</label>
          <input v-model.trim="form.phone" class="input" required maxlength="30" />
        </div>
        <div class="field">
          <label>Email</label>
          <input v-model.trim="form.email" class="input" type="email" />
        </div>
        <div class="field">
          <label>Mã số thuế</label>
          <input v-model.trim="form.taxCode" class="input" />
        </div>
        <div class="field">
          <label>Ngày sinh</label>
          <input v-model="form.dateOfBirth" class="input" type="date" />
        </div>
        <div class="field">
          <label>Giới tính</label>
          <select v-model="form.gender" class="select">
            <option value="">Không khai báo</option>
            <option value="Male">Nam</option>
            <option value="Female">Nữ</option>
            <option value="Other">Khác</option>
          </select>
        </div>
        <AppAddressFields v-model="form.addressDetails" />
        <div class="field span-2">
          <label>Ghi chú</label>
          <textarea v-model.trim="form.notes" class="textarea" maxlength="2000" />
        </div>
        <label class="check-row span-2">
          <input v-model="form.isActive" type="checkbox" /> Hồ sơ đang hoạt động
        </label>
      </form>
      <template #footer>
        <button v-if="editingId && !isEmployee" class="btn btn-secondary danger-button" :disabled="saving" @click="remove"><Trash2 :size="15" /> Xóa</button>
        <button class="btn btn-secondary" @click="modalOpen = false">Hủy</button>
        <button class="btn btn-primary" form="customer-form" :disabled="saving">
          {{ saving ? 'Đang lưu...' : 'Lưu khách hàng' }}
        </button>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.identity { display: flex; align-items: center; gap: 11px; }
.identity > span { display: grid; width: 34px; height: 34px; place-items: center; border-radius: 10px; color: var(--navy-800); background: var(--blue-soft); }
.link:hover { color: var(--blue); }
.loading-list { display: grid; gap: 14px; padding: 20px; }
.loading-list > div { min-height: 48px; }
.check-row { display: flex; align-items: center; gap: 9px; color: var(--navy-900); font-weight: 700; }
</style>
