<script setup lang="ts">
import { Edit3, Plus, Search, Trash2, UserCog } from '@lucide/vue'
import type { Employee, PagedResult } from '~/types/api'
import { formatNumber, statusLabel } from '~/utils/format'

const api = useApi()
const toast = useToast()
const result = ref<PagedResult<Employee>>({ items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 })
const search = ref('')
const loading = ref(true)
const saving = ref(false)
const modalOpen = ref(false)
const editing = ref<Employee>()
const form = reactive({
  employeeCode: '', fullName: '', phone: '', email: '', addressDetails: emptyAddressDetails(),
  hireDate: new Date().toISOString().slice(0, 10), position: 'Kỹ thuật viên',
  skillLevel: '', specialtiesText: '', baseSalary: 0, status: 'Active', notes: ''
})

const load = async (page = 1) => {
  loading.value = true
  try {
    result.value = await api.request('/employees', {
      query: { search: search.value || undefined, page, pageSize: 20 }
    })
  } finally { loading.value = false }
}

let timer: ReturnType<typeof setTimeout>
watch(search, () => {
  clearTimeout(timer)
  timer = setTimeout(() => load(1), 350)
})

const openForm = (employee?: Employee) => {
  editing.value = employee
  Object.assign(form, employee ? {
    ...employee,
    addressDetails: normalizeAddressDetails(employee.addressDetails, employee.address),
    hireDate: (employee as any).hireDate?.slice(0, 10) || new Date().toISOString().slice(0, 10),
    specialtiesText: employee.specialties?.join(', ') || '',
    baseSalary: (employee as any).baseSalary || 0,
    notes: (employee as any).notes || ''
  } : {
    employeeCode: '', fullName: '', phone: '', email: '', addressDetails: emptyAddressDetails(),
    hireDate: new Date().toISOString().slice(0, 10), position: 'Kỹ thuật viên',
    skillLevel: '', specialtiesText: '', baseSalary: 0, status: 'Active', notes: ''
  })
  modalOpen.value = true
}

const save = async () => {
  saving.value = true
  try {
    const payload = {
      ...form,
      address: formatAddressDetails(form.addressDetails),
      specialties: form.specialtiesText.split(',').map(x => x.trim()).filter(Boolean),
      hireDate: new Date(form.hireDate).toISOString(),
      dateOfBirth: null,
      userId: null
    }
    if (editing.value) await api.request(`/employees/${editing.value.id}`, { method: 'PUT', body: payload })
    else await api.request('/employees', { method: 'POST', body: payload })
    toast.success('Đã lưu nhân viên', `${form.fullName} đã được cập nhật.`)
    modalOpen.value = false
    await load(result.value.page)
  } finally { saving.value = false }
}
const remove = async () => {
  if (!editing.value || !confirm(`Xóa nhân viên ${editing.value.fullName}?`)) return
  await api.request(`/employees/${editing.value.id}`, { method: 'DELETE' })
  toast.success('Đã xóa nhân viên', editing.value.fullName)
  modalOpen.value = false
  await load(result.value.page)
}

onMounted(() => load())
</script>

<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1 class="page-title">Nhân viên</h1>
        <p class="page-subtitle">Quản lý đội ngũ kỹ thuật, chuyên môn và trạng thái làm việc.</p>
      </div>
      <button class="btn btn-accent" @click="openForm()"><Plus :size="17" /> Thêm nhân viên</button>
    </div>
    <section class="card">
      <header class="card-header">
        <div class="search-box">
          <Search :size="17" />
          <input v-model="search" class="input" placeholder="Tìm tên, mã, số điện thoại..." />
        </div>
        <span class="muted">{{ formatNumber(result.total) }} nhân viên</span>
      </header>
      <div class="table-wrap">
        <table v-if="result.items.length" class="data-table">
          <thead><tr><th>Nhân viên</th><th>Chức vụ</th><th>Chuyên môn</th><th>Trạng thái</th><th class="text-right">Thao tác</th></tr></thead>
          <tbody>
            <tr v-for="employee in result.items" :key="employee.id">
              <td><div class="cell-main">{{ employee.fullName }}</div><div class="cell-sub mono">{{ employee.employeeCode }} · {{ employee.phone }}</div></td>
              <td><div class="cell-main">{{ employee.position }}</div><div class="cell-sub">{{ employee.skillLevel || 'Chưa xếp bậc' }}</div></td>
              <td>{{ employee.specialties?.join(', ') || '—' }}</td>
              <td><AppBadge :tone="employee.status === 'Active' ? 'success' : employee.status === 'OnLeave' ? 'warning' : 'neutral'">{{ statusLabel(employee.status) }}</AppBadge></td>
              <td class="text-right"><button class="btn btn-secondary btn-sm" @click="openForm(employee)"><Edit3 :size="14" /> Sửa</button></td>
            </tr>
          </tbody>
        </table>
        <AppEmpty v-else-if="!loading" :icon="UserCog" title="Chưa có nhân viên" message="Thêm đội ngũ để phân công công việc sửa chữa." />
        <div v-else class="card-body"><div class="loading-skeleton" style="height: 260px" /></div>
      </div>
      <AppPagination :page="result.page" :total-pages="result.totalPages" :total="result.total" @change="load" />
    </section>

    <AppModal :open="modalOpen" :title="editing ? 'Cập nhật nhân viên' : 'Thêm nhân viên'" width="760px" @close="modalOpen = false">
      <form id="employee-form" class="form-grid" @submit.prevent="save">
        <div class="field"><label>Mã nhân viên <span class="muted">(tự động)</span></label><input v-model.trim="form.employeeCode" class="input" placeholder="Ví dụ: NV-000001" /></div>
        <div class="field"><label>Họ và tên *</label><input v-model.trim="form.fullName" class="input" required /></div>
        <div class="field"><label>Số điện thoại *</label><input v-model.trim="form.phone" class="input" required /></div>
        <div class="field"><label>Email</label><input v-model.trim="form.email" class="input" type="email" /></div>
        <div class="field"><label>Chức vụ</label><input v-model.trim="form.position" class="input" required /></div>
        <div class="field"><label>Cấp độ kỹ năng</label><input v-model.trim="form.skillLevel" class="input" placeholder="Senior, Junior..." /></div>
        <div class="field"><label>Ngày vào làm</label><input v-model="form.hireDate" class="input" type="date" required /></div>
        <div class="field"><label>Lương cơ bản</label><AppNumberInput v-model="form.baseSalary" class="input" min="0" /></div>
        <div class="field"><label>Trạng thái</label><select v-model="form.status" class="select"><option value="Active">Đang làm việc</option><option value="OnLeave">Nghỉ phép</option><option value="Inactive">Ngừng làm</option></select></div>
        <div class="field"><label>Chuyên môn</label><input v-model="form.specialtiesText" class="input" placeholder="Máy, điện, phanh..." /></div>
        <AppAddressFields v-model="form.addressDetails" />
        <div class="field span-2"><label>Ghi chú</label><textarea v-model.trim="form.notes" class="textarea" /></div>
      </form>
      <template #footer><button v-if="editing" class="btn btn-secondary danger-button" :disabled="saving" @click="remove"><Trash2 :size="15" /> Xóa</button><button class="btn btn-secondary" @click="modalOpen = false">Hủy</button><button class="btn btn-primary" form="employee-form" :disabled="saving">{{ saving ? 'Đang lưu...' : 'Lưu nhân viên' }}</button></template>
    </AppModal>
  </div>
</template>
