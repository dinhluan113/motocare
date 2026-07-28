<script setup lang="ts">
import { X } from '@lucide/vue'

withDefaults(defineProps<{
  open: boolean
  title: string
  description?: string
  width?: string
}>(), {
  description: '',
  width: '620px'
})

const emit = defineEmits<{ close: [] }>()
</script>

<template>
  <Teleport to="body">
    <Transition name="modal">
      <div v-if="open" class="modal-backdrop" @click.self="emit('close')">
        <section
          class="modal"
          role="dialog"
          aria-modal="true"
          :aria-label="title"
          :style="{ maxWidth: width }"
        >
          <header class="modal-header">
            <div>
              <h2>{{ title }}</h2>
              <p v-if="description">{{ description }}</p>
            </div>
            <button class="icon-btn" aria-label="Đóng" @click="emit('close')">
              <X :size="19" />
            </button>
          </header>
          <div class="modal-content">
            <slot />
          </div>
          <footer v-if="$slots.footer" class="modal-footer">
            <slot name="footer" />
          </footer>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.modal-backdrop {
  position: fixed;
  z-index: 100;
  inset: 0;
  display: grid;
  place-items: center;
  padding: 20px;
  background: rgb(5 20 35 / 62%);
  backdrop-filter: blur(4px);
}

.modal {
  width: 100%;
  max-height: min(88vh, 820px);
  overflow: hidden;
  border: 1px solid rgb(255 255 255 / 35%);
  border-radius: 18px;
  background: white;
  box-shadow: 0 30px 90px rgb(0 0 0 / 28%);
}

.modal-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
  padding: 20px 22px 16px;
  border-bottom: 1px solid var(--line);
}

.modal-header h2 {
  margin: 0;
  color: var(--navy-950);
  font-size: 1.2rem;
}

.modal-header p {
  margin: 4px 0 0;
  color: var(--muted);
  font-size: 13px;
}

.modal-content {
  max-height: calc(88vh - 155px);
  overflow-y: auto;
  padding: 22px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  padding: 14px 22px;
  border-top: 1px solid var(--line);
  background: #fafbfc;
}

.modal-enter-active,
.modal-leave-active {
  transition: opacity 160ms ease;
}

.modal-enter-active .modal,
.modal-leave-active .modal {
  transition: transform 180ms ease;
}

.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}

.modal-enter-from .modal,
.modal-leave-to .modal {
  transform: translateY(12px) scale(0.98);
}
</style>
