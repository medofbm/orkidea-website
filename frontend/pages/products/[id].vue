<script setup lang="ts">
import type { CatalogueProduct } from '~/types/catalogue'
const route = useRoute()
const { busy, message, mutate } = useCart()
const { data: product, status, error, refresh } = await useFetch<CatalogueProduct>(
  () => useRuntimeConfig().public.apiBase + '/products/' + route.params.id
)
const selectedImage = ref(0)
const imageSelectedByUser = ref(false)
const selectedVariant = ref(0)
const quantity = ref(1)
const failedImages = ref<string[]>([])
const images = computed(() => product.value?.images.filter(image => image !== '/images/logo.jpg') ?? [])
const currentImage = computed(() => images.value[selectedImage.value])
function markFailedImage(image: string) {
  if (!failedImages.value.includes(image)) failedImages.value.push(image)
}
function checkLoadedImage(element: unknown, image: string) {
  if (element instanceof HTMLImageElement && element.complete && !element.naturalWidth) {
    markFailedImage(image)
  }
}
watch(() => product.value?.id, () => {
  selectedImage.value = 0
  imageSelectedByUser.value = false
  selectedVariant.value = 0
  quantity.value = 1
  failedImages.value = []
})
const currentVariant = computed(() => product.value?.variants[selectedVariant.value])
const paragraphs = computed(() =>
  (product.value?.description ?? '').split(/\r?\n+/).map(p => p.trim()).filter(Boolean))
const whatsappNumber = String(useRuntimeConfig().public.whatsappNumber ?? '').replace(/\D/g, '')
const whatsappLink = computed(() => {
  if (!whatsappNumber || !product.value) return ''
  const size = currentVariant.value ? ' (' + currentVariant.value.size + ')' : ''
  const text = 'السلام عليكم، أرغب في طلب ' + product.value.name + size
  return 'https://wa.me/' + whatsappNumber + '?text=' + encodeURIComponent(text)
})
function addToCart() {
  if (currentVariant.value) mutate('POST', currentVariant.value.id, quantity.value)
}
useSeoMeta({ title: () => product.value?.name ?? 'المنتج', description: () => product.value?.description ?? '' })
</script>

<template>
  <div class="pb-32 md:pb-8">
    <NuxtLink to="/" class="mb-4 inline-flex min-h-11 items-center gap-2 rounded-lg px-2 font-semibold text-brand hover:bg-lavender/60">
      <span aria-hidden="true">→</span> كل المنتجات
    </NuxtLink>

    <div v-if="status === 'pending'" role="status" aria-label="جاري تحميل المنتج" class="grid gap-6 md:grid-cols-2 md:gap-10">
      <div class="skeleton aspect-[4/5] w-full rounded-3xl" />
      <div class="grid content-start gap-4">
        <div class="skeleton h-5 w-24 rounded-full" />
        <div class="skeleton h-9 w-3/4 rounded-lg" />
        <div class="skeleton h-11 w-1/2 rounded-full" />
        <div class="skeleton h-12 w-1/3 rounded-lg" />
        <div class="skeleton h-12 w-full rounded-full" />
      </div>
    </div>

    <div v-else-if="error" role="alert" class="card mx-auto max-w-md p-6 text-center">
      <p class="text-lg font-bold text-ink">
        {{ error.statusCode === 404 ? 'ما لقينا المنتج هذا' : 'تعذر تحميل المنتج' }}
      </p>
      <p class="mt-1 text-sm text-ink-soft">
        {{ error.statusCode === 404 ? 'يمكن اتشال أو الرابط غير صحيح. شوفي باقي منتجاتنا.' : 'راجعي اتصال الإنترنت وحاولي مرة ثانية.' }}
      </p>
      <div class="mt-4 flex flex-wrap justify-center gap-3">
        <button v-if="error.statusCode !== 404" type="button" class="btn-primary" @click="refresh()">إعادة المحاولة</button>
        <NuxtLink to="/" :class="error.statusCode === 404 ? 'btn-primary' : 'btn-secondary'">كل المنتجات</NuxtLink>
      </div>
    </div>

    <article v-else-if="product" aria-labelledby="product-heading"
      class="grid gap-6 md:grid-cols-2 md:gap-x-10 lg:gap-x-14">
      <section aria-label="صور المنتج" class="min-w-0">
        <div class="relative grid aspect-[4/5] w-full place-items-center overflow-hidden rounded-3xl border border-lavender bg-gradient-to-b from-lavender/60 to-cream">
          <img v-if="currentImage && !failedImages.includes(currentImage)" :key="currentImage"
            :src="currentImage" :alt="product.name + ' - صورة ' + (selectedImage + 1)"
            class="absolute inset-0 size-full object-contain p-4"
            :class="{ 'gallery-image-selected': imageSelectedByUser }"
            width="800" height="1000" @vue:mounted="checkLoadedImage($event.el, currentImage)"
            @error="markFailedImage(currentImage)">
          <div v-else class="grid place-items-center gap-2 p-4 text-center text-sm text-ink-soft">
            <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" class="size-10">
              <rect x="3" y="4" width="18" height="16" rx="3" />
              <path d="m3 16 5-5 4 4 3-3 6 6M15 8h.01" stroke-linecap="round" stroke-linejoin="round" />
            </svg>
            <span>صورة غير متاحة</span>
          </div>
        </div>
        <div v-if="images.length > 1" class="-mx-4 mt-3 flex gap-2 overflow-x-auto px-4 pb-1 md:mx-0 md:px-0" role="group" aria-label="اختيار صورة المنتج">
          <button v-for="(image, index) in images" :key="image" type="button"
            :aria-label="'عرض الصورة ' + (index + 1)" :aria-pressed="selectedImage === index"
            class="grid size-16 shrink-0 place-items-center overflow-hidden rounded-xl border-2 bg-white p-1 hover:border-brand-soft sm:size-20"
            :class="selectedImage === index ? 'border-brand ring-2 ring-lavender' : 'border-lavender'"
            @click="selectedImage = index; imageSelectedByUser = true">
            <img v-if="!failedImages.includes(image)" :src="image" alt="" class="size-full object-contain"
              width="80" height="80" loading="lazy" @vue:mounted="checkLoadedImage($event.el, image)"
              @error="markFailedImage(image)">
            <span v-else aria-hidden="true" class="text-sm text-ink-soft">{{ index + 1 }}</span>
          </button>
        </div>
      </section>

      <div class="min-w-0">
        <header>
          <p v-if="product.category" class="mb-2 text-sm font-semibold wrap-anywhere text-accent">{{ product.category }}</p>
          <h1 id="product-heading" class="text-3xl leading-tight font-bold wrap-anywhere text-ink lg:text-4xl">{{ product.name }}</h1>
        </header>

        <section aria-label="خيارات الشراء" class="mt-5">
          <template v-if="currentVariant">
            <p class="price text-3xl" aria-live="polite" aria-atomic="true">
              <bdi>{{ currentVariant.price }}</bdi> <span class="text-base">د.ل</span>
            </p>
            <fieldset class="mt-5 min-w-0" :disabled="busy">
              <legend class="mb-2 text-sm font-semibold text-ink">الحجم</legend>
              <div class="flex flex-wrap gap-2">
                <label v-for="(variant, index) in product.variants" :key="variant.id"
                  class="chip relative cursor-pointer border-2 border-transparent bg-white text-ink ring-1 ring-lavender-deep has-checked:border-brand has-checked:bg-lavender has-checked:text-brand has-focus-visible:outline-3 has-focus-visible:outline-brand">
                  <input v-model="selectedVariant" type="radio" name="variant" :value="index"
                    class="absolute inset-0 size-full cursor-pointer opacity-0 disabled:cursor-not-allowed">
                  <span>{{ variant.size }}</span>
                </label>
              </div>
            </fieldset>

            <div class="mt-5 flex items-center gap-3">
              <span id="qty-label" class="text-sm font-semibold text-ink">الكمية</span>
              <div role="group" aria-labelledby="qty-label" class="inline-flex items-center rounded-full bg-white ring-1 ring-lavender-deep">
                <button type="button" :disabled="busy || quantity >= 99" aria-label="زيادة الكمية"
                  class="size-11 rounded-full text-xl font-bold text-brand hover:bg-lavender disabled:opacity-40" @click="quantity++">+</button>
                <output class="min-w-10 text-center font-bold text-ink" aria-live="polite">{{ quantity }}</output>
                <button type="button" :disabled="busy || quantity <= 1" aria-label="تقليل الكمية"
                  class="size-11 rounded-full text-xl font-bold text-brand hover:bg-lavender disabled:opacity-40" @click="quantity--">−</button>
              </div>
            </div>

            <div class="mt-6 hidden gap-3 md:grid">
              <button type="button" :disabled="busy" class="btn-primary storefront-action w-full" @click="addToCart">أضيفي للسلة</button>
              <a v-if="whatsappLink" :href="whatsappLink" target="_blank" rel="noopener noreferrer" class="btn-secondary w-full">اطلبي عبر واتساب</a>
            </div>
            <a v-if="whatsappLink" :href="whatsappLink" target="_blank" rel="noopener noreferrer" class="btn-secondary mt-5 w-full md:hidden">اطلبي عبر واتساب</a>
          </template>
          <p v-else class="font-semibold text-ink">المنتج غير متاح للطلب حاليًا.</p>
          <p role="status" :class="{ 'mt-3': message }" class="text-sm font-semibold text-brand">{{ message }}</p>
          <NuxtLink to="/cart" class="mt-1 inline-flex min-h-11 items-center font-semibold text-brand underline underline-offset-4">عرض السلة</NuxtLink>
        </section>

        <ul class="mt-4 flex flex-wrap gap-x-5 gap-y-1 rounded-2xl bg-lavender/50 px-4 py-3 text-sm font-semibold text-ink">
          <li class="flex items-center gap-2"><span aria-hidden="true" class="text-gold">✦</span>الدفع عند الاستلام</li>
        </ul>

        <section v-if="paragraphs.length" aria-labelledby="description-heading" class="mt-6">
          <h2 id="description-heading" class="mb-2 text-lg font-bold text-ink">عن المنتج</h2>
          <p v-for="(paragraph, index) in paragraphs" :key="index" class="mb-3 leading-8 wrap-anywhere text-ink">{{ paragraph }}</p>
        </section>
      </div>

      <div v-if="currentVariant" class="fixed inset-x-0 bottom-0 z-30 flex items-center gap-4 border-t border-lavender bg-cream/95 px-4 pt-3 shadow-[0_-6px_20px_rgb(48_8_26/0.10)] backdrop-blur md:hidden"
        style="padding-bottom: calc(0.75rem + env(safe-area-inset-bottom))">
        <p class="price shrink-0 text-xl"><span class="sr-only">السعر </span><bdi>{{ currentVariant.price }}</bdi> <span class="text-sm">د.ل</span></p>
        <button type="button" :disabled="busy" class="btn-primary storefront-action flex-1" @click="addToCart">أضيفي للسلة</button>
      </div>
    </article>
  </div>
</template>

<style scoped>
.skeleton { background: var(--color-lavender); opacity: 0.7; }
@media (prefers-reduced-motion: no-preference) {
  .skeleton { animation: skeleton-pulse 1.4s ease-in-out infinite; }
  .gallery-image-selected { animation: gallery-image-fade 180ms ease-out; }
  @keyframes skeleton-pulse { 50% { opacity: 0.35; } }
  @keyframes gallery-image-fade { from { opacity: 0; } to { opacity: 1; } }
}
</style>
