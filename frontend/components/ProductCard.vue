<script setup lang="ts">
import type { CatalogueProduct } from '~/types/catalogue'

const props = defineProps<{ product: CatalogueProduct & { startingPrice?: string } }>()
const { busy, mutate, message } = useCart()
const imageFailed = ref(false)
watch(() => props.product.images[0], () => { imageFailed.value = false })
const brandImage = '/images/logo.jpg'
const showImage = computed(() => !!props.product.images[0] && props.product.images[0] !== brandImage && !imageFailed.value)
const sizes = computed(() => [...new Set(props.product.variants.map(v => v.size).filter(Boolean))])
const single = computed(() => props.product.variants.length === 1)
const target = computed(() => '/products/' + props.product.id)
const toast = useToast()
const adding = ref(false)
const justAdded = ref(false)
const failed = ref(false)
const errorText = ref('')
const overlayText = computed(() => {
  if (props.product.category) return props.product.category
  const s = sizes.value
  if (!s.length) return ''
  return s.length > 1 ? s[0] + ' - ' + s[s.length - 1] : s[0]
})
let resetTimer: ReturnType<typeof setTimeout> | undefined
onBeforeUnmount(() => clearTimeout(resetTimer))
async function add() {
  const variant = props.product.variants[0]
  if (!variant) return
  clearTimeout(resetTimer)
  justAdded.value = false
  failed.value = false
  adding.value = true
  let ok = false
  try {
    ok = await mutate('POST', variant.id, 1)
  } finally {
    adding.value = false
  }
  if (ok) {
    justAdded.value = true
    toast.success('تمت إضافة ' + props.product.name + ' للسلة', { action: { label: 'عرض السلة', to: '/cart' } })
  } else {
    failed.value = true
    errorText.value = message.value || 'تعذر تحديث السلة.'
    toast.error(errorText.value)
  }
  resetTimer = setTimeout(() => { justAdded.value = false; failed.value = false }, ok ? 2000 : 4000)
}
</script>

<template>
  <article class="product-card card relative flex min-w-0 flex-col">
    <div class="image-well relative grid aspect-[4/5] place-items-center overflow-hidden bg-lavender/60 p-2 sm:p-3">
      <img v-if="showImage" :src="product.images[0]" :alt="'عبوة ' + product.name" loading="lazy" decoding="async"
        width="320" height="400" class="product-img size-full object-contain" @error="imageFailed = true">
      <div v-else class="grid size-full place-items-center bg-lavender" role="img" :aria-label="'صورة ' + product.name + ' غير متاحة حالياً'">
        <img src="/brand/flower-icon.webp" alt="" aria-hidden="true" width="96" height="96" loading="lazy" class="size-16 rounded-2xl opacity-90 sm:size-24">
      </div>
      <span v-if="overlayText" class="card-glass pointer-events-none absolute inset-x-2 bottom-2 truncate px-3 py-1.5 text-center text-xs font-semibold text-brand sm:inset-x-3 sm:bottom-3" style="border-radius: 9999px">{{ overlayText }}</span>
    </div>
    <div class="flex flex-1 flex-col gap-1.5 p-3 sm:p-4">
      <h3 class="line-clamp-2 min-h-[2.75em] text-sm leading-snug font-bold text-ink sm:text-base">
        <NuxtLink :to="target" class="stretched rounded-md focus-visible:outline-offset-4">{{ product.name }}</NuxtLink>
      </h3>
      <ul v-if="sizes.length" class="flex flex-wrap gap-1" aria-label="الأحجام">
        <li v-for="s in sizes" :key="s" class="rounded-full bg-lavender px-2.5 py-0.5 text-xs font-semibold text-brand">{{ s }}</li>
      </ul>
      <p v-if="product.startingPrice !== undefined" class="price mt-auto pt-1 text-base sm:text-lg">
        <span v-if="product.variants.length > 1" class="text-sm font-semibold text-ink-soft">من </span><bdi>{{ product.startingPrice }}</bdi> د.ل
      </p>
      <button v-if="single" type="button" class="btn-primary relative z-10 mt-2 w-full gap-1.5 whitespace-nowrap px-3 text-sm" :disabled="busy" :aria-busy="adding" @click="add">
        <span v-if="adding" class="spinner" aria-hidden="true" />
        <AppIcon v-else :name="justAdded ? 'check' : 'cart'" :size="18" aria-hidden="true" />
        <span>{{ adding ? 'جاري الإضافة...' : justAdded ? 'تمت الإضافة ✓' : 'أضيفي للسلة' }}</span>
      </button>
      <p v-if="single && failed" role="alert" class="relative z-10 text-xs font-semibold text-danger">{{ errorText }}</p>
      <span v-else-if="!product.variants.length" class="mt-2 w-full cursor-not-allowed rounded-xl bg-lavender px-4 py-2.5 text-center text-sm font-semibold text-ink-soft opacity-70" aria-disabled="true">غير متاح حالياً</span>
      <NuxtLink v-else-if="!single" :to="target" tabindex="-1" aria-hidden="true" class="btn-secondary relative z-10 mt-2 w-full whitespace-nowrap px-3 text-sm">اختاري الحجم</NuxtLink>
    </div>
  </article>
</template>

<style scoped>
.stretched::after { content: ''; position: absolute; inset: 0; z-index: 0; }
.product-card:focus-within { border-color: var(--color-brand); }
.product-card:hover { box-shadow: var(--shadow-lift); }
.spinner { width: 1rem; height: 1rem; border-radius: 9999px; border: 2px solid rgb(255 255 255 / 0.4); border-top-color: #fff; }
@media (prefers-reduced-motion: no-preference) {
  .product-card { transition: transform 400ms cubic-bezier(.34, 1.56, .64, 1), box-shadow 400ms cubic-bezier(.34, 1.56, .64, 1), border-color 200ms ease-out; }
  .product-card:hover { transform: translateY(-8px); }
  .product-img { transition: transform 400ms cubic-bezier(.34, 1.56, .64, 1); }
  .product-card:hover .product-img { transform: scale(1.08); }
  .spinner { animation: pc-spin 0.7s linear infinite; }
  @keyframes pc-spin { to { transform: rotate(360deg); } }
}
</style>
