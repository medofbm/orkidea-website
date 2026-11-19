<script setup lang="ts">
import type { CataloguePage } from '~/types/catalogue'
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
const steps = ['اختار منتجك', 'أضفه للسلة', 'نوصلك الطلب']
useSeoMeta({
  title: 'أوركيديا | زيوت ومنتجات طبيعية',
  description: 'أوركيديا للزيوت والمنتجات الطبيعية. اختار منتجك واطلب بسهولة والدفع عند الاستلام.'
})
</script>

<template>
  <div class="overflow-x-clip">
    <!-- Hero -->
    <section aria-labelledby="store-heading" class="grid gap-8 py-6 sm:py-10 lg:grid-cols-2 lg:items-center lg:gap-14 lg:py-14">
      <div class="order-2 min-w-0 text-center lg:order-1 lg:text-start">
        <p class="text-sm font-semibold text-brand">للزيوت والمنتجات الطبيعية</p>
        <h1 id="store-heading" class="mt-3 text-3xl leading-[1.35] font-bold text-ink sm:text-4xl lg:text-5xl">
          زيوت وصابون طبيعي، حلو في العين وسهل تطلبه
        </h1>
        <p class="mx-auto mt-4 max-w-md text-base text-ink-soft lg:mx-0">أوركيديا للزيوت والمنتجات الطبيعية.</p>
        <a href="#catalogue-heading" class="btn-primary mt-7 w-full sm:w-auto sm:min-w-48">تسوّق الحين</a>
      </div>
      <div class="order-1 lg:order-2">
        <img src="/brand/hero-oils.webp" width="1122" height="1402" fetchpriority="high" decoding="async"
          alt="عبوتا زيت أوركيديا بجانب بعض"
          class="mx-auto aspect-[4/3] w-full max-w-md rounded-[var(--radius-card)] object-cover object-[50%_60%] shadow-[var(--shadow-soft)] sm:aspect-[4/5] lg:max-w-lg">
      </div>
    </section>

    <!-- Catalogue -->
    <section class="mt-6 sm:mt-10" aria-labelledby="catalogue-heading">
      <div class="mb-4 flex flex-wrap items-end justify-between gap-2">
        <h2 id="catalogue-heading" tabindex="-1" class="scroll-mt-4 text-2xl font-bold text-ink sm:text-3xl">منتجات أوركيديا</h2>
        <p v-if="data?.items.length && !error" class="text-sm text-ink-soft">عدد المنتجات <bdi class="font-bold">{{ data.total }}</bdi></p>
      </div>
      <div id="categories" tabindex="-1" class="scroll-mt-4">
        <nav v-if="data?.categories?.length" aria-label="فئات المنتجات"
          class="-mx-4 mb-5 flex gap-2 overflow-x-auto px-4 pb-2 sm:mx-0 sm:flex-wrap sm:overflow-visible sm:px-0">
          <NuxtLink :to="{ query: {}, hash: '#catalogue-heading' }"
            :aria-current="categoryId === undefined ? 'page' : undefined"
            class="chip shrink-0 whitespace-nowrap aria-[current=page]:bg-brand aria-[current=page]:text-white">كل المنتجات</NuxtLink>
          <NuxtLink v-for="category in data.categories" :key="category.id"
            :to="{ query: { categoryId: category.id }, hash: '#catalogue-heading' }"
            :aria-current="categoryId === category.id ? 'page' : undefined"
            class="chip shrink-0 whitespace-nowrap aria-[current=page]:bg-brand aria-[current=page]:text-white">{{ category.name }}</NuxtLink>
        </nav>
      </div>

      <div v-if="status === 'pending'" role="status" aria-label="جاري تحميل المنتجات"
        class="grid grid-cols-2 gap-3 sm:gap-5 md:grid-cols-3 lg:grid-cols-4">
        <div v-for="n in 4" :key="n" class="card" aria-hidden="true">
          <div class="aspect-square bg-lavender motion-safe:animate-pulse"></div>
          <div class="space-y-2 p-3 sm:p-5">
            <div class="h-4 w-3/4 rounded bg-lavender motion-safe:animate-pulse"></div>
            <div class="h-4 w-1/3 rounded bg-lavender motion-safe:animate-pulse"></div>
          </div>
        </div>
      </div>
      <div v-else-if="error" role="alert" class="card p-5 text-center">
        <p class="font-semibold">تعذر تحميل المنتجات.</p>
        <button class="btn-primary mt-4" @click="refresh()">إعادة المحاولة</button>
      </div>
      <template v-else>
        <div v-if="!data?.items.length" class="rounded-[var(--radius-card)] bg-lavender p-6 text-center sm:p-10">
          <p class="font-semibold text-ink">
            {{ categoryId ? 'ما فيش منتجات ضمن الفئة المختارة حاليًا.' : 'المنتجات جاية قريب، ارجع لنا بعد شوية 🌿' }}
          </p>
          <NuxtLink v-if="categoryId" :to="{ query: {}, hash: '#catalogue-heading' }" class="btn-primary mt-4">
            عرض كل المنتجات
          </NuxtLink>
        </div>
        <div v-else class="grid grid-cols-2 gap-3 sm:gap-5 md:grid-cols-3 lg:grid-cols-4">
          <ProductCard v-for="product in products" :key="product.id" :product="product" />
        </div>
        <nav v-if="data && data.total > data.pageSize" class="mt-8 flex flex-wrap items-center justify-between gap-3 border-t border-lavender-deep/50 pt-5" aria-label="صفحات المنتجات">
          <NuxtLink v-if="page > 1" :to="{ query: { page: page - 1, categoryId }, hash: '#catalogue-heading' }" class="btn-secondary">السابق</NuxtLink>
          <p aria-live="polite" aria-atomic="true" class="text-sm text-ink-soft">
            صفحة <bdi class="font-bold">{{ page }}</bdi> من <bdi class="font-bold">{{ Math.ceil(data.total / data.pageSize) }}</bdi>
          </p>
          <NuxtLink v-if="page * data.pageSize < data.total" :to="{ query: { page: page + 1, categoryId }, hash: '#catalogue-heading' }" class="btn-secondary">التالي</NuxtLink>
        </nav>
      </template>
    </section>

    <!-- How to order -->
    <section id="how-to-order" tabindex="-1" aria-labelledby="order-heading" class="mt-14 scroll-mt-4 rounded-[var(--radius-card)] bg-lavender px-5 py-8 sm:px-10 sm:py-12">
      <h2 id="order-heading" class="text-center text-2xl font-bold text-ink sm:text-3xl">كيف تطلب</h2>
      <ol class="mt-6 grid gap-4 sm:grid-cols-3 sm:gap-6">
        <li v-for="(step, i) in steps" :key="step" class="flex items-center gap-4 rounded-[var(--radius-card)] bg-white p-4 sm:flex-col sm:p-6 sm:text-center">
          <span class="grid size-11 shrink-0 place-items-center rounded-full bg-brand text-lg font-bold text-white" aria-hidden="true">{{ i + 1 }}</span>
          <span class="font-bold text-ink">{{ step }}</span>
        </li>
      </ol>
      <p class="mt-5 text-center text-sm font-semibold text-brand">الدفع عند الاستلام</p>
    </section>

    <!-- Lifestyle band -->
    <section id="about-orkidea" tabindex="-1" aria-labelledby="about-heading"
      class="mt-10 scroll-mt-4 overflow-hidden rounded-[var(--radius-card)] bg-plum text-white">
      <div class="grid items-center lg:grid-cols-2">
        <img src="/brand/blackseed-oil.webp" loading="lazy" decoding="async" width="1122" height="1402"
          alt="زيت الحبة السوداء من أوركيديا"
          class="aspect-[4/3] w-full object-cover object-center lg:aspect-auto lg:h-full lg:max-h-[28rem]">
        <div class="px-6 py-8 sm:px-10 lg:py-12">
          <h2 id="about-heading" class="text-2xl font-bold sm:text-3xl">أوركيديا</h2>
          <p class="mt-3 max-w-md text-lavender">زيت الحبة السوداء، زيت السمسم، زيت جوز الهند وصابون الحبة السوداء. شوف المنتجات واختار اللي يعجبك.</p>
          <a href="#catalogue-heading" class="mt-6 inline-flex min-h-12 items-center justify-center rounded-full bg-white px-6 font-bold text-brand hover:bg-lavender">شوف المنتجات</a>
        </div>
      </div>
    </section>
  </div>
</template>
