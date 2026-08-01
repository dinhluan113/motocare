<script setup lang="ts">
import { X } from '@lucide/vue'
import VueEasyLightbox from 'vue-easy-lightbox'

const props = withDefaults(defineProps<{
  images: string[]
  alt?: string
  removable?: boolean
  compact?: boolean
}>(), {
  alt: 'Ảnh đính kèm',
  removable: false,
  compact: false
})

const emit = defineEmits<{
  remove: [index: number]
}>()

const { mediaUrl } = useMedia()
const visible = ref(false)
const activeIndex = ref(0)
const sources = computed(() => props.images.filter(Boolean).map(mediaUrl))

const open = (index: number) => {
  activeIndex.value = index
  visible.value = true
}
</script>

<template>
  <div class="image-gallery" :class="{ compact }">
    <div v-for="(image, index) in sources" :key="`${image}-${index}`" class="thumbnail-wrap">
      <button type="button" class="thumbnail" :aria-label="`Xem ${alt} ${index + 1}`" @click="open(index)">
        <img :src="image" :alt="`${alt} ${index + 1}`" loading="lazy" />
      </button>
      <button
        v-if="removable"
        type="button"
        class="remove-thumbnail"
        :aria-label="`Xóa ${alt} ${index + 1}`"
        title="Xóa ảnh"
        @click.stop="emit('remove', index)"
      >
        <X :size="13" />
      </button>
    </div>

    <ClientOnly>
      <VueEasyLightbox
        :visible="visible"
        :imgs="sources"
        :index="activeIndex"
        :loop="true"
        :move-disabled="true"
        :scroll-disabled="true"
        :swipe-tolerance="30"
        :zoom-scale="0.2"
        @hide="visible = false"
      />
    </ClientOnly>
  </div>
</template>

<style scoped>
.image-gallery { display: flex; flex-wrap: wrap; gap: 8px; }
.thumbnail-wrap { position: relative; width: 88px; height: 66px; }
.thumbnail { width: 100%; height: 100%; overflow: hidden; padding: 0; border: 1px solid var(--line); border-radius: 9px; background: #eef2f5; cursor: zoom-in; }
.thumbnail img { display: block; width: 100%; height: 100%; object-fit: cover; transition: transform .18s ease; }
.thumbnail:hover img { transform: scale(1.05); }
.thumbnail:focus-visible { outline: 2px solid var(--blue); outline-offset: 2px; }
.remove-thumbnail { position: absolute; top: -6px; right: -6px; display: grid; width: 23px; height: 23px; place-items: center; padding: 0; border: 1px solid #f2caca; border-radius: 50%; color: var(--red); background: white; box-shadow: 0 2px 7px rgb(15 35 55 / 18%); cursor: pointer; }
.compact { gap: 5px; }
.compact .thumbnail-wrap { width: 42px; height: 34px; }
.compact .thumbnail { border-radius: 6px; }
:deep(.vel-modal) { z-index: 10000 !important; }
</style>
