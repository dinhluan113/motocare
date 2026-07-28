<script setup lang="ts">
import type { LocationOption } from '~/types/api'

const props = withDefaults(defineProps<{
  modelValue?: string
  required?: boolean
}>(), {
  modelValue: '',
  required: false
})

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
const api = useApi()
const options = ref<LocationOption[]>([])
const loading = ref(false)
const selectedCode = ref('')

watch(() => props.modelValue, (value) => {
  selectedCode.value = options.value.find(option => option.name === value)?.code || ''
})

watch(selectedCode, (code) => {
  emit('update:modelValue', options.value.find(option => option.code === code)?.name || '')
})

onMounted(async () => {
  loading.value = true
  try {
    options.value = localizeCountries(
      await api.request<LocationOption[]>('/locations/countries'))
    selectedCode.value = options.value.find(option => option.name === props.modelValue)?.code || ''
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <AppSearchSelect
    v-model="selectedCode"
    :options="options"
    :loading="loading"
    :required="required"
    placeholder="Chọn quốc gia"
    search-placeholder="Tìm quốc gia..."
  />
</template>
