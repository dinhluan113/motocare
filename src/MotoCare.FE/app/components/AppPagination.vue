<script setup lang="ts">
import { ChevronLeft, ChevronRight } from '@lucide/vue'

const props = defineProps<{
  page: number
  totalPages: number
  total: number
}>()

const emit = defineEmits<{ change: [page: number] }>()
</script>

<template>
  <div class="pagination">
    <span>{{ props.total.toLocaleString('vi-VN') }} bản ghi</span>
    <div class="inline">
      <button
        class="icon-btn"
        aria-label="Trang trước"
        :disabled="props.page <= 1"
        @click="emit('change', props.page - 1)"
      >
        <ChevronLeft :size="17" />
      </button>
      <strong>Trang {{ props.page }} / {{ Math.max(1, props.totalPages) }}</strong>
      <button
        class="icon-btn"
        aria-label="Trang sau"
        :disabled="props.page >= props.totalPages"
        @click="emit('change', props.page + 1)"
      >
        <ChevronRight :size="17" />
      </button>
    </div>
  </div>
</template>

<style scoped>
.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 14px;
  padding: 14px 18px;
  border-top: 1px solid var(--line);
  color: var(--muted);
  font-size: 12px;
}

.pagination strong {
  color: var(--navy-900);
}

@media (max-width: 560px) {
  .pagination {
    align-items: stretch;
    flex-direction: column;
    padding: 12px 14px;
    text-align: center;
  }

  .pagination .inline {
    justify-content: space-between;
  }
}
</style>
