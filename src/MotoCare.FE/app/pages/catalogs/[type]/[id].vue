<script setup lang="ts">
import { ArrowLeft, CalendarDays, Info, Link2 } from '@lucide/vue'
import type {
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
const isEmployee = computed(() => auth.hasAnyRole('Employee'))
const entity = ref<CatalogEntity>()
const relatedVehicleBrand = ref<VehicleBrand>()
const loading = ref(true)

const catalogType = computed<CatalogType | undefined>(() => {
  const value = String(route.params.type || '')
  return value in catalogTypes ? value as CatalogType : undefined
})
const catalogId = computed(() => String(route.params.id || ''))
const config = computed(() => catalogType.value ? catalogTypes[catalogType.value] : undefined)
const catalogListRoute = computed(() =>
  catalogType.value ? `/catalogs/${catalogType.value}` : '/catalogs/vehicle-brands')
const vehicleBrandItem = computed(() => catalogType.value === 'vehicle-brands' ? entity.value as VehicleBrand | undefined : undefined)
const vehicleModelItem = computed(() => catalogType.value === 'vehicle-models' ? entity.value as VehicleModel | undefined : undefined)
const partBrandItem = computed(() => catalogType.value === 'part-brands' ? entity.value as PartBrandDetail | undefined : undefined)
const partCategoryItem = computed(() => catalogType.value === 'part-categories' ? entity.value as PartCategory | undefined : undefined)
const serviceCategoryItem = computed(() => catalogType.value === 'service-categories' ? entity.value as ServiceCategory | undefined : undefined)

const specificationTypeLabel = (value: string) => ({
  Text: 'Ký tự',
  Number: 'Số',
  Boolean: 'Có / Không',
  Selection: 'Danh sách lựa chọn'
}[value] || value)

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
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.breadcrumb { margin-bottom: 7px; color: var(--muted); font-size: 11px; font-weight: 700; }.breadcrumb span { padding: 0 5px; color: var(--amber); }
.title-line { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.card-header > svg { color: var(--muted); }
.detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }
.detail-grid span,.detail-grid strong { display: block; }.detail-grid span { display: flex; align-items: center; gap: 5px; color: var(--muted); font-size: 11px; }.detail-grid strong { margin-top: 4px; color: var(--navy-950); overflow-wrap: anywhere; }.span-2 { grid-column: span 2; }
@media (max-width: 640px) { .detail-grid { grid-template-columns: 1fr; gap: 14px; }.span-2 { grid-column: auto; } }
</style>
