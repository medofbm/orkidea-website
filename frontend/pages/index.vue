<script setup lang="ts">
import type { CataloguePage } from '~/types/catalogue'
import { testimonials } from '~/data/testimonials'
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
const featured = computed(() => products.value.slice(0, 8))
const features = [
  { icon: 'leaf', title: '100% طبيعي', text: 'زيوت وصابون من مكونات طبيعية وعضوية' },
  { icon: 'hand-heart', title: 'صنع يدوي', text: 'نجهز كل طلب بعناية' },
  { icon: 'truck', title: 'توصيل لبابك', text: 'نوصلك طلبك لين البيت' },
  { icon: 'cash', title: 'الدفع عند الاستلام', text: 'ادفعي لما يوصلك طلبك' }
]
const stats = [
  { value: '100%', label: 'طبيعي' },
  { value: 'يدوي', label: 'صنع' },
  { value: '5', label: 'تقييم', icon: 'star-filled' }
]
const gridRef = ref<HTMLElement | null>(null)
useReveal(gridRef, { selector: '.reveal', stagger: 60 })
const steps = ['اختاري منتجك', 'أضيفيه للسلة', 'نوصلك الطلب']
const { whatsappNumber, facebookUrl, instagramUrl } = useRuntimeConfig().public
const whatsappHref = computed(() => {
  const digits = String(whatsappNumber ?? '').replace(/\D/g, '')
  return digits ? `https://wa.me/${digits}` : ''
})
const contactLinks = computed(() => [
  { label: 'واتساب', href: whatsappHref.value },
  { label: 'فيسبوك', href: facebookUrl ? String(facebookUrl) : '' },
  { label: 'إنستغرام', href: instagramUrl ? String(instagramUrl) : '' }
].filter(link => link.href))
useSeoMeta({
  title: 'أوركيديا | زيوت ومنتجات طبيعية',
  description: 'أوركيديا للزيوت والمنتجات الطبيعية. منتجات طبيعية وعضوية. اختاري منتجك واطلبي بسهولة والدفع عند الاستلام.'
})
</script>

<template>
  <div class="overflow-x-clip">
    <!-- Hero -->
    <section aria-labelledby="store-heading"
      class="relative mt-4 grid gap-8 overflow-hidden rounded-[2rem] bg-gradient-hero px-5 pt-10 pb-16 sm:px-10 sm:pt-14 lg:grid-cols-2 lg:items-center lg:gap-14 lg:px-14 lg:pt-16 lg:pb-24">
      <span class="pointer-events-none absolute -top-16 -start-16 size-56 rounded-full bg-lilac/50 blur-3xl motion-safe:animate-float sm:size-72" aria-hidden="true"></span>
      <span class="pointer-events-none absolute -bottom-16 -end-10 size-56 rounded-full bg-blush/50 blur-3xl motion-safe:animate-float sm:size-72" aria-hidden="true"></span>
      <div class="relative order-2 min-w-0 text-center lg:order-1 lg:text-start">
        <p class="inline-flex items-center rounded-full border border-lavender-deep bg-white/70 px-4 py-1 text-sm font-semibold text-brand">100% طبيعي • مصنوع يدويًا</p>
        <h1 id="store-heading" class="mt-4 text-3xl leading-[1.4] font-bold text-ink sm:text-4xl lg:text-5xl">
          اكتشفي <span class="text-gradient-brand">جمال الطبيعة</span> مع أوركيديا
        </h1>
        <p class="mx-auto mt-4 max-w-md text-base text-ink-soft lg:mx-0">زيوت وصابون طبيعي من ليبيا. اختاري منتجك واطلبيه بسهولة.</p>
        <div class="mt-7 flex flex-col gap-3 sm:flex-row sm:justify-center lg:justify-start">
          <a href="#catalogue-heading" class="btn-primary sm:min-w-44">تسوقي الآن</a>
          <a href="#contact" class="btn-secondary sm:min-w-44">تواصلي معنا</a>
        </div>
        <StatsRow :items="stats" class="mt-8 max-w-md" />
      </div>
      <div class="relative order-1 flex justify-center lg:order-2">
        <div class="grid size-56 place-items-center rounded-full bg-white/60 p-3 shadow-[var(--shadow-soft)] ring-8 ring-white/70 motion-safe:animate-float sm:size-72 lg:size-96">
          <div class="grid size-full place-items-center rounded-full bg-gradient-brand">
            <img src="/brand/flower-white.png" alt="شعار أوركيديا" width="160" height="160" decoding="async" fetchpriority="high" class="h-auto w-1/2">
          </div>
        </div>
      </div>
    </section>

    <FeaturesBar :items="features" class="-mt-8 lg:mt-0" />

    <!-- Categories -->
    <section id="categories" tabindex="-1" aria-labelledby="categories-heading" class="mt-12 scroll-mt-4 sm:mt-16">
      <SectionHeader id="categories-heading" title="تسوقي" highlight="حسب الفئة" eyebrow="الفئات" />
      <div class="mt-6 grid grid-cols-2 gap-3 sm:gap-5 md:grid-cols-3 lg:grid-cols-4">
        <CategoryCard name="كل المنتجات" to="/#catalogue-heading" />
        <CategoryCard v-for="category in data?.categories ?? []" :key="category.id" :name="category.name"
          :to="`/?categoryId=${category.id}#catalogue-heading`" />
      </div>
      <nav v-if="data?.categories?.length" aria-label="فئات المنتجات"
        class="-mx-4 mt-5 flex gap-2 overflow-x-auto px-4 pb-2 sm:mx-0 sm:flex-wrap sm:justify-center sm:overflow-visible sm:px-0">
        <NuxtLink :to="{ query: {}, hash: '#catalogue-heading' }"
          :aria-current="categoryId === undefined ? 'page' : undefined"
          class="chip shrink-0 whitespace-nowrap aria-[current=page]:bg-brand aria-[current=page]:text-white">كل المنتجات</NuxtLink>
        <NuxtLink v-for="category in data.categories" :key="category.id"
          :to="{ query: { categoryId: category.id }, hash: '#catalogue-heading' }"
          :aria-current="categoryId === category.id ? 'page' : undefined"
          class="chip shrink-0 whitespace-nowrap aria-[current=page]:bg-brand aria-[current=page]:text-white">{{ category.name }}</NuxtLink>
      </nav>
    </section>

    <!-- Featured -->
    <section v-if="featured.length >= 4" aria-labelledby="featured-heading" class="mt-12 sm:mt-16">
      <SectionHeader id="featured-heading" title="منتجات" highlight="مختارة" eyebrow="الأكثر طلبًا" />
      <AppCarousel class="mt-6" :items="featured" label="منتجات مختارة" :per-view="{ base: 1, sm: 2, lg: 3, xl: 4 }"
        :autoplay="5000" dots arrows>
        <template #item="{ item }"><ProductCard :product="item" /></template>
      </AppCarousel>
    </section>

    <!-- Catalogue -->
    <section class="mt-12 sm:mt-16" aria-labelledby="catalogue-heading">
      <div class="mb-4 flex flex-wrap items-end justify-between gap-2">
        <h2 id="catalogue-heading" tabindex="-1" class="scroll-mt-4 text-2xl font-bold text-ink sm:text-3xl">كل منتجاتنا</h2>
        <p v-if="data?.items.length && !error" class="text-sm text-ink-soft">عدد المنتجات <bdi class="font-bold">{{ data.total }}</bdi></p>
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
            {{ categoryId ? 'لا توجد منتجات ضمن الفئة المختارة حاليًا.' : 'المنتجات قريبًا، راجعينا بعد قليل 🌿' }}
          </p>
          <NuxtLink v-if="categoryId" :to="{ query: {}, hash: '#catalogue-heading' }" class="btn-primary mt-4">
            عرض كل المنتجات
          </NuxtLink>
        </div>
        <div v-else ref="gridRef" class="grid grid-cols-2 gap-3 sm:gap-5 md:grid-cols-3 lg:grid-cols-4">
          <ProductCard v-for="product in products" :key="product.id" :product="product" class="reveal" />
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
      <h2 id="order-heading" class="text-center text-2xl font-bold text-ink sm:text-3xl">كيف تطلبي؟</h2>
      <ol class="mt-6 grid gap-4 sm:grid-cols-3 sm:gap-6">
        <li v-for="(step, i) in steps" :key="step" class="flex items-center gap-4 rounded-[var(--radius-card)] bg-white p-4 sm:flex-col sm:p-6 sm:text-center">
          <span class="grid size-11 shrink-0 place-items-center rounded-full bg-brand text-lg font-bold text-white" aria-hidden="true">{{ i + 1 }}</span>
          <span class="font-bold text-ink">{{ step }}</span>
        </li>
      </ol>
      <p class="mt-5 text-center text-sm font-semibold text-brand">الدفع عند الاستلام</p>
    </section>

    <!-- Testimonials -->
    <section aria-labelledby="testimonials-heading" class="mt-14 rounded-[2rem] bg-gradient-band px-5 py-10 sm:px-10 sm:py-14">
      <SectionHeader id="testimonials-heading" tone="light" title="ماذا يقول" highlight="زبائننا" />
      <AppCarousel class="mt-8" :items="testimonials" label="آراء الزبائن" :per-view="{ base: 1, sm: 2, lg: 3 }"
        :autoplay="4500" loop dots dots-tone="light">
        <template #item="{ item }">
          <TestimonialCard :text="item.text" :product="item.product" :source="item.source" />
        </template>
      </AppCarousel>
    </section>

    <!-- About -->
    <section id="about-orkidea" tabindex="-1" aria-labelledby="about-heading" class="mt-10 scroll-mt-4 px-1 text-center">
      <h2 id="about-heading" class="text-2xl font-bold text-ink sm:text-3xl">أوركيديا</h2>
      <p class="mx-auto mt-3 max-w-xl text-ink-soft">أوركيديا متجر ليبي للزيوت والصابون الطبيعية. منتجاتنا طبيعية وعضوية.</p>
    </section>

    <!-- Contact -->
    <section id="contact" tabindex="-1" aria-labelledby="contact-heading" class="mt-10 mb-6 scroll-mt-4 rounded-[var(--radius-card)] bg-lavender px-5 py-8 text-center sm:px-10">
      <SectionHeader id="contact-heading" title="هل لديكِ" highlight="استفسار؟" subtitle="تواصلي معانا وبنرد عليك في أقرب وقت" />
      <div v-if="contactLinks.length" class="mt-5 flex flex-col justify-center gap-3 sm:flex-row">
        <a v-for="link in contactLinks" :key="link.label" :href="link.href" target="_blank" rel="noopener noreferrer" class="btn-secondary bg-white sm:min-w-40">{{ link.label }}</a>
      </div>
      <p v-else class="mt-3 text-ink-soft">قنوات التواصل ستتوفر قريبًا.</p>
    </section>
  </div>
</template>
