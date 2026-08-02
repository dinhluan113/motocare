<script setup lang="ts">
import { Bike, Boxes, Factory, ListTree, Plus, Sparkles, Trash2 } from '@lucide/vue'
import type { PagedResult, PartBrand, PartCategory, ServiceCategory, VehicleBrand, VehicleModel } from '~/types/api'
import { formatCurrency } from '~/utils/format'

type Tab = 'vehicleBrands' | 'vehicleModels' | 'partBrands' | 'partCategories' | 'serviceCategories'
type SpecificationDataType = 'Text' | 'Number' | 'Boolean' | 'Selection'
interface SpecificationDefinition { code: string, name: string, dataType: SpecificationDataType, options: string[], unit: string, isRequired: boolean }
const api = useApi()
const auth = useAuth()
const toast = useToast()
const isAdmin = computed(() => auth.hasAnyRole('Admin', 'Administrator'))
const activeTab = ref<Tab>('vehicleBrands')
const vehicleBrands = ref<VehicleBrand[]>([])
const vehicleModels = ref<VehicleModel[]>([])
const partBrands = ref<PartBrand[]>([])
const partCategories = ref<PartCategory[]>([])
const serviceCategories = ref<ServiceCategory[]>([])
const modalOpen = ref(false)
const saving = ref(false)
const editing = ref<any>()
const form = reactive({ code: '', name: '', country: '', brandId: '', vehicleType: '', engineCapacityCc: 0, contactInfo: '', description: '', defaultPrice: 0, specificationDefinitions: [] as SpecificationDefinition[], isActive: true })
const specificationTypeOptions = [
  { value: 'Text', label: 'Ký tự' },
  { value: 'Number', label: 'Số' },
  { value: 'Boolean', label: 'Có/Không' },
  { value: 'Selection', label: 'Danh sách lựa chọn' }
] as const
const specificationTypeLabel = (type: SpecificationDataType) =>
  specificationTypeOptions.find(x => x.value === type)?.label || 'Ký tự'

const load = async () => {
  const [vb, vm, pb, pc, sc] = await Promise.all([
    api.request<PagedResult<VehicleBrand>>('/vehicle-brands?pageSize=200'),
    api.request<PagedResult<VehicleModel>>('/vehicle-models?pageSize=200'),
    api.request<PagedResult<PartBrand>>('/part-brands?pageSize=200'),
    api.request<PagedResult<PartCategory>>('/part-categories?pageSize=200'),
    api.request<PagedResult<ServiceCategory>>('/service-categories?pageSize=200')
  ])
  vehicleBrands.value = vb.items; vehicleModels.value = vm.items; partBrands.value = pb.items; partCategories.value = pc.items; serviceCategories.value = sc.items
}
const routeForTab = () => activeTab.value === 'vehicleBrands' ? 'vehicle-brands' : activeTab.value === 'vehicleModels' ? 'vehicle-models' : activeTab.value === 'partBrands' ? 'part-brands' : activeTab.value === 'serviceCategories' ? 'service-categories' : 'part-categories'
const openForm = (item?: any) => {
  editing.value = item
  Object.assign(form, {
    code: item?.code || '', name: item?.name || '', country: item?.country || '',
    brandId: item?.brandId || vehicleBrands.value[0]?.id || '',
    vehicleType: item?.vehicleType || '', engineCapacityCc: item?.engineCapacityCc || 0,
    contactInfo: item?.contactInfo || '', description: item?.description || '', defaultPrice: item?.defaultPrice || 0,
    specificationDefinitions: (item?.specificationDefinitions || []).map((x: any) => ({
      code: x.code, name: x.name, dataType: x.dataType || 'Text', options: x.options || [],
      unit: x.unit || '', isRequired: x.isRequired
    })),
    isActive: item?.isActive ?? true
  })
  modalOpen.value = true
}
const save = async () => {
  saving.value = true
  try {
    const endpoint = routeForTab()
    const payload = activeTab.value === 'serviceCategories'
      ? { code: form.code, name: form.name, defaultPrice: form.defaultPrice, description: form.description, isActive: form.isActive }
      : activeTab.value === 'vehicleModels'
      ? { brandId: form.brandId, code: form.code, name: form.name, vehicleType: form.vehicleType, engineCapacityCc: form.engineCapacityCc || null, isActive: form.isActive }
      : activeTab.value === 'partBrands'
        ? { code: form.code, name: form.name, country: form.country, contactInfo: form.contactInfo, isActive: form.isActive }
        : activeTab.value === 'partCategories'
          ? { code: form.code, name: form.name, description: form.description, specificationDefinitions: form.specificationDefinitions, isActive: form.isActive }
          : { code: form.code, name: form.name, country: form.country, isActive: form.isActive }
    await api.request(`/${endpoint}${editing.value ? `/${editing.value.id}` : ''}`, { method: editing.value ? 'PUT' : 'POST', body: payload })
    toast.success('Đã lưu danh mục', form.name); modalOpen.value = false; await load()
  } finally { saving.value = false }
}
const remove = async () => {
  if (!editing.value || !confirm(`Xóa ${title.value} ${editing.value.name}?`)) return
  await api.request(`/${routeForTab()}/${editing.value.id}`, { method: 'DELETE' })
  toast.success('Đã xóa danh mục', editing.value.name)
  modalOpen.value = false
  await load()
}
const title = computed(() => activeTab.value === 'vehicleBrands' ? 'hãng xe' : activeTab.value === 'vehicleModels' ? 'dòng xe' : activeTab.value === 'partBrands' ? 'hãng phụ tùng' : activeTab.value === 'serviceCategories' ? 'dịch vụ' : 'danh mục phụ tùng')
const codePlaceholder = computed(() => ({
  vehicleBrands: 'HX-000001', vehicleModels: 'DX-000001', partBrands: 'HPT-000001',
  partCategories: 'DMPT-000001', serviceCategories: 'DV-000001'
})[activeTab.value])
const vehicleBrandOptions = computed(() => vehicleBrands.value.map(item => ({
  code: item.id,
  name: item.name
})))
const addSpecification = () => {
  const used = new Set(form.specificationDefinitions.map(x => x.code.toUpperCase()))
  let number = 1
  let code = ''
  do { code = `TSKT-${String(number++).padStart(3, '0')}` } while (used.has(code))
  form.specificationDefinitions.push({ code, name: '', dataType: 'Text', options: [], unit: '', isRequired: false })
}
const removeSpecification = (index: number) => form.specificationDefinitions.splice(index, 1)
const setSpecificationOptions = (index: number, value: string) => {
  form.specificationDefinitions[index]!.options = value.split(',').map(x => x.trim()).filter(Boolean)
}
onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-header"><div><h1 class="page-title">Danh mục hệ thống</h1><p class="page-subtitle">Dữ liệu nền dùng khi khai báo xe, dịch vụ và phụ tùng trong kho.</p></div><button v-if="activeTab !== 'serviceCategories' || isAdmin" class="btn btn-accent" @click="openForm()"><Plus :size="17" /> Thêm {{ title }}</button></div>
    <div class="catalog-tabs">
      <button :class="{ active: activeTab === 'vehicleBrands' }" @click="activeTab = 'vehicleBrands'"><Factory :size="17" /> Hãng xe <span>{{ vehicleBrands.length }}</span></button>
      <button :class="{ active: activeTab === 'vehicleModels' }" @click="activeTab = 'vehicleModels'"><Bike :size="17" /> Dòng xe <span>{{ vehicleModels.length }}</span></button>
      <button :class="{ active: activeTab === 'partBrands' }" @click="activeTab = 'partBrands'"><Boxes :size="17" /> Hãng phụ tùng <span>{{ partBrands.length }}</span></button>
      <button :class="{ active: activeTab === 'partCategories' }" @click="activeTab = 'partCategories'"><ListTree :size="17" /> Danh mục phụ tùng <span>{{ partCategories.length }}</span></button>
      <button :class="{ active: activeTab === 'serviceCategories' }" @click="activeTab = 'serviceCategories'"><Sparkles :size="17" /> Dịch vụ <span>{{ serviceCategories.length }}</span></button>
    </div>
    <section class="card">
      <div class="table-wrap">
        <table v-if="activeTab === 'vehicleBrands'" class="data-table"><thead><tr><th>Mã</th><th>Tên hãng</th><th>Quốc gia</th><th>Trạng thái</th><th class="text-right">Thao tác</th></tr></thead><tbody><tr v-for="item in vehicleBrands" :key="item.id"><td class="mono">{{ item.code }}</td><td class="cell-main">{{ item.name }}</td><td>{{ item.country || '—' }}</td><td><AppBadge :tone="item.isActive ? 'success' : 'neutral'">{{ item.isActive ? 'Hoạt động' : 'Tạm khóa' }}</AppBadge></td><td class="text-right"><button class="btn btn-secondary btn-sm" @click="openForm(item)">Chỉnh sửa</button></td></tr></tbody></table>
        <table v-else-if="activeTab === 'vehicleModels'" class="data-table"><thead><tr><th>Mã</th><th>Dòng xe</th><th>Hãng</th><th>Loại / phân khối</th><th>Trạng thái</th><th class="text-right">Thao tác</th></tr></thead><tbody><tr v-for="item in vehicleModels" :key="item.id"><td class="mono">{{ item.code }}</td><td class="cell-main">{{ item.name }}</td><td>{{ vehicleBrands.find(x => x.id === item.brandId)?.name || '—' }}</td><td>{{ item.vehicleType || '—' }} · {{ item.engineCapacityCc ? `${item.engineCapacityCc}cc` : '—' }}</td><td><AppBadge :tone="item.isActive ? 'success' : 'neutral'">{{ item.isActive ? 'Hoạt động' : 'Tạm khóa' }}</AppBadge></td><td class="text-right"><button class="btn btn-secondary btn-sm" @click="openForm(item)">Chỉnh sửa</button></td></tr></tbody></table>
        <table v-else-if="activeTab === 'partBrands'" class="data-table"><thead><tr><th>Mã</th><th>Hãng phụ tùng</th><th>Quốc gia</th><th>Liên hệ</th><th>Trạng thái</th><th class="text-right">Thao tác</th></tr></thead><tbody><tr v-for="item in partBrands" :key="item.id"><td class="mono">{{ item.code }}</td><td class="cell-main">{{ item.name }}</td><td>{{ item.country || '—' }}</td><td>{{ (item as any).contactInfo || '—' }}</td><td><AppBadge :tone="item.isActive ? 'success' : 'neutral'">{{ item.isActive ? 'Hoạt động' : 'Tạm khóa' }}</AppBadge></td><td class="text-right"><button class="btn btn-secondary btn-sm" @click="openForm(item)">Chỉnh sửa</button></td></tr></tbody></table>
        <table v-else-if="activeTab === 'partCategories'" class="data-table"><thead><tr><th>Mã</th><th>Danh mục phụ tùng</th><th>Thông số kỹ thuật</th><th>Mô tả</th><th>Trạng thái</th><th class="text-right">Thao tác</th></tr></thead><tbody><tr v-for="item in partCategories" :key="item.id"><td class="mono">{{ item.code }}</td><td class="cell-main">{{ item.name }}</td><td><span v-if="item.specificationDefinitions?.length">{{ item.specificationDefinitions.map(x => `${x.name} · ${specificationTypeLabel(x.dataType || 'Text')}${x.unit ? ` (${x.unit})` : ''}`).join(', ') }}</span><span v-else>—</span></td><td>{{ item.description || '—' }}</td><td><AppBadge :tone="item.isActive ? 'success' : 'neutral'">{{ item.isActive ? 'Hoạt động' : 'Tạm khóa' }}</AppBadge></td><td class="text-right"><button class="btn btn-secondary btn-sm" @click="openForm(item)">Chỉnh sửa</button></td></tr></tbody></table>
        <table v-else class="data-table"><thead><tr><th>Mã</th><th>Dịch vụ</th><th class="text-right">Giá mặc định</th><th>Mô tả</th><th>Trạng thái</th><th v-if="isAdmin" class="text-right">Thao tác</th></tr></thead><tbody><tr v-for="item in serviceCategories" :key="item.id"><td class="mono">{{ item.code }}</td><td class="cell-main">{{ item.name }}</td><td class="text-right">{{ formatCurrency(item.defaultPrice) }}</td><td>{{ item.description || '—' }}</td><td><AppBadge :tone="item.isActive ? 'success' : 'neutral'">{{ item.isActive ? 'Hoạt động' : 'Tạm khóa' }}</AppBadge></td><td v-if="isAdmin" class="text-right"><button class="btn btn-secondary btn-sm" @click="openForm(item)">Chỉnh sửa</button></td></tr></tbody></table>
      </div>
    </section>
    <AppModal :open="modalOpen" :title="`${editing ? 'Cập nhật' : 'Thêm'} ${title}`" :width="activeTab === 'partCategories' ? '820px' : undefined" @close="modalOpen = false">
      <form id="catalog-form" class="form-grid" @submit.prevent="save">
        <div class="field"><label>Mã <span class="muted">(tự động nếu để trống)</span></label><input v-model.trim="form.code" class="input" :placeholder="`Ví dụ: ${codePlaceholder}`" /></div><div class="field"><label>Tên *</label><input v-model.trim="form.name" class="input" required /></div>
        <template v-if="activeTab === 'vehicleModels'"><div class="field"><label>Hãng xe *</label><AppSearchSelect v-model="form.brandId" :options="vehicleBrandOptions" placeholder="Chọn hãng xe" search-placeholder="Tìm hãng xe..." required :clearable="false" /></div><div class="field"><label>Loại xe</label><input v-model.trim="form.vehicleType" class="input" placeholder="Xe số, tay ga, côn tay..." /></div><div class="field"><label>Phân khối (cc)</label><AppNumberInput v-model="form.engineCapacityCc" class="input" min="0" /></div></template>
        <template v-else-if="activeTab === 'serviceCategories'"><div class="field"><label>Giá mặc định</label><AppNumberInput v-model="form.defaultPrice" class="input" min="0" /></div><div class="field span-2"><label>Mô tả</label><textarea v-model.trim="form.description" class="textarea" placeholder="Ví dụ: Rửa xe, thay nhớt, vệ sinh bugi, tiền công..." /></div></template>
        <template v-else-if="activeTab !== 'partCategories'"><div class="field"><label>Quốc gia</label><AppCountrySelect v-model="form.country" /></div><div v-if="activeTab === 'partBrands'" class="field span-2"><label>Thông tin liên hệ</label><input v-model.trim="form.contactInfo" class="input" /></div></template>
        <template v-else>
          <div class="field span-2"><label>Mô tả</label><textarea v-model.trim="form.description" class="textarea" placeholder="Ví dụ: lọc gió, lốp xe, đèn xe..." /></div>
          <div class="field span-2 spec-editor">
            <div class="spec-head"><div><label>Thông số kỹ thuật</label><small>Mỗi danh mục có một bộ thông số riêng.</small></div><button type="button" class="btn btn-secondary btn-sm" @click="addSpecification"><Plus :size="14" /> Thêm thông số</button></div>
            <div v-for="(spec, index) in form.specificationDefinitions" :key="index" class="spec-item">
              <div class="spec-row">
                <input v-model.trim="spec.code" class="input" required placeholder="Mã (VD: SIZE)" />
                <input v-model.trim="spec.name" class="input" required placeholder="Tên (VD: Kích thước)" />
                <select v-model="spec.dataType" class="select" required><option v-for="type in specificationTypeOptions" :key="type.value" :value="type.value">{{ type.label }}</option></select>
                <input v-if="spec.dataType !== 'Boolean'" v-model.trim="spec.unit" class="input" placeholder="Đơn vị" /><span v-else class="muted">Không có đơn vị</span>
                <label class="required-check"><input v-model="spec.isRequired" type="checkbox" /> Bắt buộc</label>
                <button type="button" class="icon-btn" title="Xóa" @click="removeSpecification(index)"><Trash2 :size="15" /></button>
              </div>
              <div v-if="spec.dataType === 'Selection'" class="selection-options"><label>Các lựa chọn *</label><input class="input" required :value="spec.options.join(', ')" placeholder="Ví dụ: Trước, Sau" @input="setSpecificationOptions(index, ($event.target as HTMLInputElement).value)" /><small>Nhập ít nhất 2 lựa chọn, phân cách bằng dấu phẩy.</small></div>
            </div>
            <div v-if="!form.specificationDefinitions.length" class="muted">Chưa có thông số. Ví dụ danh mục Lốp xe có thể thêm “Kích thước”, “Loại lốp”, “Tải trọng”.</div>
          </div>
        </template>
      </form>
      <template #footer><button v-if="editing && (activeTab !== 'serviceCategories' || isAdmin)" class="btn btn-secondary danger-button" :disabled="saving" @click="remove"><Trash2 :size="15" /> Xóa</button><button class="btn btn-secondary" @click="modalOpen = false">Hủy</button><button class="btn btn-primary" form="catalog-form" :disabled="saving">Lưu danh mục</button></template>
    </AppModal>
  </div>
</template>

<style scoped>
.catalog-tabs { display: flex; gap: 8px; overflow-x: auto; padding-bottom: 2px; }
.catalog-tabs button { display: inline-flex; min-width: max-content; align-items: center; gap: 8px; padding: 11px 15px; border: 1px solid var(--line); border-radius: 11px; color: var(--muted); background: white; font-weight: 800; }
.catalog-tabs button.active { border-color: var(--navy-900); color: white; background: var(--navy-900); }
.catalog-tabs span { display: grid; min-width: 22px; height: 22px; place-items: center; border-radius: 99px; color: var(--navy-900); background: var(--amber-soft); font-size: 10px; }
.spec-editor { display: grid; gap: 9px; }.spec-head { display: flex; align-items: center; justify-content: space-between; }.spec-head label,.spec-head small { display: block; }.spec-head small { margin-top: 3px; color: var(--muted); }.spec-item { display: grid; gap: 8px; padding: 10px; border: 1px solid var(--line); border-radius: 10px; background: #f9fbfc; }.spec-row { display: grid; grid-template-columns: 120px minmax(160px, 1fr) 150px 100px 90px 38px; align-items: center; gap: 8px; }.required-check { display: flex; align-items: center; gap: 6px; font-size: 11px; }.selection-options { display: grid; grid-template-columns: 120px 1fr; align-items: center; gap: 8px; }.selection-options small { grid-column: 2; color: var(--muted); }
@media (max-width: 720px) { .spec-row { grid-template-columns: 1fr; } }
</style>
