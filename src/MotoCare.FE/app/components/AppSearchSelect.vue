<script setup lang="ts">
import { Check, ChevronDown, Search, X } from '@lucide/vue'
import type { LocationOption } from '~/types/api'

defineOptions({ inheritAttrs: false })

const props = withDefaults(defineProps<{
  modelValue?: string
  options: LocationOption[]
  placeholder?: string
  searchPlaceholder?: string
  loading?: boolean
  disabled?: boolean
  required?: boolean
  clearable?: boolean
}>(), {
  modelValue: '',
  placeholder: 'Chọn dữ liệu',
  searchPlaceholder: 'Tìm kiếm...',
  loading: false,
  disabled: false,
  required: false,
  clearable: true
})

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
const root = ref<HTMLElement>()
const menu = ref<HTMLElement>()
const searchInput = ref<HTMLInputElement>()
const open = ref(false)
const search = ref('')
const menuStyle = ref<Record<string, string>>({})

const selected = computed(() =>
  props.options.find(option => option.code === props.modelValue))

const normalizeSearchText = (value: string) =>
  value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLocaleLowerCase('vi-VN')

const normalizedSearch = computed(() =>
  normalizeSearchText(search.value.trim()))

const filteredOptions = computed(() => {
  if (!normalizedSearch.value) return props.options
  return props.options.filter(option =>
    normalizeSearchText(option.name).includes(normalizedSearch.value))
})

const updateMenuPosition = () => {
  if (!open.value || !root.value) return

  const rect = root.value.getBoundingClientRect()
  const gap = 6
  const viewportPadding = 8
  const preferredHeight = 278
  const spaceBelow = window.innerHeight - rect.bottom - gap - viewportPadding
  const spaceAbove = rect.top - gap - viewportPadding
  const openAbove = spaceBelow < preferredHeight && spaceAbove > spaceBelow
  const availableHeight = Math.max(
    130,
    Math.min(preferredHeight, openAbove ? spaceAbove : spaceBelow)
  )
  const width = Math.min(rect.width, window.innerWidth - viewportPadding * 2)
  const left = Math.min(
    Math.max(viewportPadding, rect.left),
    window.innerWidth - width - viewportPadding
  )

  menuStyle.value = {
    position: 'fixed',
    left: `${left}px`,
    width: `${width}px`,
    top: openAbove ? 'auto' : `${rect.bottom + gap}px`,
    bottom: openAbove ? `${window.innerHeight - rect.top + gap}px` : 'auto',
    '--options-max-height': `${Math.max(80, availableHeight - 60)}px`
  }
}

const toggle = async () => {
  if (props.disabled || props.loading) return
  open.value = !open.value
  search.value = ''
  if (open.value) {
    await nextTick()
    updateMenuPosition()
    await nextTick()
    searchInput.value?.focus({ preventScroll: true })
  }
}

const select = (value: string) => {
  emit('update:modelValue', value)
  open.value = false
  search.value = ''
}

const clear = () => {
  if (!props.disabled) emit('update:modelValue', '')
}

const closeOnOutsideClick = (event: MouseEvent) => {
  const target = event.target as Node
  if (!root.value?.contains(target) && !menu.value?.contains(target)) open.value = false
}

onMounted(() => {
  document.addEventListener('mousedown', closeOnOutsideClick)
  document.addEventListener('scroll', updateMenuPosition, true)
  window.addEventListener('resize', updateMenuPosition)
})
onBeforeUnmount(() => {
  document.removeEventListener('mousedown', closeOnOutsideClick)
  document.removeEventListener('scroll', updateMenuPosition, true)
  window.removeEventListener('resize', updateMenuPosition)
})
</script>

<template>
  <div ref="root" class="search-select" :class="{ open, disabled }">
    <button
      v-bind="$attrs"
      type="button"
      class="search-select-trigger"
      :disabled="disabled"
      :aria-expanded="open"
      aria-haspopup="listbox"
      @click="toggle"
    >
      <span :class="{ placeholder: !selected }">
        {{ loading ? 'Đang tải...' : selected?.name || placeholder }}
      </span>
      <span class="search-select-actions">
        <X
          v-if="clearable && selected && !disabled"
          :size="15"
          aria-label="Xóa lựa chọn"
          @click.stop="clear"
        />
        <ChevronDown :size="17" />
      </span>
    </button>
    <input
      v-if="required"
      class="search-select-required"
      tabindex="-1"
      aria-hidden="true"
      :value="modelValue"
      required
    >

    <Teleport to="body">
      <div
        v-if="open"
        ref="menu"
        class="search-select-menu"
        :style="menuStyle"
      >
        <div class="search-select-search">
          <Search :size="16" />
          <input
            ref="searchInput"
            v-model="search"
            :placeholder="searchPlaceholder"
            @keydown.esc="open = false"
          >
        </div>
        <div class="search-select-options" role="listbox">
          <button
            v-for="option in filteredOptions"
            :key="option.code"
            type="button"
            role="option"
            :aria-selected="option.code === modelValue"
            @click="select(option.code)"
          >
            <span>{{ option.name }}</span>
            <Check v-if="option.code === modelValue" :size="16" />
          </button>
          <p v-if="!filteredOptions.length">Không tìm thấy dữ liệu.</p>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.search-select { position: relative; width: 100%; }
.search-select-trigger {
  display: flex;
  width: 100%;
  min-height: 42px;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 0 11px;
  border: 1px solid #cfd8e1;
  border-radius: 10px;
  color: var(--ink);
  background: white;
  text-align: left;
}
.search-select.open .search-select-trigger {
  border-color: var(--blue);
  box-shadow: 0 0 0 3px rgb(47 127 179 / 12%);
}
.search-select-trigger .placeholder { color: #8995a3; }
.search-select-actions { display: inline-flex; align-items: center; gap: 5px; color: var(--muted); }
.search-select.disabled { opacity: .62; }
.search-select-menu {
  z-index: 300;
  overflow: hidden;
  border: 1px solid var(--line);
  border-radius: 11px;
  background: white;
  box-shadow: 0 16px 38px rgb(15 35 55 / 18%);
}
.search-select-search { position: relative; padding: 9px; border-bottom: 1px solid var(--line); }
.search-select-search svg { position: absolute; top: 50%; left: 20px; color: var(--muted); transform: translateY(-50%); }
.search-select-search input {
  width: 100%;
  min-height: 36px;
  padding: 0 10px 0 34px;
  border: 1px solid #cfd8e1;
  border-radius: 8px;
  outline: none;
}
.search-select-options { max-height: var(--options-max-height, 220px); overflow-y: auto; padding: 5px; }
.search-select-options button {
  display: flex;
  width: 100%;
  align-items: center;
  justify-content: space-between;
  padding: 9px 10px;
  border: 0;
  border-radius: 7px;
  color: var(--ink);
  background: transparent;
  text-align: left;
}
.search-select-options button:hover,
.search-select-options button[aria-selected='true'] { color: var(--navy-900); background: var(--blue-soft); }
.search-select-options p { margin: 12px; color: var(--muted); text-align: center; }
.search-select-required {
  position: absolute;
  width: 1px;
  height: 1px;
  opacity: 0;
  pointer-events: none;
}
</style>
