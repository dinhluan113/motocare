<script setup lang="ts">
import { Inbox } from '@lucide/vue'

withDefaults(defineProps<{
  title?: string
  message?: string
}>(), {
  title: 'Chưa có dữ liệu',
  message: 'Dữ liệu sẽ xuất hiện tại đây khi được tạo.'
})
</script>

<template>
  <div class="empty-state">
    <div>
      <Inbox :size="36" :stroke-width="1.5" />
      <strong>{{ title }}</strong>
      <span>{{ message }}</span>
    </div>
  </div>
</template>
