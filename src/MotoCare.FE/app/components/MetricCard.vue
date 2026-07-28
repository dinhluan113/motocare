<script setup lang="ts">
import type { Component } from 'vue'

withDefaults(defineProps<{
  label: string
  value: string | number
  detail?: string
  tone?: 'navy' | 'teal' | 'amber' | 'blue'
  icon: Component
}>(), {
  detail: '',
  tone: 'navy'
})
</script>

<template>
  <article class="metric-card" :class="`metric-${tone}`">
    <div class="metric-icon">
      <component :is="icon" :size="21" />
    </div>
    <div>
      <p>{{ label }}</p>
      <strong>{{ value }}</strong>
      <span v-if="detail">{{ detail }}</span>
    </div>
  </article>
</template>

<style scoped>
.metric-card {
  position: relative;
  display: flex;
  min-height: 138px;
  align-items: flex-start;
  gap: 15px;
  overflow: hidden;
  padding: 20px;
  border: 1px solid var(--line);
  border-radius: 16px;
  background: white;
  box-shadow: var(--shadow);
}

.metric-card::after {
  position: absolute;
  right: -24px;
  bottom: -36px;
  width: 110px;
  height: 110px;
  border-radius: 50%;
  background: currentColor;
  content: '';
  opacity: 0.05;
}

.metric-icon {
  display: grid;
  width: 42px;
  height: 42px;
  flex: 0 0 auto;
  place-items: center;
  border-radius: 12px;
  background: currentColor;
}

.metric-icon :deep(svg) {
  color: white;
}

.metric-card p {
  margin: 1px 0 6px;
  color: var(--muted);
  font-size: 12px;
  font-weight: 700;
}

.metric-card strong {
  display: block;
  color: var(--navy-950);
  font-size: 1.6rem;
  line-height: 1.1;
  letter-spacing: -0.04em;
}

.metric-card span {
  display: block;
  margin-top: 7px;
  color: var(--muted);
  font-size: 11px;
}

.metric-navy { color: var(--navy-800); }
.metric-teal { color: var(--teal); }
.metric-amber { color: var(--amber); }
.metric-blue { color: var(--blue); }
</style>
