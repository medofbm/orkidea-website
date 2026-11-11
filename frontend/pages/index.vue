<script setup lang="ts">
import type { CataloguePage } from '~/types/catalogue'
const brandImage = '/images/logo.jpg'
const route = useRoute()
function positiveInteger(value: unknown, max: number) {
  if (typeof value !== 'string' || !/^[0-9]+$/.test(value)) return undefined
  const number = Number(value)
  return Number.isSafeInteger(number) && number >= 1 && number <= max ? number : undefined
}
const page = computed(() => positiveInteger(route.query.page, 10000) ?? 1)
const categoryId = computed(() => positiveInteger(route.query.categoryId, 2147483647))
const { data, status, error, refresh } = await useFetch<CataloguePage>(
  () => useRuntimeConfig().public.apiBase + '/products',
  { query: { page, categoryId } }
)
const products = computed(() => (data.value?.items ?? []).map(product => ({
  ...product,
  startingPrice: product.variants.reduce<string | undefined>((lowest, variant) =>
    lowest === undefined || Number(variant.price) < Number(lowest) ? variant.price : lowest, undefined)
})))
useSeoMeta({ title: 'منتجات أوركيديا', description: 'تصفّح منتجات أوركيديا والأحجام المتاحة.' })
</script>

<template>
  <section aria-labelledby="store-heading"
    class="relative isolate grid gap-5 overflow-hidden rounded-3xl border border-purple-200 bg-purple-50 px-5 py-6 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center sm:gap-10 sm:px-10 sm:py-10 lg:px-14 lg:py-12">
    <div class="pointer-events-none absolute inset-0 -z-10 bg-[radial-gradient(ellipse_at_left,#e9d5ff,transparent_66%)]" aria-hidden="true"></div>
    <div class="flex items-center gap-3 sm:col-start-1 sm:row-start-1">
      <img :src="brandImage" alt="" width="48" height="48" class="size-12 rounded-full ring-2 ring-white sm:hidden">
      <p class="text-sm font-bold text-purple-900">متجر أوركيديا</p>
    </div>
    <div class="min-w-0 sm:col-start-1 sm:row-start-2">
      <h1 id="store-heading" class="max-w-lg text-3xl leading-tight font-bold text-purple-950 sm:text-4xl lg:text-5xl">
        زيوت وصابون للعناية اليومية
      </h1>
      <p class="mt-3 max-w-md text-sm leading-relaxed text-purple-900 sm:text-base">
        تصفّحي المنتجات وشوفي الأحجام والأسعار قبل ما تختاري اللي يناسبك.
      </p>
      <a href="#catalogue-heading"
        class="mt-5 inline-flex min-h-11 items-center gap-3 rounded-full bg-purple-900 px-5 text-sm font-bold text-white hover:bg-purple-800">
        تصفّحي المنتجات
        <span aria-hidden="true">↓</span>
      </a>
    </div>
    <div class="hidden place-items-center rounded-full border border-white/80 p-4 sm:col-start-2 sm:row-span-2 sm:row-start-1 sm:grid lg:p-6" aria-hidden="true">
      <img :src="brandImage" alt="" width="208" height="208"
        class="size-36 rounded-full border-4 border-white shadow-lg shadow-purple-900/15 lg:size-52">
    </div>
  </section>
  <section class="mt-8 sm:mt-12" aria-labelledby="catalogue-heading">
    <div class="mb-4 flex flex-wrap items-end justify-between gap-2 sm:mb-6">
      <h2 id="catalogue-heading" tabindex="-1" class="scroll-mt-4 text-2xl font-bold text-purple-950 sm:text-3xl">منتجات أوركيديا</h2>
      <p v-if="data?.items.length && !error" class="text-sm text-purple-900">عدد المنتجات <bdi class="font-bold">{{ data.total }}</bdi></p>
    </div>
    <nav v-if="data?.categories?.length" aria-label="فئات المنتجات" class="mb-5 flex flex-wrap gap-2">
      <NuxtLink :to="{ query: {}, hash: '#catalogue-heading' }"
        :aria-current="categoryId === undefined ? 'page' : undefined"
        class="inline-flex min-h-11 items-center rounded-full border border-purple-200 px-4 text-sm font-semibold text-purple-950 aria-[current=page]:bg-purple-900 aria-[current=page]:text-white">كل المنتجات</NuxtLink>
      <NuxtLink v-for="category in data.categories" :key="category.id"
        :to="{ query: { categoryId: category.id }, hash: '#catalogue-heading' }"
        :aria-current="categoryId === category.id ? 'page' : undefined"
        class="inline-flex min-h-11 items-center rounded-full border border-purple-200 px-4 text-sm font-semibold text-purple-950 aria-[current=page]:bg-purple-900 aria-[current=page]:text-white">{{ category.name }}</NuxtLink>
    </nav>
    <p v-if="status === 'pending'" role="status">جاري تحميل المنتجات…</p>
    <div v-else-if="error" role="alert" class="rounded-xl border border-red-200 p-4">
      <p>تعذر تحميل المنتجات.</p>
      <button class="mt-3 min-h-11 rounded-lg bg-purple-800 px-4 text-white" @click="refresh()">إعادة المحاولة</button>
    </div>
    <template v-else>
      <p v-if="!data?.items.length">لا توجد منتجات لعرضها حاليًا.</p>
      <div class="grid grid-cols-2 gap-3 sm:gap-5 md:grid-cols-3 lg:grid-cols-4">
        <NuxtLink v-for="product in products" :key="product.id" :to="'/products/' + product.id"
          class="group flex min-w-0 flex-col rounded-2xl border border-purple-100 bg-white p-2 shadow-sm motion-safe:transition-shadow hover:border-purple-300 hover:shadow-md sm:p-3">
          <div class="grid aspect-square place-items-center overflow-hidden rounded-xl bg-purple-50">
            <img v-if="product.images[0] && product.images[0] !== brandImage"
              :src="product.images[0]" alt="" class="size-full object-contain" loading="lazy" width="320" height="320">
            <div v-else class="grid place-items-center gap-2 p-2 text-center text-xs text-purple-900">
              <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" class="size-8">
                <rect x="3" y="4" width="18" height="16" rx="3" />
                <path d="m3 16 5-5 4 4 3-3 6 6M15 8h.01" stroke-linecap="round" stroke-linejoin="round" />
              </svg>
              <span>صورة غير متاحة</span>
            </div>
          </div>
          <div class="flex flex-1 flex-col px-1 pt-3 pb-1">
            <p v-if="product.category" class="mb-1 text-xs wrap-anywhere text-purple-800">{{ product.category }}</p>
            <h3 class="text-sm leading-relaxed font-bold wrap-anywhere text-purple-950 sm:text-base">{{ product.name }}</h3>
            <p v-if="product.startingPrice !== undefined" class="mt-1 pb-3 text-sm text-purple-900">
              <span v-if="product.variants.length > 1">من </span><bdi class="font-bold">{{ product.startingPrice }}</bdi> د.ل
            </p>
            <span aria-hidden="true" class="mt-auto flex min-h-9 items-center justify-between gap-1 border-t border-purple-100 pt-1 text-xs font-bold text-purple-900 sm:text-sm">
              عرض التفاصيل <span class="group-hover:text-purple-700">←</span>
            </span>
          </div>
        </NuxtLink>
      </div>
      <nav v-if="data && data.total > data.pageSize" class="mt-8 flex justify-between" aria-label="صفحات المنتجات">
        <NuxtLink v-if="page > 1" :to="{ query: { page: page - 1, categoryId }, hash: '#catalogue-heading' }">السابق</NuxtLink>
        <NuxtLink v-if="page * data.pageSize < data.total" :to="{ query: { page: page + 1, categoryId }, hash: '#catalogue-heading' }">التالي</NuxtLink>
      </nav>
    </template>
  </section>
</template>
