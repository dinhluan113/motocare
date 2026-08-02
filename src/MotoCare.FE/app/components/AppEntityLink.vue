<script setup lang="ts">
import { ExternalLink } from '@lucide/vue'

withDefaults(defineProps<{
  to?: string
  title?: string
  block?: boolean
  icon?: boolean
}>(), {
  to: undefined,
  title: 'Xem chi tiết',
  block: false,
  icon: false
})
</script>

<template>
  <NuxtLink
    v-if="to"
    class="entity-link"
    :class="{ 'entity-link-block': block }"
    :to="to"
    :title="title"
    @click.stop
    @keydown.enter.stop
  >
    <span class="entity-link-content" :class="{ 'entity-link-content-block': block }"><slot /></span>
    <ExternalLink v-if="icon" :size="12" aria-hidden="true" />
  </NuxtLink>
  <span v-else><slot /></span>
</template>

<style scoped>
.entity-link { display: inline-flex; max-width: 100%; align-items: center; gap: 5px; color: var(--blue); font-weight: inherit; text-underline-offset: 3px; }
.entity-link:hover { text-decoration: underline; }
.entity-link-block { width: max-content; align-items: flex-start; }
.entity-link-content { min-width: 0; }
.entity-link-content-block { display: grid; gap: 1px; }
</style>
