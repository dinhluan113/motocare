<script setup lang="ts">
defineOptions({ inheritAttrs: false })

const props = withDefaults(defineProps<{
  min?: number | string
  max?: number | string
  step?: number | string
  required?: boolean
  disabled?: boolean
}>(), {
  required: false,
  disabled: false
})

const model = defineModel<number | null | undefined>()
const input = ref<HTMLInputElement>()
const displayValue = ref('')
const focused = ref(false)

const formatNumber = (value: number | null | undefined) => {
  if (value === null || value === undefined || !Number.isFinite(Number(value))) return ''

  const [integer = '', decimal] = String(value).split('.')
  const sign = integer.startsWith('-') ? '-' : ''
  const digits = sign ? integer.slice(1) : integer
  const formattedInteger = digits.replace(/\B(?=(\d{3})+(?!\d))/g, ',')

  return `${sign}${formattedInteger}${decimal === undefined ? '' : `.${decimal}`}`
}

const sanitize = (rawValue: string) => {
  const allowNegative = props.min === undefined || Number(props.min) < 0
  let value = rawValue.replace(/,/g, '').replace(/[^\d.-]/g, '')
  const negative = allowNegative && value.startsWith('-')

  value = value.replace(/-/g, '')
  const decimalIndex = value.indexOf('.')
  if (decimalIndex >= 0) {
    value = `${value.slice(0, decimalIndex)}.${value.slice(decimalIndex + 1).replace(/\./g, '')}`
  }

  const [integer = '', decimal] = value.split('.')
  const normalizedInteger = integer.replace(/^0+(?=\d)/, '') || (decimal !== undefined ? '0' : '')
  return `${negative ? '-' : ''}${normalizedInteger}${decimal === undefined ? '' : `.${decimal}`}`
}

const formatEditable = (value: string) => {
  if (!value || value === '-' || value === '.' || value === '-.') return value

  const negative = value.startsWith('-')
  const unsigned = negative ? value.slice(1) : value
  const [integer, decimal] = unsigned.split('.')
  const formattedInteger = (integer || '0').replace(/\B(?=(\d{3})+(?!\d))/g, ',')

  return `${negative ? '-' : ''}${formattedInteger}${decimal === undefined ? '' : `.${decimal}`}`
}

const validate = () => {
  if (!input.value) return

  const value = model.value
  if (value === null || value === undefined) {
    input.value.setCustomValidity('')
    return
  }

  if (props.min !== undefined && value < Number(props.min)) {
    input.value.setCustomValidity(`Giá trị phải lớn hơn hoặc bằng ${formatNumber(Number(props.min))}.`)
    return
  }

  if (props.max !== undefined && value > Number(props.max)) {
    input.value.setCustomValidity(`Giá trị phải nhỏ hơn hoặc bằng ${formatNumber(Number(props.max))}.`)
    return
  }

  if (props.step !== undefined) {
    const step = Number(props.step)
    const base = props.min === undefined ? 0 : Number(props.min)
    const stepOffset = (value - base) / step
    if (step > 0 && Math.abs(stepOffset - Math.round(stepOffset)) > 1e-9) {
      input.value.setCustomValidity(`Giá trị phải theo bước ${formatNumber(step)}.`)
      return
    }
  }

  input.value.setCustomValidity('')
}

const onInput = (event: Event) => {
  const target = event.target as HTMLInputElement
  const caret = target.selectionStart ?? target.value.length
  const tokensBeforeCaret = target.value.slice(0, caret).replace(/[^0-9.-]/g, '').length
  const sanitized = sanitize(target.value)
  const formatted = formatEditable(sanitized)

  displayValue.value = formatted
  target.value = formatted

  let nextCaret = 0
  let seenTokens = 0
  while (nextCaret < formatted.length && seenTokens < tokensBeforeCaret) {
    if (/[0-9.-]/.test(formatted[nextCaret] || '')) seenTokens++
    nextCaret++
  }
  target.setSelectionRange(nextCaret, nextCaret)

  const numericValue = Number(sanitized)
  model.value = sanitized && sanitized !== '-' && sanitized !== '.' && sanitized !== '-.'
    && Number.isFinite(numericValue)
    ? numericValue
    : undefined
  validate()
}

const onFocus = () => {
  focused.value = true
}

const onBlur = () => {
  focused.value = false
  displayValue.value = formatNumber(model.value)
  validate()
}

watch([model, () => props.min, () => props.max, () => props.step], ([value]) => {
  if (!focused.value) displayValue.value = formatNumber(value)
  nextTick(validate)
}, { immediate: true })
</script>

<template>
  <input
    ref="input"
    v-bind="$attrs"
    :value="displayValue"
    type="text"
    inputmode="decimal"
    :min="min"
    :max="max"
    :step="step"
    :required="required"
    :disabled="disabled"
    @input="onInput"
    @focus="onFocus"
    @blur="onBlur"
  >
</template>
