<script setup lang="ts">
import { ArrowLeft, CalendarDays, Info, Link2, Pencil, Plus, Trash2 } from '@lucide/vue'
import type {
  PagedResult,
  PartBrand,
  PartCategory,
  ServiceCategory,
  VehicleBrand,
  VehicleModel
} from '~/types/api'
import { formatCurrency, formatDate, formatNumber } from '~/utils/format'

type CatalogType =
  | 'vehicle-brands'
  | 'vehicle-models'
  | 'part-brands'
  | 'part-categories'
  | 'service-categories'
type SpecificationDataType = 'Text' | 'Number' | 'Boolean' | 'Selection'
interface SpecificationDefinition {
  code: string
  name: string
  dataType: SpecificationDataType
  options: string[]
  unit: string
  isRequired: boolean
}
type PartBrandDetail = PartBrand & { contactInfo?: string }
type CatalogEntity = VehicleBrand | VehicleModel | PartBrandDetail | PartCategory | ServiceCategory

const catalogTypes: Record<CatalogType, { label: string, description: string }> = {
  'vehicle-brands': { label: 'Hãng xe', description: 'Thông tin hãng sản xuất phương tiện.' },
  'vehicle-models': { label: 'Dòng xe', description: 'Thông tin dòng xe và hãng sản xuất liên quan.' },
  'part-brands': { label: 'Hãng phụ tùng', description: 'Thông tin thương hiệu phụ tùng.' },
  'part-categories': { label: 'Danh mục phụ tùng', description: 'Thông tin phân loại và bộ thông số kỹ thuật.' },
  'service-categories': { label: 'Dịch vụ', description: 'Thông tin dịch vụ và giá mặc định.' }
}

const route = useRoute()
const api = useApi()
const auth = useAuth()
const toast = useToast()

const isEmployee = computed(() => auth.hasAnyRole('Employee'))
const isAdmin = computed(() => auth.hasAnyRole('Admin', 'Administrator'))
const entity = ref<CatalogEntity>()
const relatedVehicleBrand = ref<VehicleBrand>()
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const deleting = ref(false)
const vehicleBrands = ref<VehicleBrand[]>([])
const form = reactive({
  code: '',
  name: '',
  country: '',
  brandId: '',
  vehicleType: '',
  engineCapacityCc: 0,
  contactInfo: '',
  description: '',
  defaultPrice: 0,
  specificationDefinitions: [] as SpecificationDefinition[],
  isActive: true
})
const specificationTypeOptions = [
  { value: 'Text', label: 'Ký tự' },
  { value: 'Number', label: 'Số' },
  { value: 'Boolean', label: 'Có/Không' },
  { value: 'Selection', label: 'Danh sách lựa chọn' }
] as const

const catalogType = computed<CatalogType | undefined>(() => {
  const value = String(route.params.type || '')
  return value in catalogTypes ? value as CatalogType : undefined
})
const catalogId = computed(() => String(route.params.id || ''))
const config = computed(() => catalogType.value ? catalogTypes[catalogType.value] : undefined)
const catalogListRoute = computed(() =>
  catalogType.value ? `/catalogs/${catalogType.value}` : '/catalogs/vehicle-brands')
const canEdit = computed(() =>
  !isEmployee.value
  && Boolean(entity.value)
  && (catalogType.value !== 'service-categories' || isAdmin.value))
const vehicleBrandItem = computed(() => catalogType.value === 'vehicle-brands' ? entity.value as VehicleBrand | undefined : undefined)
const vehicleModelItem = computed(() => catalogType.value === 'vehicle-models' ? entity.value as VehicleModel | undefined : undefined)
const partBrandItem = computed(() => catalogType.value === 'part-brands' ? entity.value as PartBrandDetail | undefined : undefined)
const partCategoryItem = computed(() => catalogType.value === 'part-categories' ? entity.value as PartCategory | undefined : undefined)
const serviceCategoryItem = computed(() => catalogType.value === 'service-categories' ? entity.value as ServiceCategory | undefined : undefined)
const codePlaceholder = computed(() => ({
  'vehicle-brands': 'HX-000001',
  'vehicle-models': 'DX-000001',
  'part-brands': 'HPT-000001',
  'part-categories': 'DMPT-000001',
  'service-categories': 'DV-000001'
}[catalogType.value || 'vehicle-brands']))
const vehicleBrandOptions = computed(() => vehicleBrands.value.map(item => ({ code: item.id, name: item.name })))

const specificationTypeLabel = (value: string) => ({
  Text: 'Ký tự',
  Number: 'Số',
  Boolean: 'Có / Không',
  Selection: 'Danh sách lựa chọn'
}[value] || value)

const loadVehicleBrands = async () => {
  const brands = await api.request<PagedResult<VehicleBrand>>('/vehicle-brands?pageSize=200')
  vehicleBrands.value = brands.items
}

const load = async () => {
  loading.value = true
  entity.value = undefined
  relatedVehicleBrand.value = undefined

  const type = catalogType.value
  const id = catalogId.value
  if (!type || !id) {
    loading.value = false
    return
  }

  try {
    const current = await api.request<CatalogEntity>(`/${type}/${id}`, {
      query: { includeDeleted: true }
    })
    entity.value = current

    if (type === 'vehicle-models') {
      const brandId = (current as VehicleModel).brandId
      if (brandId) {
        try {
          relatedVehicleBrand.value = await api.request<VehicleBrand>(`/vehicle-brands/${brandId}`, {
            query: { includeDeleted: true }
          })
        } catch {
          relatedVehicleBrand.value = undefined
        }
      }
    }
  } catch {
    entity.value = undefined
  } finally {
    loading.value = false
  }
}

const openEdit = async () => {
  if (!entity.value || !catalogType.value) return
  if (catalogType.value === 'vehicle-models' && !vehicleBrands.value.length) {
    await loadVehicleBrands()
  }
  Object.assign(form, {
    code: entity.value.code || '',
    name: entity.value.name || '',
    country: 'country' in entity.value ? (entity.value.country || '') : '',
    brandId: 'brandId' in entity.value ? (entity.value.brandId || vehicleBrands.value[0]?.id || '') : '',
    vehicleType: 'vehicleType' in entity.value ? (entity.value.vehicleType || '') : '',
    engineCapacityCc: 'engineCapacityCc' in entity.value ? (entity.value.engineCapacityCc || 0) : 0,
    contactInfo: 'contactInfo' in entity.value ? (entity.value.contactInfo || '') : '',
    description: 'description' in entity.value ? (entity.value.description || '') : '',
    defaultPrice: 'defaultPrice' in entity.value ? (entity.value.defaultPrice || 0) : 0,
    specificationDefinitions: ('specificationDefinitions' in entity.value ? (entity.value.specificationDefinitions || []) : []).map((x: any) => ({
      code: x.code,
      name: x.name,
      dataType: x.dataType || 'Text',
      options: x.options || [],
      unit: x.unit || '',
      isRequired: x.isRequired
    })),
    isActive: entity.value.isActive ?? true
  })
  modalOpen.value = true
}

const save = async () => {
  const type = catalogType.value
  const current = entity.value
  if (!type || !current) return

  saving.value = true
  try {
    const payload = type === 'service-categories'
      ? { code: form.code, name: form.name, defaultPrice: form.defaultPrice, description: form.description, isActive: form.isActive }
      : type === 'vehicle-models'
        ? { brandId: form.brandId, code: form.code, name: form.name, vehicleType: form.vehicleType, engineCapacityCc: form.engineCapacityCc || null, isActive: form.isActive }
        : type === 'part-brands'
          ? { code: form.code, name: form.name, country: form.country, contactInfo: form.contactInfo, isActive: form.isActive }
          : type === 'part-categories'
            ? { code: form.code, name: form.name, description: form.description, specificationDefinitions: form.specificationDefinitions, isActive: form.isActive }
            : { code: form.code, name: form.name, country: form.country, isActive: form.isActive }
    await api.request(`/${type}/${current.id}`, { method: 'PUT', body: payload })
    toast.success('Đã cập nhật danh mục', form.name)
    modalOpen.value = false
    await load()
  } finally {
    saving.value = false
  }
}

const remove = async () => {
  const type = catalogType.value
  const current = entity.value
  if (!type || !current || !confirm(`Xóa ${config.value?.label.toLowerCase()} ${current.name}?`)) return

  deleting.value = true
  try {
    await api.request(`/${type}/${current.id}`, { method: 'DELETE' })
    toast.success('Đã xóa danh mục', current.name)
    await navigateTo(catalogListRoute.value)
  } finally {
    deleting.value = false
  }
}

const addSpecification = () => {
  const used = new Set(form.specificationDefinitions.map(x => x.code.toUpperCase()))
  let number = 1
  let code = ''
  do {
    code = `TSKT-${String(number++).padStart(3, '0')}`
  } while (used.has(code))
  form.specificationDefinitions.push({ code, name: '', dataType: 'Text', options: [], unit: '', isRequired: false })
}

const removeSpecification = (index: number) => form.specificationDefinitions.splice(index, 1)
const setSpecificationOptions = (index: number, value: string) => {
  form.specificationDefinitions[index]!.options = value.split(',').map(x => x.trim()).filter(Boolean)
}

onMounted(load)
watch([catalogType, catalogId], () => load())
</script>

<template>
  <div class="page">
    <NuxtLink class="back-link" :to="isEmployee ? '/inventory' : catalogListRoute"><ArrowLeft :size="16" /> {{ isEmployee ? 'Kho phụ tùng' : 'Danh mục hệ thống' }}</NuxtLink>

    <template v-if="entity && config">
      <div class="page-header">
        <div>
          <div class="breadcrumb">Danh mục hệ thống <span>›</span> {{ config.label }}</div>
          <div class="title-line">
            <h1 class="page-title">{{ entity.name }}</h1>
            <AppBadge :tone="entity.isDeleted || !entity.isActive ? 'neutral' : 'success'">
              {{ entity.isDeleted ? 'Đã xóa' : entity.isActive ? 'Đang sử dụng' : 'Tạm khóa' }}
            </AppBadge>
          </div>
          <p class="page-subtitle">{{ config.description }}</p>
        </div>
        <div v-if="canEdit" class="page-actions">
          <button class="btn btn-secondary" @click="openEdit"><Pencil :size="15" /> Chỉnh sửa</button>
          <button class="btn btn-secondary danger-button" :disabled="deleting" @click="remove"><Trash2 :size="15" /> Xóa</button>
        </div>
      </div>

      <section class="card">
        <header class="card-header"><h2 class="card-title">Thông tin {{ config.label.toLowerCase() }}</h2><Info :size="19" /></header>
        <div class="card-body detail-grid">
          <div><span>Mã</span><strong class="mono">{{ entity.code }}</strong></div>
          <div><span>Trạng thái</span><strong>{{ entity.isDeleted ? 'Đã xóa' : entity.isActive ? 'Đang hoạt động' : 'Ngừng hoạt động' }}</strong></div>

          <template v-if="vehicleBrandItem">
            <div class="span-2"><span>Quốc gia</span><strong>{{ vehicleBrandItem.country || '—' }}</strong></div>
          </template>

          <template v-else-if="vehicleModelItem">
            <div class="span-2">
              <span><Link2 :size="14" /> Hãng xe</span>
              <strong>
                <AppEntityLink :to="vehicleModelItem.brandId ? `/catalogs/vehicle-brands/${vehicleModelItem.brandId}` : undefined" icon>
                  {{ relatedVehicleBrand?.name || vehicleModelItem.brandId || '—' }}
                </AppEntityLink>
              </strong>
            </div>
            <div><span>Loại xe</span><strong>{{ vehicleModelItem.vehicleType || '—' }}</strong></div>
            <div><span>Phân khối</span><strong>{{ vehicleModelItem.engineCapacityCc ? `${formatNumber(vehicleModelItem.engineCapacityCc)} cc` : '—' }}</strong></div>
          </template>

          <template v-else-if="partBrandItem">
            <div><span>Quốc gia</span><strong>{{ partBrandItem.country || '—' }}</strong></div>
            <div><span>Thông tin liên hệ</span><strong>{{ partBrandItem.contactInfo || '—' }}</strong></div>
          </template>

          <template v-else-if="partCategoryItem">
            <div class="span-2"><span>Mô tả</span><strong>{{ partCategoryItem.description || '—' }}</strong></div>
          </template>

          <template v-else-if="serviceCategoryItem">
            <div><span>Giá mặc định</span><strong>{{ formatCurrency(serviceCategoryItem.defaultPrice) }}</strong></div>
            <div class="span-2"><span>Mô tả</span><strong>{{ serviceCategoryItem.description || '—' }}</strong></div>
          </template>

          <div><span><CalendarDays :size="14" /> Ngày tạo</span><strong>{{ formatDate(entity.createdAt, true) }}</strong></div>
          <div><span><CalendarDays :size="14" /> Cập nhật gần nhất</span><strong>{{ formatDate(entity.updatedAt, true) }}</strong></div>
        </div>
      </section>

      <section v-if="partCategoryItem" class="card">
        <header class="card-header">
          <div><h2 class="card-title">Thông số kỹ thuật</h2><span class="section-note">Bộ trường dùng khi khai báo phụ tùng thuộc danh mục này</span></div>
          <span class="muted">{{ formatNumber(partCategoryItem.specificationDefinitions?.length || 0) }} thông số</span>
        </header>
        <div v-if="partCategoryItem.specificationDefinitions?.length" class="table-wrap">
          <table class="data-table">
            <thead><tr><th>Mã</th><th>Tên thông số</th><th>Kiểu dữ liệu</th><th>Đơn vị</th><th>Lựa chọn</th><th>Bắt buộc</th></tr></thead>
            <tbody>
              <tr v-for="specification in partCategoryItem.specificationDefinitions" :key="specification.code">
                <td class="mono">{{ specification.code }}</td>
                <td class="cell-main">{{ specification.name }}</td>
                <td>{{ specificationTypeLabel(specification.dataType) }}</td>
                <td>{{ specification.unit || '—' }}</td>
                <td>{{ specification.options?.join(', ') || '—' }}</td>
                <td><AppBadge :tone="specification.isRequired ? 'warning' : 'neutral'">{{ specification.isRequired ? 'Bắt buộc' : 'Tùy chọn' }}</AppBadge></td>
              </tr>
            </tbody>
          </table>
        </div>
        <AppEmpty v-else title="Chưa có thông số" message="Danh mục này chưa cấu hình bộ thông số kỹ thuật." />
      </section>
    </template>

    <div v-else-if="loading" class="loading-skeleton" style="height: 420px" />
    <AppEmpty
      v-else-if="!catalogType"
      title="Đường dẫn danh mục không hợp lệ"
      message="Loại danh mục trong đường dẫn không được hệ thống hỗ trợ."
    />
    <AppEmpty v-else title="Không tìm thấy dữ liệu" message="Mục danh mục không tồn tại hoặc bạn không có quyền xem." />

    <AppModal
      :open="modalOpen"
      :title="`Cập nhật ${config?.label.toLowerCase()}`"
      :width="catalogType === 'part-categories' ? '820px' : undefined"
      @close="!saving && (modalOpen = false)"
    >
      <form id="catalog-detail-form" class="form-grid" @submit.prevent="save">
        <div class="field"><label>Mã <span class="muted">(tự động nếu để trống)</span></label><input v-model.trim="form.code" class="input" :placeholder="`Ví dụ: ${codePlaceholder}`" /></div>
        <div class="field"><label>Tên *</label><input v-model.trim="form.name" class="input" required /></div>

        <template v-if="catalogType === 'vehicle-models'">
          <div class="field"><label>Hãng xe *</label><AppSearchSelect v-model="form.brandId" :options="vehicleBrandOptions" placeholder="Chọn hãng xe" search-placeholder="Tìm hãng xe..." required :clearable="false" /></div>
          <div class="field"><label>Loại xe</label><input v-model.trim="form.vehicleType" class="input" placeholder="Xe số, tay ga, côn tay..." /></div>
          <div class="field"><label>Phân khối (cc)</label><AppNumberInput v-model="form.engineCapacityCc" class="input" min="0" /></div>
        </template>
        <template v-else-if="catalogType === 'service-categories'">
          <div class="field"><label>Giá mặc định</label><AppNumberInput v-model="form.defaultPrice" class="input" min="0" /></div>
          <div class="field span-2"><label>Mô tả</label><textarea v-model.trim="form.description" class="textarea" placeholder="Ví dụ: Rửa xe, thay nhớt, vệ sinh bugi, tiền công..." /></div>
        </template>
        <template v-else-if="catalogType !== 'part-categories'">
          <div class="field"><label>Quốc gia</label><AppCountrySelect v-model="form.country" /></div>
          <div v-if="catalogType === 'part-brands'" class="field span-2"><label>Thông tin liên hệ</label><input v-model.trim="form.contactInfo" class="input" /></div>
        </template>
        <template v-else>
          <div class="field span-2"><label>Mô tả</label><textarea v-model.trim="form.description" class="textarea" placeholder="Ví dụ: lọc gió, lốp xe, đèn xe..." /></div>
          <div class="field span-2 spec-editor">
            <div class="spec-head"><div><label>Thông số kỹ thuật</label><small>Mỗi danh mục có một bộ thông số riêng.</small></div><button type="button" class="btn btn-secondary btn-sm" @click="addSpecification"><Plus :size="14" /> Thêm thông số</button></div>
            <div v-for="(spec, index) in form.specificationDefinitions" :key="index" class="spec-item">
              <div class="spec-row">
                <input v-model.trim="spec.code" class="input" required placeholder="Mã (VD: SIZE)" />
                <input v-model.trim="spec.name" class="input" required placeholder="Tên (VD: Kích thước)" />
                <select v-model="spec.dataType" class="select" required><option v-for="type in specificationTypeOptions" :key="type.value" :value="type.value">{{ type.label }}</option></select>
                <input v-if="spec.dataType !== 'Boolean'" v-model.trim="spec.unit" class="input" placeholder="Đơn vị" />
                <span v-else class="muted">Không có đơn vị</span>
                <label class="required-check"><input v-model="spec.isRequired" type="checkbox" /> Bắt buộc</label>
                <button type="button" class="icon-btn" title="Xóa" @click="removeSpecification(index)"><Trash2 :size="15" /></button>
              </div>
              <div v-if="spec.dataType === 'Selection'" class="selection-options"><label>Các lựa chọn *</label><input class="input" required :value="spec.options.join(', ')" placeholder="Ví dụ: Trước, Sau" @input="setSpecificationOptions(index, ($event.target as HTMLInputElement).value)" /><small>Nhập ít nhất 2 lựa chọn, phân cách bằng dấu phẩy.</small></div>
            </div>
            <div v-if="!form.specificationDefinitions.length" class="muted">Chưa có thông số. Ví dụ danh mục Lốp xe có thể thêm “Kích thước”, “Loại lốp”, “Tải trọng”.</div>
          </div>
        </template>
      </form>
      <template #footer>
        <button class="btn btn-secondary" :disabled="saving" @click="modalOpen = false">Hủy</button>
        <button class="btn btn-primary" form="catalog-detail-form" :disabled="saving">Lưu thay đổi</button>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.breadcrumb { margin-bottom: 7px; color: var(--muted); font-size: 11px; font-weight: 700; }.breadcrumb span { padding: 0 5px; color: var(--amber); }
.title-line { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.page-actions { display: flex; align-items: center; gap: 8px; }
.card-header > svg { color: var(--muted); }
.detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }
.detail-grid span,.detail-grid strong { display: block; }.detail-grid span { display: flex; align-items: center; gap: 5px; color: var(--muted); font-size: 11px; }.detail-grid strong { margin-top: 4px; color: var(--navy-950); overflow-wrap: anywhere; }.span-2 { grid-column: span 2; }
.spec-editor { display: grid; gap: 9px; }
.spec-head { display: flex; align-items: center; justify-content: space-between; }
.spec-head label,.spec-head small { display: block; }
.spec-head small { margin-top: 3px; color: var(--muted); }
.spec-item { display: grid; gap: 8px; padding: 10px; border: 1px solid var(--line); border-radius: 10px; background: #f9fbfc; }
.spec-row { display: grid; grid-template-columns: 120px minmax(160px, 1fr) 150px 100px 90px 38px; align-items: center; gap: 8px; }
.required-check { display: flex; align-items: center; gap: 6px; font-size: 11px; }
.selection-options { display: grid; grid-template-columns: 120px 1fr; align-items: center; gap: 8px; }
.selection-options small { grid-column: 2; color: var(--muted); }
@media (max-width: 720px) { .spec-row { grid-template-columns: 1fr; } }
@media (max-width: 640px) { .detail-grid { grid-template-columns: 1fr; gap: 14px; }.span-2 { grid-column: auto; }.page-actions { width: 100%; justify-content: flex-start; } }
</style>
