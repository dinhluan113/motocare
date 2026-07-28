<script setup lang="ts">
import { Bike, Boxes, Factory, Plus } from '@lucide/vue'
import type { PagedResult, PartBrand, VehicleBrand, VehicleModel } from '~/types/api'

type Tab = 'vehicleBrands' | 'vehicleModels' | 'partBrands'
const api = useApi()
const toast = useToast()
const activeTab = ref<Tab>('vehicleBrands')
const vehicleBrands = ref<VehicleBrand[]>([])
const vehicleModels = ref<VehicleModel[]>([])
const partBrands = ref<PartBrand[]>([])
const modalOpen = ref(false)
const saving = ref(false)
const editing = ref<any>()
const form = reactive({ code: '', name: '', country: '', brandId: '', vehicleType: '', engineCapacityCc: 0, contactInfo: '', isActive: true })

const load = async () => {
  const [vb, vm, pb] = await Promise.all([
    api.request<PagedResult<VehicleBrand>>('/vehicle-brands?pageSize=200'),
    api.request<PagedResult<VehicleModel>>('/vehicle-models?pageSize=200'),
    api.request<PagedResult<PartBrand>>('/part-brands?pageSize=200')
  ])
  vehicleBrands.value = vb.items; vehicleModels.value = vm.items; partBrands.value = pb.items
}
const routeForTab = () => activeTab.value === 'vehicleBrands' ? 'vehicle-brands' : activeTab.value === 'vehicleModels' ? 'vehicle-models' : 'part-brands'
const openForm = (item?: any) => {
  editing.value = item
  Object.assign(form, {
    code: item?.code || '', name: item?.name || '', country: item?.country || '',
    brandId: item?.brandId || vehicleBrands.value[0]?.id || '',
    vehicleType: item?.vehicleType || '', engineCapacityCc: item?.engineCapacityCc || 0,
    contactInfo: item?.contactInfo || '', isActive: item?.isActive ?? true
  })
  modalOpen.value = true
}
const save = async () => {
  saving.value = true
  try {
    const endpoint = routeForTab()
    const payload = activeTab.value === 'vehicleModels'
      ? { brandId: form.brandId, code: form.code, name: form.name, vehicleType: form.vehicleType, engineCapacityCc: form.engineCapacityCc || null, isActive: form.isActive }
      : activeTab.value === 'partBrands'
        ? { code: form.code, name: form.name, country: form.country, contactInfo: form.contactInfo, isActive: form.isActive }
        : { code: form.code, name: form.name, country: form.country, isActive: form.isActive }
    await api.request(`/${endpoint}${editing.value ? `/${editing.value.id}` : ''}`, { method: editing.value ? 'PUT' : 'POST', body: payload })
    toast.success('Đã lưu danh mục', form.name); modalOpen.value = false; await load()
  } finally { saving.value = false }
}
const title = computed(() => activeTab.value === 'vehicleBrands' ? 'hãng xe' : activeTab.value === 'vehicleModels' ? 'dòng xe' : 'hãng phụ tùng')
const vehicleBrandOptions = computed(() => vehicleBrands.value.map(item => ({
  code: item.id,
  name: item.name
})))
onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-header"><div><h1 class="page-title">Danh mục hệ thống</h1><p class="page-subtitle">Dữ liệu nền dùng khi khai báo xe và phụ tùng trong kho.</p></div><button class="btn btn-accent" @click="openForm()"><Plus :size="17" /> Thêm {{ title }}</button></div>
    <div class="catalog-tabs">
      <button :class="{ active: activeTab === 'vehicleBrands' }" @click="activeTab = 'vehicleBrands'"><Factory :size="17" /> Hãng xe <span>{{ vehicleBrands.length }}</span></button>
      <button :class="{ active: activeTab === 'vehicleModels' }" @click="activeTab = 'vehicleModels'"><Bike :size="17" /> Dòng xe <span>{{ vehicleModels.length }}</span></button>
      <button :class="{ active: activeTab === 'partBrands' }" @click="activeTab = 'partBrands'"><Boxes :size="17" /> Hãng phụ tùng <span>{{ partBrands.length }}</span></button>
    </div>
    <section class="card">
      <div class="table-wrap">
        <table v-if="activeTab === 'vehicleBrands'" class="data-table"><thead><tr><th>Mã</th><th>Tên hãng</th><th>Quốc gia</th><th>Trạng thái</th><th class="text-right">Thao tác</th></tr></thead><tbody><tr v-for="item in vehicleBrands" :key="item.id"><td class="mono">{{ item.code }}</td><td class="cell-main">{{ item.name }}</td><td>{{ item.country || '—' }}</td><td><AppBadge :tone="item.isActive ? 'success' : 'neutral'">{{ item.isActive ? 'Hoạt động' : 'Tạm khóa' }}</AppBadge></td><td class="text-right"><button class="btn btn-secondary btn-sm" @click="openForm(item)">Chỉnh sửa</button></td></tr></tbody></table>
        <table v-else-if="activeTab === 'vehicleModels'" class="data-table"><thead><tr><th>Mã</th><th>Dòng xe</th><th>Hãng</th><th>Loại / phân khối</th><th>Trạng thái</th><th class="text-right">Thao tác</th></tr></thead><tbody><tr v-for="item in vehicleModels" :key="item.id"><td class="mono">{{ item.code }}</td><td class="cell-main">{{ item.name }}</td><td>{{ vehicleBrands.find(x => x.id === item.brandId)?.name || '—' }}</td><td>{{ item.vehicleType || '—' }} · {{ item.engineCapacityCc ? `${item.engineCapacityCc}cc` : '—' }}</td><td><AppBadge :tone="item.isActive ? 'success' : 'neutral'">{{ item.isActive ? 'Hoạt động' : 'Tạm khóa' }}</AppBadge></td><td class="text-right"><button class="btn btn-secondary btn-sm" @click="openForm(item)">Chỉnh sửa</button></td></tr></tbody></table>
        <table v-else class="data-table"><thead><tr><th>Mã</th><th>Hãng phụ tùng</th><th>Quốc gia</th><th>Liên hệ</th><th>Trạng thái</th><th class="text-right">Thao tác</th></tr></thead><tbody><tr v-for="item in partBrands" :key="item.id"><td class="mono">{{ item.code }}</td><td class="cell-main">{{ item.name }}</td><td>{{ item.country || '—' }}</td><td>{{ (item as any).contactInfo || '—' }}</td><td><AppBadge :tone="item.isActive ? 'success' : 'neutral'">{{ item.isActive ? 'Hoạt động' : 'Tạm khóa' }}</AppBadge></td><td class="text-right"><button class="btn btn-secondary btn-sm" @click="openForm(item)">Chỉnh sửa</button></td></tr></tbody></table>
      </div>
    </section>
    <AppModal :open="modalOpen" :title="`${editing ? 'Cập nhật' : 'Thêm'} ${title}`" @close="modalOpen = false">
      <form id="catalog-form" class="form-grid" @submit.prevent="save">
        <div class="field"><label>Mã *</label><input v-model.trim="form.code" class="input" required /></div><div class="field"><label>Tên *</label><input v-model.trim="form.name" class="input" required /></div>
        <template v-if="activeTab === 'vehicleModels'"><div class="field"><label>Hãng xe *</label><AppSearchSelect v-model="form.brandId" :options="vehicleBrandOptions" placeholder="Chọn hãng xe" search-placeholder="Tìm hãng xe..." required :clearable="false" /></div><div class="field"><label>Loại xe</label><input v-model.trim="form.vehicleType" class="input" placeholder="Xe số, tay ga, côn tay..." /></div><div class="field"><label>Phân khối (cc)</label><AppNumberInput v-model="form.engineCapacityCc" class="input" min="0" /></div></template>
        <template v-else><div class="field"><label>Quốc gia</label><AppCountrySelect v-model="form.country" /></div><div v-if="activeTab === 'partBrands'" class="field span-2"><label>Thông tin liên hệ</label><input v-model.trim="form.contactInfo" class="input" /></div></template>
      </form>
      <template #footer><button class="btn btn-secondary" @click="modalOpen = false">Hủy</button><button class="btn btn-primary" form="catalog-form" :disabled="saving">Lưu danh mục</button></template>
    </AppModal>
  </div>
</template>

<style scoped>
.catalog-tabs { display: flex; gap: 8px; overflow-x: auto; padding-bottom: 2px; }
.catalog-tabs button { display: inline-flex; min-width: max-content; align-items: center; gap: 8px; padding: 11px 15px; border: 1px solid var(--line); border-radius: 11px; color: var(--muted); background: white; font-weight: 800; }
.catalog-tabs button.active { border-color: var(--navy-900); color: white; background: var(--navy-900); }
.catalog-tabs span { display: grid; min-width: 22px; height: 22px; place-items: center; border-radius: 99px; color: var(--navy-900); background: var(--amber-soft); font-size: 10px; }
</style>
