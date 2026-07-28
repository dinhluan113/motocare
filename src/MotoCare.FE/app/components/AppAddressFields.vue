<script setup lang="ts">
import type { AddressDetails, LocationOption } from '~/types/api'

const model = defineModel<AddressDetails>({ required: true })
const api = useApi()
const countries = ref<LocationOption[]>([])
const regions = ref<LocationOption[]>([])
const areas = ref<LocationOption[]>([])
const loadingCountries = ref(false)
const loadingRegions = ref(false)
const loadingAreas = ref(false)

const update = (values: Partial<AddressDetails>) => {
  model.value = { ...model.value, ...values }
}

const loadRegions = async (countryCode: string) => {
  regions.value = []
  areas.value = []
  if (countryCode !== 'VN') return

  loadingRegions.value = true
  try {
    regions.value = await api.request<LocationOption[]>(
      `/locations/countries/${encodeURIComponent(countryCode)}/regions`)
  } finally {
    loadingRegions.value = false
  }
}

const loadAreas = async (countryCode: string, regionCode: string) => {
  areas.value = []
  if (countryCode !== 'VN' || !regionCode) return

  loadingAreas.value = true
  try {
    areas.value = await api.request<LocationOption[]>(
      `/locations/countries/${encodeURIComponent(countryCode)}/regions/${encodeURIComponent(regionCode)}/areas`)
  } finally {
    loadingAreas.value = false
  }
}

const selectCountry = async (code: string) => {
  const country = countries.value.find(option => option.code === code)
  update({
    countryCode: code,
    countryName: country?.name || '',
    regionCode: '',
    regionName: '',
    areaCode: '',
    areaName: ''
  })
  await loadRegions(code)
}

const selectRegion = async (code: string) => {
  const region = regions.value.find(option => option.code === code)
  update({
    regionCode: code,
    regionName: region?.name || '',
    areaCode: '',
    areaName: ''
  })
  await loadAreas(model.value.countryCode, code)
}

const selectArea = (code: string) => {
  const area = areas.value.find(option => option.code === code)
  update({ areaCode: code, areaName: area?.name || '' })
}

onMounted(async () => {
  loadingCountries.value = true
  try {
    countries.value = localizeCountries(
      await api.request<LocationOption[]>('/locations/countries'))
    const countryCode = model.value.countryCode || 'VN'
    const country = countries.value.find(option => option.code === countryCode)
    update({ countryCode, countryName: country?.name || model.value.countryName })
    await loadRegions(countryCode)

    if (model.value.regionCode) {
      const region = regions.value.find(option => option.code === model.value.regionCode)
      update({ regionName: region?.name || model.value.regionName })
      await loadAreas(countryCode, model.value.regionCode)
      const area = areas.value.find(option => option.code === model.value.areaCode)
      if (area) update({ areaName: area.name })
    }
  } finally {
    loadingCountries.value = false
  }
})
</script>

<template>
  <div class="field">
    <label>Quốc gia</label>
    <AppSearchSelect
      :model-value="model.countryCode"
      :options="countries"
      :loading="loadingCountries"
      placeholder="Chọn quốc gia"
      search-placeholder="Tìm quốc gia..."
      @update:model-value="selectCountry"
    />
  </div>
  <div v-if="model.countryCode === 'VN'" class="field">
    <label>Tỉnh / Thành phố</label>
    <AppSearchSelect
      :model-value="model.regionCode"
      :options="regions"
      :loading="loadingRegions"
      :disabled="!model.countryCode"
      placeholder="Chọn tỉnh, thành phố"
      search-placeholder="Tìm tỉnh, thành phố..."
      @update:model-value="selectRegion"
    />
  </div>
  <div v-if="model.countryCode === 'VN'" class="field">
    <label>Phường / Xã</label>
    <AppSearchSelect
      :model-value="model.areaCode"
      :options="areas"
      :loading="loadingAreas"
      :disabled="!model.regionCode"
      placeholder="Chọn phường, xã"
      search-placeholder="Tìm kiếm..."
      @update:model-value="selectArea"
    />
  </div>
  <div class="field">
    <label>Địa chỉ chi tiết</label>
    <input
      :value="model.addressLine"
      class="input"
      maxlength="250"
      placeholder="Số nhà, tên đường..."
      @input="update({ addressLine: ($event.target as HTMLInputElement).value })"
    >
  </div>
</template>
