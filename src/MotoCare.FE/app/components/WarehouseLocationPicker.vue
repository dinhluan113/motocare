<script setup lang="ts">
import { Check, MapPin, Warehouse } from '@lucide/vue'
import type { WarehouseLocation } from '~/types/api'

const props = withDefaults(defineProps<{
  modelValue?: string[]
  defaultLocationId?: string
  locations: WarehouseLocation[]
  clearable?: boolean
  disabled?: boolean
}>(), {
  modelValue: () => [],
  defaultLocationId: '',
  clearable: true,
  disabled: false
})

const emit = defineEmits<{
  'update:modelValue': [value: string[]]
  'update:defaultLocationId': [value: string]
}>()
const open = ref(false)
const pendingValues = ref<string[]>([])
const pendingDefault = ref('')
const selectedLocations = computed(() => props.locations.filter(x => props.modelValue.includes(x.id)))
const defaultLocation = computed(() => props.locations.find(x => x.id === props.defaultLocationId))
const activeLocations = computed(() => props.locations.filter(x => !x.isDeleted && x.isActive))
const racks = computed(() => {
  const grouped = new Map<number, WarehouseLocation[]>()
  for (const location of activeLocations.value) {
    grouped.set(location.rack, [...(grouped.get(location.rack) || []), location])
  }
  return [...grouped.entries()].sort(([a], [b]) => a - b).map(([rack, locations]) => ({
    rack,
    levels: [...new Set(locations.map(x => x.level))].sort((a, b) => b - a).map(level => ({
      level,
      bins: locations.filter(x => x.level === level).sort((a, b) => a.bin - b.bin)
    }))
  }))
})

const show = () => {
  pendingValues.value = [...props.modelValue]
  pendingDefault.value = props.defaultLocationId || props.modelValue[0] || ''
  open.value = true
}
const confirm = () => {
  const defaultId = pendingValues.value.includes(pendingDefault.value)
    ? pendingDefault.value
    : pendingValues.value[0] || ''
  emit('update:modelValue', [...pendingValues.value])
  emit('update:defaultLocationId', defaultId)
  open.value = false
}
const clear = () => {
  pendingValues.value = []
  pendingDefault.value = ''
  emit('update:modelValue', [])
  emit('update:defaultLocationId', '')
  open.value = false
}
const toggle = (locationId: string) => {
  pendingValues.value = pendingValues.value.includes(locationId)
    ? pendingValues.value.filter(x => x !== locationId)
    : [...pendingValues.value, locationId]
  if (!pendingValues.value.includes(pendingDefault.value)) {
    pendingDefault.value = pendingValues.value[0] || ''
  }
}
</script>

<template>
  <button type="button" class="location-trigger" :disabled="disabled" @click="show">
    <MapPin :size="18" />
    <span><small>Các vị trí đang chọn</small><strong v-if="selectedLocations.length">{{ selectedLocations.length }} ngăn · mặc định <span class="mono">{{ defaultLocation?.code || selectedLocations[0]?.code }}</span></strong><strong v-else>Chưa chọn vị trí</strong><em v-if="selectedLocations.length">{{ selectedLocations.map(x => x.code).join(' · ') }}</em></span>
    <b>{{ selectedLocations.length ? 'Đổi vị trí' : 'Mở sơ đồ kho' }}</b>
  </button>

  <AppModal :open="open" title="Chọn vị trí trên sơ đồ kho" description="Chọn trực tiếp kệ, tầng và ngăn sẽ dùng làm vị trí mặc định của phụ tùng." width="1040px" @close="open = false">
    <div v-if="racks.length" class="picker-stage">
      <article v-for="rack in racks" :key="rack.rack" class="picker-rack">
        <header><Warehouse :size="18" /> Kệ {{ rack.rack }}</header>
        <div class="picker-frame">
          <div v-for="level in rack.levels" :key="level.level" class="picker-level">
            <span class="picker-level-label">T{{ level.level }}</span>
            <button v-for="location in level.bins" :key="location.id" type="button" class="picker-bin" :class="{ selected: pendingValues.includes(location.id) }" @click="toggle(location.id)">
              <Check v-if="pendingValues.includes(location.id)" :size="15" />
              <span>N{{ location.bin }}</span>
              <strong>{{ location.code }}</strong>
            </button>
          </div>
        </div>
      </article>
    </div>
    <AppEmpty v-else title="Chưa có vị trí khả dụng" message="Hãy khai báo vị trí kho đang hoạt động trong phần Danh mục." />
    <div v-if="pendingValues.length" class="default-location-list">
      <strong>Ngăn nhập hàng mặc định</strong><span>Hàng nhập mới sẽ được cộng vào ngăn này nếu phiếu nhập không chỉ định ngăn khác.</span>
      <label v-for="locationId in pendingValues" :key="locationId"><input v-model="pendingDefault" type="radio" :value="locationId" /><span class="mono">{{ locations.find(x => x.id === locationId)?.code }}</span><small>{{ locations.find(x => x.id === locationId)?.name }}</small></label>
    </div>
    <template #footer>
      <button v-if="clearable && modelValue.length" type="button" class="btn btn-secondary" @click="clear">Bỏ tất cả vị trí</button>
      <button type="button" class="btn btn-secondary" @click="open = false">Hủy</button>
      <button type="button" class="btn btn-primary" :disabled="!pendingValues.length" @click="confirm"><MapPin :size="15" /> Chọn {{ pendingValues.length }} vị trí</button>
    </template>
  </AppModal>
</template>

<style scoped>
.location-trigger { display: grid; width: 100%; min-height: 66px; grid-template-columns: 22px minmax(0, 1fr) auto; align-items: center; gap: 10px; padding: 11px 13px; border: 1px solid var(--line); border-radius: 10px; color: var(--navy-800); background: #f8fafb; text-align: left; }.location-trigger:hover { border-color: #86a9bf; background: #f1f7fa; }.location-trigger:disabled { cursor: not-allowed; opacity: .6; }.location-trigger span { min-width: 0; }.location-trigger small,.location-trigger strong,.location-trigger em { display: block; }.location-trigger small { color: var(--muted); font-size: 10px; }.location-trigger strong { margin-top: 2px; color: var(--navy-950); }.location-trigger em { overflow: hidden; margin-top: 2px; color: var(--muted); font-size: 10px; font-style: normal; text-overflow: ellipsis; white-space: nowrap; }.location-trigger b { color: var(--blue); font-size: 11px; white-space: nowrap; }
.picker-stage { display: grid; grid-template-columns: repeat(auto-fit, minmax(270px, 1fr)); gap: 25px; padding: 3px; perspective: 1000px; }.picker-rack { filter: drop-shadow(8px 10px 10px rgb(20 40 60 / 12%)); transform: rotateY(-2deg); transform-style: preserve-3d; }.picker-rack header { display: flex; align-items: center; gap: 8px; padding: 9px 13px; border-radius: 8px 8px 0 0; color: white; background: var(--navy-900); font-weight: 800; }.picker-frame { display: grid; gap: 7px; padding: 12px; border: 5px solid #547086; border-top: 0; background: #d8e2e9; box-shadow: inset -7px -5px 0 rgb(30 57 77 / 12%); }.picker-level { position: relative; display: grid; grid-template-columns: repeat(auto-fit, minmax(76px, 1fr)); gap: 7px; padding: 4px 4px 9px 31px; border-bottom: 6px solid #50697c; }.picker-level-label { position: absolute; top: 50%; left: 2px; color: #365267; font-size: 10px; font-weight: 900; transform: translateY(-50%); }.picker-bin { position: relative; display: grid; min-height: 70px; place-content: center; border: 1px solid #b87920; border-radius: 4px; color: #6d4408; background: linear-gradient(145deg, #ffe6a8, #e6b44d); box-shadow: inset -5px -5px 0 rgb(118 73 8 / 12%), 3px 4px 0 rgb(50 72 88 / 12%); text-align: center; }.picker-bin:hover { outline: 3px solid rgb(17 110 163 / 20%); transform: translateY(-2px); }.picker-bin.selected { border-color: #087e68; color: #075e50; background: linear-gradient(145deg, #bff2df, #65c9aa); outline: 3px solid rgb(8 126 104 / 22%); }.picker-bin svg { position: absolute; top: 5px; right: 5px; }.picker-bin span { font-size: 11px; }.picker-bin strong { margin-top: 2px; font-size: 10px; }
.default-location-list { display: grid; gap: 7px; margin-top: 20px; padding: 14px; border: 1px solid var(--line); border-radius: 11px; background: #f8fafb; }.default-location-list > strong,.default-location-list > span { display: block; }.default-location-list > span { color: var(--muted); font-size: 10px; }.default-location-list label { display: grid; grid-template-columns: 18px 100px 1fr; align-items: center; gap: 8px; padding: 8px 9px; border-radius: 8px; background: white; cursor: pointer; }.default-location-list label span { color: var(--navy-900); font-weight: 800; }.default-location-list label small { color: var(--muted); }
@media (max-width: 560px) { .location-trigger { grid-template-columns: 22px minmax(0, 1fr); }.location-trigger b { grid-column: 2; }.picker-stage { grid-template-columns: 1fr; }.picker-rack { transform: none; } }
</style>
