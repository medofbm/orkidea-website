<script setup lang="ts">
import type { CatalogueProduct } from '~/types/catalogue'

const props = defineProps<{ product: CatalogueProduct & { startingPrice?: string } }>()
const { busy, mutate, message } = useCart()
const toast = useToast()
const uid = useId()

const imageFailed = ref(false)
// WebP copies (400/800/1200) when they exist; if one fails to load, fall back to the original photo.
const variantFailed = ref(false)
const imageSrcset = computed(() => variantFailed.value ? undefined : productImageSrcset(props.product.images[0]))
function onImageError() {
  if (imageSrcset.value) variantFailed.value = true
  else imageFailed.value = true
}
watch(() => props.product.images[0], () => { imageFailed.value = false })
const brandImage = '/images/logo.jpg'
const showImage = computed(() => !!props.product.images[0] && props.product.images[0] !== brandImage && !imageFailed.value)
const target = computed(() => '/products/' + props.product.id)

const variants = computed(() => props.product.variants)
const multiple = computed(() => variants.value.length > 1)
// Pre-select the cheapest size (only used to pick a default; the price shown is always the server value).
// A size the server marks as not orderable is shown disabled and never pre-selected.
const isOrderable = (v?: { orderable?: boolean }) => v?.orderable !== false
const hasOrderable = computed(() => variants.value.some(isOrderable))
const cheapestId = computed(() => {
  const pool = hasOrderable.value ? variants.value.filter(isOrderable) : variants.value
  let best = pool[0]
  for (const v of pool) if (Number(v.price) < Number(best?.price)) best = v
  return best?.id
})
const pickedId = ref<number | null>(null)
const selected = computed(() => variants.value.find(v => v.id === (pickedId.value ?? cheapestId.value)) ?? variants.value[0])
const priceText = computed(() => selected.value?.price ?? props.product.startingPrice)
const sizeLabel = (size: string) => size || 'الحجم الأساسي'

const adding = ref(false)
const justAdded = ref(false)
const failed = ref(false)
const errorText = ref('')
let resetTimer: ReturnType<typeof setTimeout> | undefined
onBeforeUnmount(() => clearTimeout(resetTimer))

async function add() {
  const variant = selected.value
  if (!variant || adding.value || !isOrderable(variant)) return
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
    toast.success('تمت إضافة ' + props.product.name + ' للسلة', { action: { label: 'افتحي السلة', to: '/cart' } })
  } else {
    failed.value = true
    errorText.value = message.value || 'تعذر تحديث السلة.'
    toast.error(errorText.value)
  }
  resetTimer = setTimeout(() => { justAdded.value = false; failed.value = false }, ok ? 2000 : 4000)
}
</script>

<template>
  <article class="product-card card relative flex h-full min-w-0 flex-col overflow-hidden">
    <div class="relative aspect-[4/5] overflow-hidden rounded-t-2xl bg-gradient-to-br from-lavender to-white">
      <img v-if="showImage" :src="product.images[0]" :srcset="imageSrcset" sizes="(min-width: 1024px) 25vw, (min-width: 768px) 33vw, 50vw"
        :alt="'عبوة ' + product.name" loading="lazy" decoding="async"
        width="320" height="400" class="product-img absolute inset-0 size-full object-cover" @error="onImageError">
      <div v-else class="absolute inset-0 grid place-items-center bg-gradient-to-br from-lavender to-white" role="img" :aria-label="'صورة ' + product.name + ' غير متاحة حالياً'">
        <img src="/brand/flower-purple.png" alt="" aria-hidden="true" width="96" height="96" loading="lazy" class="size-16 object-contain opacity-80 sm:size-24">
      </div>
      <span v-if="product.category" class="pointer-events-none absolute start-2 top-2 max-w-[calc(100%-1rem)] truncate rounded-full bg-white/90 px-2.5 py-1 text-xs font-semibold text-brand shadow-soft backdrop-blur-sm sm:start-3 sm:top-3">{{ product.category }}</span>
    </div>

    <div class="flex flex-1 flex-col gap-2 p-3 sm:p-4">
      <h3 class="line-clamp-2 min-h-[2.75em] text-sm leading-snug font-bold text-ink sm:text-base">
        <NuxtLink :to="target" class="stretched rounded-md focus-visible:outline-offset-4">{{ product.name }}</NuxtLink>
      </h3>

      <div v-if="variants.length === 1" class="relative z-10 flex min-h-[2.75rem] flex-wrap content-start gap-1.5">
        <span class="grid min-h-11 place-items-center rounded-full border px-3 text-xs font-semibold whitespace-nowrap sm:text-sm"
          :class="isOrderable(variants[0]) ? 'border-brand bg-brand text-white' : 'border-brand/25 bg-lavender text-ink-soft opacity-70'">{{ sizeLabel(variants[0]!.size) }}<template v-if="!isOrderable(variants[0])">&nbsp;·&nbsp;غير متاح</template></span>
      </div>
      <div v-else-if="multiple" role="group" :aria-label="'اختاري الحجم لـ ' + product.name" class="relative z-10 flex min-h-[2.75rem] flex-wrap content-start gap-1.5">
        <button v-for="v in variants" :key="v.id" type="button" :aria-pressed="selected?.id === v.id" :disabled="busy || adding || !isOrderable(v)"
          class="grid min-h-11 cursor-pointer place-items-center rounded-full border border-brand/25 bg-white px-3 text-xs font-semibold text-ink-soft focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand disabled:cursor-not-allowed disabled:opacity-60 aria-pressed:border-brand aria-pressed:bg-brand aria-pressed:text-white sm:text-sm"
          @click="pickedId = v.id">
          <!-- Hidden copy reserves the width of the "size · price" text so chips never change width on selection. -->
          <span class="invisible col-start-1 row-start-1 whitespace-nowrap" aria-hidden="true">{{ sizeLabel(v.size) }}&nbsp;·&nbsp;<bdi>{{ formatPrice(v.price) }}</bdi>&nbsp;د.ل</span>
          <span class="col-start-1 row-start-1 whitespace-nowrap">{{ sizeLabel(v.size) }}<template v-if="!isOrderable(v)">&nbsp;·&nbsp;غير متاح</template><template v-else-if="selected?.id === v.id">&nbsp;·&nbsp;<bdi>{{ formatPrice(v.price) }}</bdi>&nbsp;د.ل</template></span>
        </button>
      </div>

      <p v-if="priceText !== undefined && variants.length" class="price mt-auto pt-1 text-base sm:text-lg" aria-live="polite">
        <bdi>{{ formatPrice(priceText) }}</bdi> د.ل
      </p>

      <template v-if="variants.length && hasOrderable">
        <button type="button" :aria-label="'أضيفي ' + product.name + ' للسلة'" class="btn-primary relative z-10 min-h-11 w-full gap-1.5 whitespace-nowrap px-3 text-sm"
          :disabled="busy || adding" :aria-busy="adding" @click="add">
          <span v-if="adding" class="spinner" aria-hidden="true" />
          <AppIcon v-else :name="justAdded ? 'check' : 'cart'" :size="18" aria-hidden="true" />
          <span>{{ adding ? 'جاري الإضافة...' : justAdded ? 'تمت الإضافة' : 'أضيفي للسلة' }}</span>
        </button>
        <p v-if="failed" role="alert" class="relative z-10 text-xs font-semibold text-danger">{{ errorText }}</p>
      </template>
      <span v-else class="mt-auto min-h-11 w-full cursor-not-allowed rounded-xl bg-lavender px-4 py-2.5 text-center text-sm font-semibold text-ink-soft opacity-70" aria-disabled="true">غير متاح حالياً</span>
    </div>
  </article>
</template>

<style scoped>
.stretched::after { content: ''; position: absolute; inset: 0; z-index: 0; }
.product-card:focus-within { border-color: var(--color-brand); }
.product-card:hover { box-shadow: var(--shadow-lift); }
.spinner { width: 1rem; height: 1rem; border-radius: 9999px; border: 2px solid rgb(255 255 255 / 0.4); border-top-color: #fff; }
@media (prefers-reduced-motion: no-preference) {
  .product-card { transition: transform 300ms ease-out, box-shadow 300ms ease-out, border-color 200ms ease-out; }
  .product-card:hover { transform: translateY(-4px); }
  .product-img { transition: transform 500ms ease-out; }
  .product-card:hover .product-img { transform: scale(1.05); }
  .spinner { animation: pc-spin 0.7s linear infinite; }
  @keyframes pc-spin { to { transform: rotate(360deg); } }
}
</style>
