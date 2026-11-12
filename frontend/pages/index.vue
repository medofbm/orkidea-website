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
        class="storefront-action mt-5 inline-flex min-h-11 items-center gap-3 rounded-full bg-purple-900 px-5 text-sm font-bold text-white hover:bg-purple-800">
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
    <div id="categories" tabindex="-1" class="scroll-mt-4">
    <nav v-if="data?.categories?.length" aria-label="فئات المنتجات" class="mb-5 flex flex-wrap gap-2">
      <NuxtLink :to="{ query: {}, hash: '#catalogue-heading' }"
        :aria-current="categoryId === undefined ? 'page' : undefined"
        class="inline-flex min-h-11 items-center rounded-full border border-purple-200 px-4 text-sm font-semibold text-purple-950 aria-[current=page]:bg-purple-900 aria-[current=page]:text-white">كل المنتجات</NuxtLink>
      <NuxtLink v-for="category in data.categories" :key="category.id"
        :to="{ query: { categoryId: category.id }, hash: '#catalogue-heading' }"
        :aria-current="categoryId === category.id ? 'page' : undefined"
        class="inline-flex min-h-11 items-center rounded-full border border-purple-200 px-4 text-sm font-semibold text-purple-950 aria-[current=page]:bg-purple-900 aria-[current=page]:text-white">{{ category.name }}</NuxtLink>
    </nav>
    <p v-else-if="status !== 'pending' && !error" class="mb-5 text-sm text-purple-900">تصفّحي جميع المنتجات المتاحة أدناه.</p>
    </div>
    <p v-if="status === 'pending'" role="status">جاري تحميل المنتجات…</p>
    <div v-else-if="error" role="alert" class="rounded-xl border border-red-200 p-4">
      <p>تعذر تحميل المنتجات.</p>
      <button class="mt-3 min-h-11 rounded-lg bg-purple-800 px-4 text-white" @click="refresh()">إعادة المحاولة</button>
    </div>
    <template v-else>
      <div v-if="!data?.items.length" class="rounded-2xl border border-purple-100 bg-purple-50 p-5 text-center sm:p-8">
        <p class="font-semibold text-purple-950">
          {{ categoryId ? 'ما فيش منتجات ضمن الفئة المختارة حاليًا.' : 'لا توجد منتجات لعرضها حاليًا.' }}
        </p>
        <NuxtLink v-if="categoryId" :to="{ query: {}, hash: '#catalogue-heading' }"
          class="mt-4 inline-flex min-h-11 items-center justify-center rounded-full bg-purple-900 px-5 font-bold text-white hover:bg-purple-800 motion-safe:transition-colors motion-safe:duration-200">
          عرض كل المنتجات
        </NuxtLink>
      </div>
      <div class="grid grid-cols-2 gap-3 sm:gap-5 md:grid-cols-3 lg:grid-cols-4">
        <ProductCard v-for="product in products" :key="product.id" :product="product" />
      </div>
      <nav v-if="data && data.total > data.pageSize" class="mt-8 flex flex-wrap items-center justify-between gap-3 border-t border-purple-100 pt-5" aria-label="صفحات المنتجات">
        <NuxtLink v-if="page > 1" :to="{ query: { page: page - 1, categoryId }, hash: '#catalogue-heading' }"
          class="inline-flex min-h-11 items-center justify-center rounded-full border border-purple-200 bg-white px-5 font-semibold text-purple-900 hover:border-purple-400 hover:bg-purple-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-800 motion-safe:transition-colors motion-safe:duration-200">السابق</NuxtLink>
        <p aria-live="polite" aria-atomic="true" class="text-sm text-purple-900">
          صفحة <bdi class="font-bold">{{ page }}</bdi> من <bdi class="font-bold">{{ Math.ceil(data.total / data.pageSize) }}</bdi>
        </p>
        <NuxtLink v-if="page * data.pageSize < data.total" :to="{ query: { page: page + 1, categoryId }, hash: '#catalogue-heading' }"
          class="inline-flex min-h-11 items-center justify-center rounded-full border border-purple-200 bg-white px-5 font-semibold text-purple-900 hover:border-purple-400 hover:bg-purple-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-800 motion-safe:transition-colors motion-safe:duration-200">التالي</NuxtLink>
      </nav>
    </template>
  </section>
  <section id="how-to-order" tabindex="-1" aria-labelledby="order-heading" class="mt-10 scroll-mt-4 rounded-3xl bg-purple-50 p-5 sm:p-8">
    <h2 id="order-heading" class="text-2xl font-bold text-purple-950">كيف تطلبي؟</h2>
    <ol class="mt-4 grid gap-4 text-purple-900 sm:grid-cols-3">
      <li><strong>١. اختاري منتجاتك</strong><p class="mt-1 leading-relaxed">تصفّحي المنتجات واختاري الحجم المناسب وأضيفيه للسلة.</p></li>
      <li><strong>٢. راجعي السلة</strong><p class="mt-1 leading-relaxed">راجعي الكميات والأسعار قبل الانتقال لإتمام الطلب.</p></li>
      <li><strong>٣. أكملي الطلب</strong><p class="mt-1 leading-relaxed">أدخلي بيانات التوصيل وراجعي الخيارات والتكلفة والإجمالي قبل التأكيد.</p></li>
    </ol>
  </section>
  <section id="about-orkidea" tabindex="-1" aria-labelledby="about-heading"
    class="mt-10 scroll-mt-4 rounded-3xl border border-purple-100 bg-white p-5 sm:p-8">
    <h2 id="about-heading" class="text-2xl font-bold text-purple-950">عن أوركيديا</h2>
    <p class="mt-2 max-w-prose leading-relaxed text-purple-900">
      أوركيديا متجر ليبي للزيوت والصابون للعناية اليومية. اختاري المنتج والحجم، وشوفي تكلفة التوصيل والإجمالي قبل تأكيد الطلب.
    </p>
  </section>
</template>
