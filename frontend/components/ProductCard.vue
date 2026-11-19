<script setup lang="ts">
import type { CatalogueProduct } from '~/types/catalogue'

const props = defineProps<{ product: CatalogueProduct & { startingPrice?: string } }>()
const { busy, mutate } = useCart()
const imageFailed = ref(false)
watch(() => props.product.images[0], () => { imageFailed.value = false })
const brandImage = '/images/logo.jpg'
const showImage = computed(() => !!props.product.images[0] && props.product.images[0] !== brandImage && !imageFailed.value)
const sizes = computed(() => [...new Set(props.product.variants.map(v => v.size).filter(Boolean))])
const single = computed(() => props.product.variants.length === 1)
const target = computed(() => '/products/' + props.product.id)
const justAdded = ref(false)
async function add() {
  const variant = props.product.variants[0]
  if (!variant) return
  await mutate('POST', variant.id, 1)
  justAdded.value = true
  setTimeout(() => { justAdded.value = false }, 2000)
}
</script>

<template>
  <article class="product-card card relative flex min-w-0 flex-col">
    <div class="relative grid aspect-[4/5] place-items-center overflow-hidden bg-lavender/60 p-2 sm:p-3">
      <img v-if="showImage" :src="product.images[0]" :alt="'عبوة ' + product.name" loading="lazy" decoding="async"
        width="320" height="400" class="size-full object-contain" @error="imageFailed = true">
      <div v-else class="grid size-full place-items-center bg-lavender" role="img" :aria-label="'صورة ' + product.name + ' غير متاحة حالياً'">
        <img src="/brand/flower-icon.webp" alt="" aria-hidden="true" width="96" height="96" loading="lazy" class="size-16 rounded-2xl opacity-90 sm:size-24">
      </div>
    </div>
    <div class="flex flex-1 flex-col gap-1.5 p-3 sm:p-4">
      <h3 class="line-clamp-2 min-h-[3.4em] text-sm leading-snug font-bold text-ink sm:text-base">
        <NuxtLink :to="target" class="stretched rounded-md focus-visible:outline-offset-4">{{ product.name }}</NuxtLink>
      </h3>
      <ul v-if="sizes.length" class="flex flex-wrap gap-1" aria-label="الأحجام">
        <li v-for="s in sizes" :key="s" class="rounded-full bg-lavender px-2.5 py-0.5 text-xs font-semibold text-brand">{{ s }}</li>
      </ul>
      <p v-if="product.startingPrice !== undefined" class="price mt-auto pt-1 text-base sm:text-lg">
        <span v-if="product.variants.length > 1" class="text-sm font-semibold text-ink-soft">من </span><bdi>{{ product.startingPrice }}</bdi> د.ل
      </p>
      <button v-if="single" type="button" class="btn-primary relative z-10 mt-2 w-full text-sm" :disabled="busy" @click="add">
        {{ justAdded ? 'تمت الإضافة ✓' : 'أضف للسلة' }}
      </button>
      <NuxtLink v-else :to="target" tabindex="-1" aria-hidden="true" class="btn-secondary relative z-10 mt-2 w-full text-sm">اختار الحجم</NuxtLink>
    </div>
  </article>
</template>

<style scoped>
.stretched::after { content: ''; position: absolute; inset: 0; z-index: 0; }
.product-card:focus-within { border-color: var(--color-brand); }
@media (prefers-reduced-motion: no-preference) {
  .product-card { transition: transform 160ms ease-out, box-shadow 160ms ease-out; }
  .product-card:hover { transform: translateY(-2px); box-shadow: 0 10px 26px rgb(48 8 26 / 0.16); }
}
</style>
