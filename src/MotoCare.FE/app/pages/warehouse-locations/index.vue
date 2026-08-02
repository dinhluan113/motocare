<script setup lang="ts">
import { Edit3, PackageOpen, Plus, SlidersHorizontal, Trash2, Warehouse } from '@lucide/vue'
import type { PagedResult, Part, WarehouseLocation } from '~/types/api'
import { entityDetailRoute } from '~/utils/entityRoute'
import { formatNumber } from '~/utils/format'

const api = useApi()
const toast = useToast()
const locations = ref<WarehouseLocation[]>([])
const parts = ref<Part[]>([])
const selectedLocationId = ref('')
const loading = ref(true)
const loadingParts = ref(false)
const saving = ref(false)
const locationModal = ref(false)
const adjustmentModal = ref(false)
const editingLocation = ref<WarehouseLocation>()
const adjustmentPart = ref<Part>()
const locationForm = reactive({ rack: 1, level: 1, bin: 1, name: '', description: '', isActive: true })
const adjustmentForm = reactive({ countedQuantity: 0, notes: '' })

const activeLocations = computed(() => locations.value.filter(x => !x.isDeleted))
const selectedLocation = computed(() => locations.value.find(x => x.id === selectedLocationId.value))
const selectedParts = computed(() => [...parts.value]
  .filter(x => !x.isDeleted)
  .sort((a, b) => a.name.localeCompare(b.name, 'vi')))
const warehouseRacks = computed(() => {
  const racks = new Map<number, WarehouseLocation[]>()
  for (const location of activeLocations.value) {
    const items = racks.get(location.rack) || []
    items.push(location)
    racks.set(location.rack, items)
  }
  return [...racks.entries()].sort(([a], [b]) => a - b).map(([rack, rackLocations]) => ({
    rack,
    levels: [...new Set(rackLocations.map(x => x.level))].sort((a, b) => b - a).map(level => ({
      level,
      bins: rackLocations.filter(x => x.level === level).sort((a, b) => a.bin - b.bin)
    }))
  }))
})

const quantityAtSelectedLocation = (part?: Part) => {
  if (!part) return 0
  return part.warehouseStocks?.find(x => x.warehouseLocationId === selectedLocationId.value)?.quantityOnHand
    ?? (part.warehouseLocationId === selectedLocationId.value && !part.warehouseStocks?.length ? part.quantityOnHand : 0)
}
const currentAdjustmentQuantity = computed(() => quantityAtSelectedLocation(adjustmentPart.value))
const adjustmentDifference = computed(() => Number(adjustmentForm.countedQuantity || 0) - currentAdjustmentQuantity.value)

const loadLocations = async () => {
  loading.value = true
  try {
    const result = await api.request<PagedResult<WarehouseLocation>>('/warehouse-locations?pageSize=500&includeDeleted=true')
    locations.value = result.items
    if (selectedLocationId.value && !locations.value.some(x => x.id === selectedLocationId.value && !x.isDeleted)) {
      selectedLocationId.value = ''
      parts.value = []
    }
  } finally {
    loading.value = false
  }
}

const selectLocation = async (location: WarehouseLocation) => {
  selectedLocationId.value = location.id
  parts.value = []
  loadingParts.value = true
  try {
    const result = await api.request<PagedResult<Part>>('/parts', {
      query: { warehouseLocationId: location.id, pageSize: 500 }
    })
    if (selectedLocationId.value === location.id) parts.value = result.items
  } finally {
    if (selectedLocationId.value === location.id) loadingParts.value = false
  }
}

const openLocationForm = (location?: WarehouseLocation) => {
  editingLocation.value = location
  Object.assign(locationForm, {
    rack: location?.rack || 1,
    level: location?.level || 1,
    bin: location?.bin || 1,
    name: location?.name || '',
    description: location?.description || '',
    isActive: location?.isActive ?? true
  })
  locationModal.value = true
}

const saveLocation = async () => {
  saving.value = true
  try {
    const location = editingLocation.value
    const payload = { ...locationForm }
    const saved = await api.request<WarehouseLocation>(`/warehouse-locations${location ? `/${location.id}` : ''}`, {
      method: location ? 'PUT' : 'POST',
      body: payload
    })
    toast.success(location ? 'Đã cập nhật vị trí kho' : 'Đã thêm vị trí kho', saved.code)
    locationModal.value = false
    await loadLocations()
    if (location?.id === selectedLocationId.value) await selectLocation(saved)
  } finally {
    saving.value = false
  }
}

const removeLocation = async () => {
  const location = editingLocation.value
  if (!location || !confirm(`Xóa vị trí ${location.code}?`)) return
  saving.value = true
  try {
    await api.request(`/warehouse-locations/${location.id}`, { method: 'DELETE' })
    toast.success('Đã xóa vị trí kho', location.code)
    locationModal.value = false
    await loadLocations()
  } finally {
    saving.value = false
  }
}

const openAdjustment = (part: Part) => {
  adjustmentPart.value = part
  adjustmentForm.countedQuantity = quantityAtSelectedLocation(part)
  adjustmentForm.notes = ''
  adjustmentModal.value = true
}

const saveAdjustment = async () => {
  const part = adjustmentPart.value
  const location = selectedLocation.value
  const desired = Number(adjustmentForm.countedQuantity)
  if (!part || !location || !Number.isFinite(desired) || desired < 0) return
  const difference = desired - quantityAtSelectedLocation(part)
  if (difference === 0) {
    toast.info('Số lượng không thay đổi', `${part.name} tại ${location.code}`)
    adjustmentModal.value = false
    return
  }
  saving.value = true
  try {
    await api.request('/inventory/movements', {
      method: 'POST',
      body: {
        partId: part.id,
        type: difference > 0 ? 'AdjustmentIncrease' : 'AdjustmentDecrease',
        quantity: Math.abs(difference),
        unitCost: part.stockPrice || part.importPrice || 0,
        referenceType: 'WarehouseStocktake',
        referenceId: location.id,
        notes: adjustmentForm.notes,
        warehouseLocationId: location.id
      }
    })
    toast.success('Đã điều chỉnh tồn kho', `${part.name}: ${formatNumber(desired)} ${part.unit} tại ${location.code}`)
    adjustmentModal.value = false
    await selectLocation(location)
  } finally {
    saving.value = false
  }
}

onMounted(loadLocations)
</script>

<template>
  <div class="page">
    <div class="page-header">
      <div><h1 class="page-title">Quản lý kho</h1><p class="page-subtitle">Quản lý sơ đồ kệ, xem và kiểm kê số lượng phụ tùng tại từng ngăn.</p></div>
      <button class="btn btn-accent" @click="openLocationForm()"><Plus :size="17" /> Thêm vị trí</button>
    </div>

    <section class="card warehouse-layout">
      <div class="warehouse-layout-head">
        <div><strong>Sơ đồ kho trực quan</strong><span>Chọn một ngăn để xem phụ tùng và điều chỉnh tồn kho tại đúng vị trí đó.</span></div>
        <span class="layout-hint">Kệ → Tầng → Ngăn</span>
      </div>
      <div v-if="loading" class="loading-state">Đang tải sơ đồ kho...</div>
      <div v-else-if="warehouseRacks.length" class="rack-stage">
        <article v-for="rack in warehouseRacks" :key="rack.rack" class="rack-view">
          <header><Warehouse :size="18" /> Kệ {{ rack.rack }}</header>
          <div class="rack-frame">
            <div v-for="level in rack.levels" :key="level.level" class="rack-level">
              <span class="level-label">T{{ level.level }}</span>
              <button
                v-for="location in level.bins"
                :key="location.id"
                type="button"
                class="warehouse-bin"
                :class="{ inactive: !location.isActive, selected: selectedLocationId === location.id }"
                :title="location.name"
                @click="selectLocation(location)"
              ><span>N{{ location.bin }}</span><strong>{{ location.code }}</strong></button>
            </div>
          </div>
        </article>
      </div>
      <AppEmpty v-else title="Chưa có vị trí kho" message="Tạo các ngăn theo cấu trúc Kệ – Tầng – Ngăn để bắt đầu sắp xếp phụ tùng." />
    </section>

    <section v-if="selectedLocation" class="card location-panel">
      <header class="location-head">
        <div><span>Vị trí đang xem</span><AppEntityLink block :to="entityDetailRoute('WarehouseLocation', selectedLocation.id)"><strong class="mono">{{ selectedLocation.code }}</strong><small>{{ selectedLocation.name }}</small></AppEntityLink></div>
        <button class="btn btn-secondary btn-sm" @click="openLocationForm(selectedLocation)"><Edit3 :size="14" /> Sửa vị trí</button>
      </header>
      <div v-if="loadingParts" class="loading-state">Đang tải phụ tùng trong ngăn...</div>
      <div v-else-if="selectedParts.length" class="table-wrap">
        <table class="data-table">
          <thead><tr><th>Mã phụ tùng</th><th>Tên phụ tùng</th><th>Đơn vị</th><th class="text-right">Tồn tại ngăn</th><th class="text-right">Tổng tồn</th><th class="text-right">Thao tác</th></tr></thead>
          <tbody>
            <tr v-for="part in selectedParts" :key="part.id">
              <td class="mono"><NuxtLink :to="`/inventory/${part.id}`">{{ part.code }}</NuxtLink></td>
              <td class="cell-main"><NuxtLink :to="`/inventory/${part.id}`">{{ part.name }}</NuxtLink></td>
              <td>{{ part.unit }}</td>
              <td class="text-right"><strong>{{ formatNumber(quantityAtSelectedLocation(part)) }}</strong></td>
              <td class="text-right">{{ formatNumber(part.quantityOnHand) }}</td>
              <td class="text-right"><button class="btn btn-secondary btn-sm" :disabled="!selectedLocation?.isActive" :title="selectedLocation?.isActive ? 'Kiểm kê số lượng tại ngăn này' : 'Vị trí đã tạm khóa, không thể điều chỉnh tồn'" @click="openAdjustment(part)"><SlidersHorizontal :size="14" /> Điều chỉnh</button></td>
            </tr>
          </tbody>
        </table>
      </div>
      <AppEmpty v-else title="Ngăn đang trống" message="Chưa có phụ tùng nào được gán vào vị trí này." />
    </section>
    <div v-else-if="warehouseRacks.length" class="select-bin-hint"><PackageOpen :size="18" /> Chọn một ngăn trên sơ đồ để xem và kiểm kê phụ tùng.</div>

    <section class="card">
      <div class="section-head"><div><h2>Danh sách vị trí</h2><p>{{ activeLocations.length }} vị trí trong kho</p></div></div>
      <div class="table-wrap">
        <table class="data-table">
          <thead><tr><th>Mã vị trí</th><th>Tên hiển thị</th><th>Kệ</th><th>Tầng</th><th>Ngăn</th><th>Trạng thái</th><th class="text-right">Thao tác</th></tr></thead>
          <tbody>
            <tr v-for="location in activeLocations" :key="location.id">
              <td><AppEntityLink class="mono cell-main" :to="entityDetailRoute('WarehouseLocation', location.id)">{{ location.code }}</AppEntityLink></td>
              <td><AppEntityLink :to="entityDetailRoute('WarehouseLocation', location.id)">{{ location.name }}</AppEntityLink></td><td>Kệ {{ location.rack }}</td><td>Tầng {{ location.level }}</td><td>Ngăn {{ location.bin }}</td>
              <td><AppBadge :tone="location.isActive ? 'success' : 'neutral'">{{ location.isActive ? 'Đang dùng' : 'Tạm khóa' }}</AppBadge></td>
              <td class="text-right"><button class="btn btn-secondary btn-sm" @click="openLocationForm(location)">Chỉnh sửa</button></td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <AppModal :open="locationModal" :title="editingLocation ? 'Cập nhật vị trí kho' : 'Thêm vị trí kho'" @close="locationModal = false">
      <form id="location-form" class="form-grid" @submit.prevent="saveLocation">
        <div class="field"><label>Kệ *</label><AppNumberInput v-model="locationForm.rack" class="input" min="1" required /></div>
        <div class="field"><label>Tầng *</label><AppNumberInput v-model="locationForm.level" class="input" min="1" required /></div>
        <div class="field"><label>Ngăn / ô *</label><AppNumberInput v-model="locationForm.bin" class="input" min="1" required /></div>
        <div class="field"><label>Mã tự động</label><input class="input mono" :value="`K${locationForm.rack}-T${locationForm.level}-N${locationForm.bin}`" disabled /></div>
        <div class="field span-2"><label>Tên hiển thị</label><input v-model.trim="locationForm.name" class="input" :placeholder="`Kệ ${locationForm.rack} · Tầng ${locationForm.level} · Ngăn ${locationForm.bin}`" /></div>
        <div class="field span-2"><label>Ghi chú</label><textarea v-model.trim="locationForm.description" class="textarea" placeholder="Ví dụ: Khu phụ tùng tiêu hao, gần cửa nhập hàng..." /></div>
        <label class="required-check span-2"><input v-model="locationForm.isActive" type="checkbox" /> Vị trí đang được sử dụng</label>
      </form>
      <template #footer><button v-if="editingLocation" class="btn btn-secondary danger-button" :disabled="saving" @click="removeLocation"><Trash2 :size="15" /> Xóa</button><button class="btn btn-secondary" @click="locationModal = false">Hủy</button><button class="btn btn-primary" form="location-form" :disabled="saving">Lưu vị trí</button></template>
    </AppModal>

    <AppModal :open="adjustmentModal" title="Kiểm kê tồn theo ngăn" @close="adjustmentModal = false">
      <form id="adjustment-form" class="form-grid" @submit.prevent="saveAdjustment">
        <div class="adjustment-summary span-2"><div><span>Phụ tùng</span><AppEntityLink block :to="entityDetailRoute('Part', adjustmentPart?.id)"><strong>{{ adjustmentPart?.name }}</strong><small class="mono">{{ adjustmentPart?.code }}</small></AppEntityLink></div><div><span>Vị trí</span><AppEntityLink block :to="entityDetailRoute('WarehouseLocation', selectedLocation?.id)"><strong class="mono">{{ selectedLocation?.code }}</strong><small>{{ selectedLocation?.name }}</small></AppEntityLink></div></div>
        <div class="field"><label>Số lượng hiện tại</label><input class="input" :value="formatNumber(currentAdjustmentQuantity)" disabled /></div>
        <div class="field"><label>Số lượng thực tế *</label><AppNumberInput v-model="adjustmentForm.countedQuantity" class="input" min="0" required /></div>
        <div class="difference span-2" :class="{ increase: adjustmentDifference > 0, decrease: adjustmentDifference < 0 }">Chênh lệch: <strong>{{ adjustmentDifference > 0 ? '+' : '' }}{{ formatNumber(adjustmentDifference) }} {{ adjustmentPart?.unit }}</strong></div>
        <div class="field span-2"><label>Lý do điều chỉnh *</label><textarea v-model.trim="adjustmentForm.notes" class="textarea" required placeholder="Ví dụ: Kiểm kê thực tế ngày..., phát hiện thừa/thiếu..." /></div>
      </form>
      <template #footer><button class="btn btn-secondary" @click="adjustmentModal = false">Hủy</button><button class="btn btn-primary" form="adjustment-form" :disabled="saving">Xác nhận điều chỉnh</button></template>
    </AppModal>
  </div>
</template>

<style scoped>
.warehouse-layout { padding: 18px; background: linear-gradient(145deg, #f7fafc, #edf3f7); }
.warehouse-layout-head,.location-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; margin-bottom: 20px; }
.warehouse-layout-head strong,.warehouse-layout-head span,.location-head span,.location-head strong,.location-head small { display: block; }
.warehouse-layout-head span,.location-head span,.location-head small { margin-top: 3px; color: var(--muted); font-size: 11px; }
.layout-hint { padding: 7px 10px; border-radius: 99px; color: var(--navy-800) !important; background: white; font-weight: 800; }
.rack-stage { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 24px; perspective: 1000px; }
.rack-view { filter: drop-shadow(8px 10px 10px rgb(20 40 60 / 12%)); transform: rotateY(-2deg); transform-style: preserve-3d; }
.rack-view header { display: flex; align-items: center; gap: 8px; padding: 9px 13px; border-radius: 8px 8px 0 0; color: white; background: var(--navy-900); font-weight: 800; }
.rack-frame { display: grid; gap: 7px; padding: 12px; border: 5px solid #547086; border-top: 0; background: #d8e2e9; box-shadow: inset -7px -5px 0 rgb(30 57 77 / 12%); }
.rack-level { position: relative; display: grid; grid-template-columns: repeat(auto-fit, minmax(76px, 1fr)); gap: 7px; padding: 4px 4px 9px 31px; border-bottom: 6px solid #50697c; }
.level-label { position: absolute; top: 50%; left: 2px; color: #365267; font-size: 10px; font-weight: 900; transform: translateY(-50%); }
.warehouse-bin { display: grid; min-height: 70px; place-content: center; border: 1px solid #b87920; border-radius: 4px; color: #6d4408; background: linear-gradient(145deg, #ffe6a8, #e6b44d); box-shadow: inset -5px -5px 0 rgb(118 73 8 / 12%), 3px 4px 0 rgb(50 72 88 / 12%); text-align: center; }
.warehouse-bin:hover { outline: 3px solid rgb(17 110 163 / 18%); transform: translateY(-2px); }
.warehouse-bin span { font-size: 11px; }.warehouse-bin strong { margin-top: 2px; font-size: 10px; }
.warehouse-bin.inactive { filter: grayscale(1); opacity: .55; }.warehouse-bin.selected { border-color: #087e68; color: #075e50; background: linear-gradient(145deg, #bff2df, #65c9aa); outline: 3px solid rgb(8 126 104 / 22%); }
.location-panel { overflow: hidden; }.location-head { margin: 0; padding: 14px 16px; border-bottom: 1px solid var(--line); }.location-head strong { margin: 2px 0; color: var(--navy-950); font-size: 17px; }
.select-bin-hint,.loading-state { display: flex; align-items: center; justify-content: center; gap: 8px; padding: 24px; border: 1px dashed #9eb2c2; border-radius: 10px; color: var(--muted); background: white; text-align: center; }
.location-link { border: 0; color: var(--navy-900); background: transparent; font-weight: 800; }
.location-link:hover,.data-table a:hover { color: #0c7b69; text-decoration: underline; }
.required-check { display: flex; align-items: center; gap: 6px; font-size: 11px; }
.danger-button { color: var(--red); }
.adjustment-summary { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; padding: 13px; border-radius: 10px; background: #f4f7f9; }.adjustment-summary span,.adjustment-summary strong,.adjustment-summary small { display: block; }.adjustment-summary span,.adjustment-summary small { color: var(--muted); font-size: 10px; }.adjustment-summary strong { margin: 3px 0; }
.difference { padding: 10px 12px; border-radius: 8px; color: var(--muted); background: #f4f6f8; }.difference.increase { color: #08705f; background: #e6f7f1; }.difference.decrease { color: #b13939; background: #fff0f0; }
@media (max-width: 720px) { .warehouse-layout-head,.location-head { flex-direction: column; }.rack-stage { grid-template-columns: 1fr; }.rack-view { transform: none; }.adjustment-summary { grid-template-columns: 1fr; } }
</style>
