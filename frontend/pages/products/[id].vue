<script setup lang="ts">
import type { CatalogueProduct } from '~/types/catalogue'
const route = useRoute()
const { busy, message, mutate } = useCart()
const { data: product, status, error, refresh } = await useFetch<CatalogueProduct>(
  () => useRuntimeConfig().public.apiBase + '/products/' + route.params.id
)
const selectedImage = ref(0)
const selectedVariant = ref(0)
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
  selectedVariant.value = 0
  failedImages.value = []
})
const currentVariant = computed(() => product.value?.variants[selectedVariant.value])
useSeoMeta({ title: () => product.value?.name ?? 'المنتج', description: () => product.value?.description ?? '' })
</script>

<template>
  <NuxtLink to="/" class="mb-4 inline-flex min-h-11 items-center gap-2 rounded-lg px-2 font-semibold text-purple-900 hover:bg-purple-50">
    <span aria-hidden="true">→</span> كل المنتجات
  </NuxtLink>
  <p v-if="status === 'pending'" role="status">جاري تحميل المنتج…</p>
  <div v-else-if="error" role="alert" class="rounded-2xl border border-red-200 bg-white p-5">
    <p>{{ error.statusCode === 404 ? 'المنتج غير موجود.' : 'تعذر تحميل المنتج.' }}</p>
    <button v-if="error.statusCode !== 404" type="button" class="mt-3 min-h-11 rounded-lg bg-purple-800 px-4 text-white" @click="refresh()">إعادة المحاولة</button>
  </div>
  <article v-else-if="product" aria-labelledby="product-heading"
    class="grid gap-5 md:grid-cols-2 md:grid-rows-[auto_1fr] md:gap-x-10 lg:gap-x-14">
    <header class="min-w-0 md:col-start-2 md:row-start-1">
      <p v-if="product.category" class="mb-2 text-sm font-semibold wrap-anywhere text-purple-800">{{ product.category }}</p>
      <h1 id="product-heading" class="text-3xl leading-tight font-bold wrap-anywhere text-purple-950 lg:text-4xl">{{ product.name }}</h1>
    </header>
    <section aria-label="صور المنتج" class="min-w-0 md:col-start-1 md:row-span-2 md:row-start-1">
      <div class="relative grid aspect-square max-h-120 place-items-center overflow-hidden rounded-2xl border border-purple-100 bg-white shadow-sm">
        <img v-if="currentImage && !failedImages.includes(currentImage)" :key="currentImage"
          :src="currentImage" :alt="product.name" class="absolute inset-0 size-full object-contain"
          width="480" height="480" @vue:mounted="checkLoadedImage($event.el, currentImage)"
          @error="markFailedImage(currentImage)">
        <div v-else class="grid place-items-center gap-2 p-4 text-center text-sm text-purple-900">
          <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" class="size-10">
            <rect x="3" y="4" width="18" height="16" rx="3" />
            <path d="m3 16 5-5 4 4 3-3 6 6M15 8h.01" stroke-linecap="round" stroke-linejoin="round" />
          </svg>
          <span>صورة غير متاحة</span>
        </div>
      </div>
      <div v-if="images.length > 1" class="mt-3 flex flex-wrap gap-2" role="group" aria-label="اختيار صورة المنتج">
        <button v-for="(image, index) in images" :key="image" type="button"
          :aria-label="'عرض الصورة ' + (index + 1)" :aria-pressed="selectedImage === index"
          class="grid size-14 place-items-center overflow-hidden rounded-xl border-2 bg-white p-1 hover:border-purple-400 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-800 motion-safe:transition-colors motion-safe:duration-200 sm:size-16"
          :class="selectedImage === index ? 'border-purple-800 ring-2 ring-purple-200' : 'border-purple-100'"
          @click="selectedImage = index">
          <img v-if="!failedImages.includes(image)" :src="image" alt="" class="size-full object-contain"
            width="56" height="56" loading="lazy" @vue:mounted="checkLoadedImage($event.el, image)"
            @error="markFailedImage(image)">
          <span v-else aria-hidden="true" class="text-sm text-purple-900">{{ index + 1 }}</span>
        </button>
      </div>
    </section>
    <div class="min-w-0 md:col-start-2 md:row-start-2">
      <section aria-label="خيارات الشراء" class="rounded-2xl border border-purple-100 bg-white p-4 sm:p-6">
        <template v-if="currentVariant">
          <div aria-live="polite" aria-atomic="true" class="mb-5">
            <p class="mb-1 text-sm font-medium text-purple-800">السعر للحجم المختار: <bdi>{{ currentVariant.size }}</bdi></p>
            <p class="text-3xl font-bold text-purple-950">
              <bdi>{{ currentVariant.price }}</bdi> <span class="text-base">د.ل</span>
            </p>
          </div>
          <fieldset class="mb-5 min-w-0" :disabled="busy">
            <legend class="mb-2 text-sm font-semibold text-purple-950">اختاري الحجم</legend>
            <div class="flex flex-wrap gap-2">
              <label v-for="(variant, index) in product.variants" :key="variant.id"
                class="flex min-h-11 max-w-full items-center gap-2 rounded-xl border border-purple-200 px-3 py-2 text-sm wrap-anywhere hover:border-purple-400 motion-safe:transition-colors motion-safe:duration-200 has-checked:border-purple-800 has-checked:bg-purple-50 has-checked:font-semibold has-checked:ring-1 has-checked:ring-purple-800 has-focus-visible:outline-2 has-focus-visible:outline-offset-2 has-focus-visible:outline-purple-800">
                <input v-model="selectedVariant" type="radio" name="variant" :value="index" class="shrink-0 accent-purple-800">
                <span>{{ variant.size }}</span>
              </label>
            </div>
          </fieldset>
          <button type="button" :disabled="busy"
            class="storefront-action min-h-12 w-full rounded-xl bg-purple-900 px-5 py-3 font-bold text-white shadow-sm hover:bg-purple-800 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-800 motion-safe:transition-colors motion-safe:duration-200 disabled:opacity-60"
            @click="mutate('POST', currentVariant.id)">أضيفي للسلة</button>
        </template>
        <p v-else class="font-semibold text-purple-950">المنتج غير متاح للطلب حاليًا.</p>
        <p role="status" :class="{ 'mt-3': message }" class="text-sm font-medium text-purple-900">{{ message }}</p>
        <NuxtLink to="/cart" class="mt-2 inline-flex min-h-11 items-center font-semibold text-purple-900 underline">عرض السلة</NuxtLink>
      </section>
      <section v-if="product.description" aria-labelledby="description-heading" class="mt-6">
        <h2 id="description-heading" class="mb-2 font-bold text-purple-950">عن المنتج</h2>
        <p class="leading-8 wrap-anywhere whitespace-pre-line">{{ product.description }}</p>
      </section>
    </div>
  </article>
</template>
