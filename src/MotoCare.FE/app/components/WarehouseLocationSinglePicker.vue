<script setup lang="ts">
import { Check, MapPin, Warehouse } from '@lucide/vue'
import type { WarehouseLocation } from '~/types/api'

const props = withDefaults(defineProps<{
  modelValue?: string
  locations: WarehouseLocation[]
  title?: string
  description?: string
  placeholder?: string
  actionLabel?: string
  locationDetails?: Record<string, string>
  disabled?: boolean
  showLeadingIcon?: boolean
  actionOutside?: boolean
}>(), {
  modelValue: '',
  title: 'Chọn vị trí trên sơ đồ kho',
  description: 'Chọn trực tiếp kệ, tầng và ngăn trên sơ đồ.',
  placeholder: 'Chưa chọn vị trí',
  actionLabel: 'Mở sơ đồ kho',
  locationDetails: () => ({}),
  disabled: false,
  showLeadingIcon: true,
  actionOutside: false
})

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
const open = ref(false)
const pendingValue = ref('')
const selectedLocation = computed(() => props.locations.find(x => x.id === props.modelValue))
const activeLocations = computed(() => props.locations.filter(x => !x.isDeleted))
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
  pendingValue.value = props.modelValue
  open.value = true
}
const confirm = () => {
  emit('update:modelValue', pendingValue.value)
  open.value = false
}
</script>

<template>
  <div class="location-picker">
    <div v-if="actionOutside" class="location-actions">
      <button type="button" class="link-button" :disabled="disabled" @click="show">
        {{ selectedLocation ? 'Đổi vị trí' : actionLabel }}
      </button>
    </div>
    <button type="button" class="location-trigger" :class="{ compact: actionOutside, 'no-icon': !showLeadingIcon }" :disabled="disabled" @click="show">
      <MapPin v-if="showLeadingIcon" :size="18" />
      <i v-else />
    <span>
      <small>Vị trí đang chọn</small>
      <strong v-if="selectedLocation" class="mono">{{ selectedLocation.code }}</strong>
      <strong v-else>{{ placeholder }}</strong>
      <em v-if="selectedLocation">{{ selectedLocation.name }}<template v-if="locationDetails[selectedLocation.id]"> · {{ locationDetails[selectedLocation.id] }}</template></em>
    </span>
      <b v-if="!actionOutside">{{ selectedLocation ? 'Đổi vị trí' : actionLabel }}</b>
    </button>
  </div>

  <AppModal :open="open" :title="title" :description="description" width="1040px" @close="open = false">
    <div v-if="racks.length" class="picker-stage">
      <article v-for="rack in racks" :key="rack.rack" class="picker-rack">
        <header><Warehouse :size="18" /> Kệ {{ rack.rack }}</header>
        <div class="picker-frame">
          <div v-for="level in rack.levels" :key="level.level" class="picker-level">
            <span class="picker-level-label">T{{ level.level }}</span>
            <button v-for="location in level.bins" :key="location.id" type="button" class="picker-bin" :class="{ selected: pendingValue === location.id }" @click="pendingValue = location.id">
              <Check v-if="pendingValue === location.id" :size="15" />
              <span>N{{ location.bin }}</span>
              <strong>{{ location.code }}</strong>
              <small v-if="locationDetails[location.id]">{{ locationDetails[location.id] }}</small>
            </button>
          </div>
        </div>
      </article>
    </div>
    <AppEmpty v-else title="Chưa có vị trí phù hợp" message="Không có ngăn kho nào đáp ứng điều kiện để lựa chọn." />
    <template #footer>
      <button type="button" class="btn btn-secondary" @click="open = false">Hủy</button>
      <button type="button" class="btn btn-primary" :disabled="!pendingValue" @click="confirm"><MapPin :size="15" /> Chọn vị trí</button>
    </template>
  </AppModal>
</template>

<style scoped>
.location-picker { display: grid; gap: 6px; }
.location-actions { display: flex; justify-content: flex-end; }
.link-button { border: 0; color: var(--blue); background: transparent; font-size: 12px; font-weight: 700; text-decoration: underline; }
.link-button:disabled { cursor: not-allowed; opacity: .6; text-decoration: none; }
.location-trigger { display: grid; width: 100%; min-height: 66px; grid-template-columns: 22px minmax(0, 1fr) auto; align-items: center; gap: 10px; padding: 11px 13px; border: 1px solid var(--line); border-radius: 10px; color: var(--navy-800); background: #f8fafb; text-align: left; }.location-trigger:hover { border-color: #86a9bf; background: #f1f7fa; }.location-trigger:disabled { cursor: not-allowed; opacity: .6; }.location-trigger span { min-width: 0; }.location-trigger small,.location-trigger strong,.location-trigger em { display: block; }.location-trigger small { color: var(--muted); font-size: 10px; }.location-trigger strong { margin-top: 2px; color: var(--navy-950); }.location-trigger em { overflow: hidden; margin-top: 2px; color: var(--muted); font-size: 10px; font-style: normal; text-overflow: ellipsis; white-space: nowrap; }.location-trigger b { color: var(--blue); font-size: 11px; white-space: nowrap; }
.location-trigger.compact { min-height: 58px; }
.location-trigger.no-icon { grid-template-columns: minmax(0, 1fr); }
.location-trigger.no-icon > i { display: none; }
.picker-stage { display: grid; grid-template-columns: repeat(auto-fit, minmax(270px, 1fr)); gap: 25px; padding: 3px; perspective: 1000px; }.picker-rack { filter: drop-shadow(8px 10px 10px rgb(20 40 60 / 12%)); transform: rotateY(-2deg); transform-style: preserve-3d; }.picker-rack header { display: flex; align-items: center; gap: 8px; padding: 9px 13px; border-radius: 8px 8px 0 0; color: white; background: var(--navy-900); font-weight: 800; }.picker-frame { display: grid; gap: 7px; padding: 12px; border: 5px solid #547086; border-top: 0; background: #d8e2e9; box-shadow: inset -7px -5px 0 rgb(30 57 77 / 12%); }.picker-level { position: relative; display: grid; grid-template-columns: repeat(auto-fit, minmax(76px, 1fr)); gap: 7px; padding: 4px 4px 9px 31px; border-bottom: 6px solid #50697c; }.picker-level-label { position: absolute; top: 50%; left: 2px; color: #365267; font-size: 10px; font-weight: 900; transform: translateY(-50%); }.picker-bin { position: relative; display: grid; min-height: 76px; place-content: center; border: 1px solid #b87920; border-radius: 4px; color: #6d4408; background: linear-gradient(145deg, #ffe6a8, #e6b44d); box-shadow: inset -5px -5px 0 rgb(118 73 8 / 12%), 3px 4px 0 rgb(50 72 88 / 12%); text-align: center; }.picker-bin:hover { outline: 3px solid rgb(17 110 163 / 20%); transform: translateY(-2px); }.picker-bin.selected { border-color: #087e68; color: #075e50; background: linear-gradient(145deg, #bff2df, #65c9aa); outline: 3px solid rgb(8 126 104 / 22%); }.picker-bin > svg { position: absolute; top: 5px; right: 5px; }.picker-bin > span { font-size: 11px; }.picker-bin > strong { margin-top: 2px; font-size: 10px; }.picker-bin > small { margin-top: 4px; font-size: 9px; font-weight: 800; }
@media (max-width: 560px) { .location-trigger { grid-template-columns: 22px minmax(0, 1fr); }.location-trigger b { grid-column: 2; }.picker-stage { grid-template-columns: 1fr; }.picker-rack { transform: none; } }
</style>
