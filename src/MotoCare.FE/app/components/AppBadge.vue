<script setup lang="ts">
const props = withDefaults(defineProps<{
  label?: string
  tone?: 'success' | 'warning' | 'danger' | 'info' | 'neutral'
}>(), {
  label: '',
  tone: 'neutral'
})
</script>

<template>
  <span class="badge" :class="`badge-${props.tone}`">
    <span class="badge-dot" />
    <slot>{{ props.label }}</slot>
  </span>
</template>

<style scoped>
.badge {
  display: inline-flex;
  min-height: 25px;
  align-items: center;
  gap: 6px;
  padding: 4px 9px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 800;
  line-height: 1;
}

.badge-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
}

.badge-success { color: #067466; background: var(--teal-soft); }
.badge-warning { color: #986500; background: var(--amber-soft); }
.badge-danger { color: #ae3838; background: var(--red-soft); }
.badge-info { color: #21698f; background: var(--blue-soft); }
.badge-neutral { color: #637083; background: #eef2f5; }
</style>
