<script setup lang="ts">
import { CircleCheck, CircleX, Info, X } from '@lucide/vue'

const toast = useToast()
const icons = {
  success: CircleCheck,
  error: CircleX,
  info: Info
}
</script>

<template>
  <Teleport to="body">
    <div class="toast-stack" aria-live="polite">
      <TransitionGroup name="toast">
        <article
          v-for="item in toast.messages.value"
          :key="item.id"
          class="toast"
          :class="`toast-${item.type}`"
        >
          <component :is="icons[item.type]" :size="21" />
          <div>
            <strong>{{ item.title }}</strong>
            <p v-if="item.message">{{ item.message }}</p>
          </div>
          <button aria-label="Đóng thông báo" @click="toast.remove(item.id)">
            <X :size="16" />
          </button>
        </article>
      </TransitionGroup>
    </div>
  </Teleport>
</template>

<style scoped>
.toast-stack {
  position: fixed;
  z-index: 200;
  right: 18px;
  bottom: 18px;
  display: grid;
  width: min(390px, calc(100vw - 36px));
  gap: 10px;
}

.toast {
  display: grid;
  grid-template-columns: auto 1fr auto;
  align-items: start;
  gap: 11px;
  padding: 14px;
  border: 1px solid var(--line);
  border-radius: 13px;
  background: white;
  box-shadow: 0 18px 50px rgb(10 31 51 / 20%);
}

.toast-success > svg { color: var(--teal); }
.toast-error > svg { color: var(--red); }
.toast-info > svg { color: var(--blue); }

.toast strong {
  color: var(--navy-950);
}

.toast p {
  margin: 3px 0 0;
  color: var(--muted);
  font-size: 12px;
}

.toast button {
  padding: 2px;
  border: 0;
  color: var(--muted);
  background: transparent;
}

.toast-enter-active,
.toast-leave-active {
  transition: all 200ms ease;
}

.toast-enter-from,
.toast-leave-to {
  opacity: 0;
  transform: translateX(24px);
}
</style>
