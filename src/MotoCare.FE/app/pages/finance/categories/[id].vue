<script setup lang="ts">
import { ArrowLeft, CalendarDays, Tags, WalletCards } from '@lucide/vue'
import type { CashCategory } from '~/types/api'
import { formatDate } from '~/utils/format'

const route = useRoute()
const api = useApi()
const category = ref<CashCategory>()
const loading = ref(true)

const categoryId = computed(() => String(route.params.id || ''))
const scopeLabel = (scope: CashCategory['scope']) => ({
  Income: 'Khoản thu',
  Expense: 'Khoản chi',
  Both: 'Khoản thu và khoản chi'
}[scope] || scope)

const load = async () => {
  loading.value = true
  category.value = undefined
  try {
    category.value = await api.request<CashCategory>(`/cash-categories/${categoryId.value}`, {
      query: { includeDeleted: true }
    })
  } catch {
    category.value = undefined
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch(categoryId, () => load())
</script>

<template>
  <div class="page">
    <NuxtLink class="back-link" to="/finance"><ArrowLeft :size="16" /> Quay lại sổ thu chi</NuxtLink>

    <template v-if="category">
      <div class="page-header">
        <div>
          <div class="title-line">
            <h1 class="page-title">{{ category.name }}</h1>
            <AppBadge :tone="category.isDeleted || !category.isActive ? 'neutral' : 'success'">
              {{ category.isDeleted ? 'Đã xóa' : category.isActive ? 'Đang sử dụng' : 'Tạm khóa' }}
            </AppBadge>
          </div>
          <p class="page-subtitle mono">{{ category.code }}</p>
        </div>
      </div>

      <section class="summary-grid">
        <article><Tags :size="20" /><div><span>Mã danh mục</span><strong class="mono">{{ category.code }}</strong></div></article>
        <article><WalletCards :size="20" /><div><span>Phạm vi áp dụng</span><strong>{{ scopeLabel(category.scope) }}</strong></div></article>
        <article><CalendarDays :size="20" /><div><span>Cập nhật gần nhất</span><strong>{{ formatDate(category.updatedAt, true) }}</strong></div></article>
      </section>

      <section class="card">
        <header class="card-header"><h2 class="card-title">Thông tin danh mục thu chi</h2><Tags :size="19" /></header>
        <div class="card-body detail-grid">
          <div><span>Mã danh mục</span><strong class="mono">{{ category.code }}</strong></div>
          <div><span>Tên danh mục</span><strong>{{ category.name }}</strong></div>
          <div><span>Phạm vi áp dụng</span><strong>{{ scopeLabel(category.scope) }}</strong></div>
          <div><span>Trạng thái</span><strong>{{ category.isDeleted ? 'Đã xóa' : category.isActive ? 'Đang hoạt động' : 'Ngừng hoạt động' }}</strong></div>
          <div class="span-2"><span>Mô tả</span><strong>{{ category.description || 'Không có mô tả' }}</strong></div>
          <div><span>Ngày tạo</span><strong>{{ formatDate(category.createdAt, true) }}</strong></div>
          <div><span>Ngày cập nhật</span><strong>{{ formatDate(category.updatedAt, true) }}</strong></div>
        </div>
      </section>
    </template>

    <div v-else-if="loading" class="loading-skeleton" style="height: 380px" />
    <AppEmpty v-else title="Không tìm thấy danh mục" message="Danh mục thu chi không tồn tại hoặc bạn không có quyền xem dữ liệu này." />
  </div>
</template>

<style scoped>
.back-link { display: inline-flex; width: max-content; align-items: center; gap: 7px; color: var(--muted); font-weight: 700; }
.title-line { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.summary-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 14px; }
.summary-grid article { display: flex; min-width: 0; align-items: center; gap: 12px; padding: 17px 19px; border: 1px solid var(--line); border-radius: var(--radius-lg); color: var(--navy-800); background: white; box-shadow: var(--shadow-sm); }
.summary-grid span,.summary-grid strong { display: block; }.summary-grid span { color: var(--muted); font-size: 10px; }.summary-grid strong { margin-top: 4px; overflow: hidden; color: var(--navy-950); font-size: 16px; text-overflow: ellipsis; }
.card-header > svg { color: var(--muted); }
.detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }.detail-grid span,.detail-grid strong { display: block; }.detail-grid span { color: var(--muted); font-size: 11px; }.detail-grid strong { margin-top: 4px; color: var(--navy-950); overflow-wrap: anywhere; }.span-2 { grid-column: span 2; }
@media (max-width: 680px) { .summary-grid,.detail-grid { grid-template-columns: 1fr; gap: 14px; }.span-2 { grid-column: auto; } }
</style>
