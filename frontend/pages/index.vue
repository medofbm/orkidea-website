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
// Page and category live in the URL; picking another category goes back to page 1.
function catalogueQuery(extra: { page?: number, categoryId?: number } = {}) {
  const c = 'categoryId' in extra ? extra.categoryId : categoryId.value
  const query: Record<string, string | number> = {}
  if (extra.page) query.page = extra.page
  if (c) query.categoryId = c
  return query
}
const products = computed(() => (data.value?.items ?? []).map(product => ({
  ...product,
  startingPrice: product.variants.reduce<string | undefined>((lowest, variant) =>
    lowest === undefined || Number(variant.price) < Number(lowest) ? variant.price : lowest, undefined)
})))
const featured = computed(() => products.value.slice(0, 8))
const features = [
  { icon: 'leaf', title: '100% طبيعي', text: 'زيوت وصابون من مكونات طبيعية' },
  { icon: 'hand-heart', title: 'صنع يدوي', text: 'نجهز كل طلب بعناية' },
  { icon: 'truck', title: 'توصيل لبابك', text: 'نوصلك طلبك لين البيت' },
  { icon: 'cash', title: 'الدفع عند الاستلام', text: 'ادفعي لما يوصلك طلبك' }
]
const stats = [
  { value: '100%', label: 'طبيعي' },
  { value: 'يدوي', label: 'صنع' },
  { value: '5', label: 'تقييم', icon: 'star-filled' }
]
// Steps follow the real flow: guest checkout (name, WhatsApp, delivery city), total shown before confirming, cash on delivery.
const steps = [
  { icon: 'bag', title: 'اختاري المنتج والحجم', text: 'تصفحي منتجاتنا واختاري الحجم اللي يناسبك.' },
  { icon: 'cart', title: 'أضيفيه للسلة', text: 'راجعي سلتك وعدّلي الكمية قبل ما تكملي.' },
  { icon: 'user', title: 'اكتبي بياناتك وأكدي', text: 'اسمك ورقم الواتساب ومدينة التوصيل، بدون حساب. تشوفي رسوم التوصيل والإجمالي قبل التأكيد.' },
  { icon: 'truck', title: 'نوصلك الطلب', text: 'نجهزو طلبك ونوصلوه لك، وتدفعي عند الاستلام.' }
] as const
// Category photos are matched by name; a category without a match keeps the flower tile.
function categoryImage(name: string) {
  if (/زيت|زيوت/.test(name)) return '/brand/cat-oils.webp'
  if (/صابون/.test(name)) return '/brand/cat-soap.webp'
  return undefined
}
// Small logo flowers drifting in the hero (decorative; frozen with reduced motion).
const petals = [
  { cls: 'top-8 end-[8%] size-8', delay: '0s' },
  { cls: 'top-1/2 start-[4%] size-6 max-sm:hidden', delay: '-2s' },
  { cls: 'bottom-10 start-[38%] size-7', delay: '-4s' },
  { cls: 'top-6 start-[45%] size-5 max-lg:hidden', delay: '-1s' }
]
const { whatsappNumber, facebookUrl, instagramUrl } = useRuntimeConfig().public
const whatsappHref = computed(() => {
  const digits = String(whatsappNumber ?? '').replace(/\D/g, '')
  return digits ? `https://wa.me/${digits}` : ''
})
const contactLinks = computed(() => [
  { key: 'whatsapp' as const, label: 'واتساب', hint: 'أسرع رد، اكتبي لنا وقت ما تحبي', color: '#25D366', href: whatsappHref.value },
  { key: 'instagram' as const, label: 'إنستغرام', hint: 'شوفي صور منتجاتنا وجديدنا', color: 'linear-gradient(135deg,#f58529,#dd2a7b 50%,#8134af)', href: instagramUrl ? String(instagramUrl) : '' },
  { key: 'facebook' as const, label: 'فيسبوك', hint: 'تابعينا وشوفي آراء الزبائن', color: '#1877f2', href: facebookUrl ? String(facebookUrl) : '' }
].filter(link => link.href))
// Visible reviews (site + imported Facebook comments) come only from the API, client-side.
type ApiReview = { id: number, message: string, rating: number, source: 'facebook' | 'site', name: string | null }
const { data: reviewsData } = useFetch<{ items: ApiReview[] }>('/api/reviews', { lazy: true, server: false })
const allTestimonials = computed(() => (reviewsData.value?.items ?? []).map(r => ({
  key: `${r.source}-${r.id}`, text: r.message, name: r.name,
  rating: r.source === 'site' ? r.rating : undefined, source: r.source
})))
const reviewDialog = ref<HTMLDialogElement | null>(null)
const reviewOpen = ref(false)
const reviewOpener = ref<HTMLElement | null>(null)
function openReview(event: Event) {
  reviewOpener.value = event.currentTarget as HTMLElement
  reviewOpen.value = true
  nextTick(() => {
    reviewDialog.value?.showModal()
    reviewDialog.value?.querySelector<HTMLElement>('input, textarea, button[autofocus]')?.focus()
  })
}
function closeReview() {
  if (reviewDialog.value?.open) reviewDialog.value.close()
}
function onReviewClosed() {
  reviewOpen.value = false
  reviewOpener.value?.focus()
}
const siteUrl = useRuntimeConfig().public.siteUrl
const homeDescription = 'أوركيديا للزيوت والمنتجات الطبيعية. منتجات طبيعية. اختاري منتجك واطلبي بسهولة والدفع عند الاستلام.'
useSeoMeta({
  title: 'أوركيديا | زيوت ومنتجات طبيعية',
  description: homeDescription,
  ogTitle: 'أوركيديا | زيوت ومنتجات طبيعية',
  ogDescription: homeDescription,
  ogUrl: siteUrl + '/',
  ogImage: siteUrl + '/brand/og-cover.webp'
})
useHead({ link: [{ rel: 'canonical', href: siteUrl + '/' }] })
</script>

<template>
  <div class="overflow-x-clip">
    <!-- Hero -->
    <section aria-labelledby="store-heading"
      class="relative grid gap-5 sm:mt-4 overflow-hidden rounded-[2rem] bg-gradient-hero px-5 pt-6 pb-16 sm:px-10 sm:pt-14 lg:grid-cols-2 lg:items-center lg:gap-14 lg:px-14 lg:pt-16 lg:pb-24">
      <span class="pointer-events-none absolute -top-16 -start-16 size-56 rounded-full bg-lilac/50 blur-3xl motion-safe:animate-float sm:size-72" aria-hidden="true"></span>
      <span class="pointer-events-none absolute -bottom-16 -end-10 size-56 rounded-full bg-blush/50 blur-3xl motion-safe:animate-float sm:size-72" aria-hidden="true"></span>
      <img v-for="petal in petals" :key="petal.cls" src="/brand/flower-purple.png" alt="" aria-hidden="true" width="40" height="40" loading="lazy"
        class="petal pointer-events-none absolute opacity-25" :class="petal.cls" :style="{ animationDelay: petal.delay }">
      <div class="relative order-2 min-w-0 text-center motion-safe:animate-fade-in-up lg:order-1 lg:text-start">
        <p class="inline-flex items-center rounded-full border border-lavender-deep bg-white/70 px-4 py-1 text-sm font-semibold text-brand">100% طبيعي • مصنوع يدويًا</p>
        <h1 id="store-heading" class="mt-4 text-3xl leading-[1.4] font-bold text-ink sm:text-4xl lg:text-5xl">
          اكتشفي <span class="text-gradient-brand text-shine">جمال الطبيعة</span> مع أوركيديا
        </h1>
        <p class="mx-auto mt-4 max-w-md text-base text-ink-soft lg:mx-0">زيوت وصابون طبيعي من ليبيا. اختاري منتجك واطلبيه بسهولة.</p>
        <div class="mt-7 flex flex-col gap-3 sm:flex-row sm:justify-center lg:justify-start">
          <a href="#catalogue-heading" class="btn-primary sm:min-w-44">تسوقي الآن</a>
          <a href="#contact" class="btn-secondary sm:min-w-44">تواصلي معنا</a>
        </div>
        <StatsRow :items="stats" class="mt-8 max-w-md" />
      </div>
      <div class="relative order-1 flex justify-center lg:order-2">
        <div class="relative w-44 min-[400px]:w-52 sm:w-72 lg:w-full lg:max-w-sm motion-safe:animate-fade-in-up">
          <div class="overflow-hidden rounded-t-full rounded-b-[2rem] bg-white/60 p-2 shadow-[var(--shadow-soft)] ring-1 ring-white/80">
            <img src="/brand/hero-oils.webp" srcset="/brand/hero-oils-640.webp 640w, /brand/hero-oils.webp 1122w"
              sizes="(min-width: 1024px) 384px, (min-width: 640px) 288px, 208px"
              alt="زيت الحبة السوداء وزيت السمسم من أوركيديا، معصوران على البارد" width="1122" height="1402"
              decoding="async" fetchpriority="high" class="aspect-[4/5] w-full rounded-t-full rounded-b-[1.6rem] object-cover">
          </div>
          <div class="absolute -bottom-4 -start-4 flex items-center gap-2 rounded-2xl bg-white px-3 py-2 shadow-[var(--shadow-soft)] motion-safe:animate-float sm:-start-8">
            <span class="grid size-9 place-items-center rounded-full bg-gradient-brand"><img src="/brand/flower-white.png" alt="" width="20" height="20" class="size-5"></span>
            <span class="text-start text-sm leading-tight"><span class="block font-bold text-ink">معصور على البارد</span><span class="text-ink-soft">مكوّن واحد طبيعي</span></span>
          </div>
        </div>
      </div>
    </section>

    <FeaturesBar v-reveal :items="features" class="-mt-8 lg:mt-0" />

    <!-- Categories -->
    <section id="categories" tabindex="-1" aria-labelledby="categories-heading" class="mt-12 scroll-mt-4 sm:mt-16">
      <SectionHeader v-reveal id="categories-heading" title="تسوقي" highlight="حسب الفئة" eyebrow="الفئات" />
      <div class="mx-auto mt-6 grid max-w-3xl grid-cols-2 justify-center gap-3 sm:flex sm:flex-wrap sm:justify-center sm:gap-5 sm:*:w-56 lg:*:w-64">
        <CategoryCard v-for="(category, i) in data?.categories ?? []" :key="category.id" v-reveal.zoom="i * 80" :name="category.name"
          :to="categoryId === category.id ? '/#catalogue-heading' : `/?categoryId=${category.id}#catalogue-heading`"
          :image="categoryImage(category.name)" :active="categoryId === category.id" />
      </div>
    </section>

    <!-- Featured -->
    <section v-if="featured.length >= 4" aria-labelledby="featured-heading" class="mt-12 sm:mt-16">
      <SectionHeader v-reveal id="featured-heading" title="منتجات" highlight="مختارة" eyebrow="الأكثر طلبًا" />
      <AppCarousel class="mt-6" :items="featured" label="منتجات مختارة" :per-view="{ base: 1, sm: 2, lg: 3, xl: 4 }"
        :autoplay="5000" dots arrows>
        <template #item="{ item }"><ProductCard :product="item" /></template>
      </AppCarousel>
    </section>

    <!-- Catalogue -->
    <section class="mt-12 sm:mt-16" aria-labelledby="catalogue-heading">
      <h2 id="catalogue-heading" tabindex="-1" class="mb-4 scroll-mt-4 text-2xl font-bold text-ink sm:text-3xl">كل منتجاتنا</h2>

      <p v-if="data?.items.length && !error" class="mb-5 text-sm text-ink-soft">عدد المنتجات <bdi class="font-bold text-brand">{{ data.total }}</bdi></p>

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
          <NuxtLink v-if="categoryId" :to="{ query: catalogueQuery({ categoryId: undefined }), hash: '#catalogue-heading' }" class="btn-primary mt-4">
            عرض كل المنتجات
          </NuxtLink>
        </div>
        <div v-else :key="`${categoryId ?? 0}-${page}`" class="grid grid-cols-2 gap-3 sm:gap-5 md:grid-cols-3 lg:grid-cols-4">
          <ProductCard v-for="product in products" :key="product.id" :product="product" />
        </div>
        <nav v-if="data && data.total > data.pageSize" class="mt-8 flex flex-wrap items-center justify-between gap-3 border-t border-lavender-deep/50 pt-5" aria-label="صفحات المنتجات">
          <NuxtLink v-if="page > 1" :to="{ query: catalogueQuery({ page: page - 1 }), hash: '#catalogue-heading' }" class="btn-secondary">السابق</NuxtLink>
          <p aria-live="polite" aria-atomic="true" class="text-sm text-ink-soft">
            صفحة <bdi class="font-bold">{{ page }}</bdi> من <bdi class="font-bold">{{ Math.ceil(data.total / data.pageSize) }}</bdi>
          </p>
          <NuxtLink v-if="page * data.pageSize < data.total" :to="{ query: catalogueQuery({ page: page + 1 }), hash: '#catalogue-heading' }" class="btn-secondary">التالي</NuxtLink>
        </nav>
      </template>
    </section>

    <!-- How to order -->
    <section id="how-to-order" tabindex="-1" aria-labelledby="order-heading" class="mt-14 scroll-mt-4 rounded-[var(--radius-card)] bg-lavender px-5 py-8 sm:px-10 sm:py-12">
      <SectionHeader v-reveal id="order-heading" title="كيف" highlight="تطلبي؟" eyebrow="أربع خطوات بسيطة" subtitle="تطلبي كضيفة بدون إنشاء حساب" />
      <ol class="mx-auto mt-8 grid max-w-md gap-0 lg:max-w-none lg:grid-cols-4 lg:gap-6">
        <li v-for="(step, i) in steps" :key="step.title" v-reveal="i * 120"
          class="relative flex gap-4 pb-6 last:pb-0 lg:flex-col lg:items-center lg:pb-0 lg:text-center">
          <span v-if="i < steps.length - 1" class="absolute start-[1.3rem] top-12 bottom-0 w-0.5 rounded-full bg-brand/25 lg:start-1/2 lg:top-[1.3rem] lg:bottom-auto lg:h-0.5 lg:w-[calc(100%+1.5rem)]" aria-hidden="true"></span>
          <span class="relative z-10 grid size-11 shrink-0 place-items-center rounded-full bg-gradient-brand text-white shadow-soft" aria-hidden="true">
            <AppIcon :name="step.icon" :size="22" />
            <span class="absolute -top-1.5 -end-1.5 grid size-5 place-items-center rounded-full bg-white text-xs font-bold text-brand shadow-soft">{{ i + 1 }}</span>
          </span>
          <div class="min-w-0 flex-1 rounded-[var(--radius-card)] bg-white p-4 shadow-soft lg:w-full lg:flex-none">
            <h3 class="font-bold text-ink"><span class="sr-only">الخطوة {{ i + 1 }}: </span>{{ step.title }}</h3>
            <p class="mt-1 text-sm leading-relaxed text-ink-soft">{{ step.text }}</p>
          </div>
        </li>
      </ol>
      <div class="mx-auto mt-8 flex max-w-xl items-center gap-3 rounded-[var(--radius-card)] border border-brand/30 bg-white p-4 shadow-soft">
        <span class="grid size-10 shrink-0 place-items-center rounded-full bg-brand/10 text-brand" aria-hidden="true"><AppIcon name="cash" :size="22" /></span>
        <p class="text-sm leading-relaxed text-ink"><strong class="text-brand">الدفع عند الاستلام.</strong> ما فيش دفع مسبق، تدفعي لما يوصلك طلبك.</p>
      </div>
      <div class="mt-6 flex justify-center">
        <a href="#catalogue-heading" class="btn-primary sm:min-w-52">ابدئي التسوق</a>
      </div>
    </section>

    <!-- Testimonials -->
    <section aria-labelledby="testimonials-heading" class="mt-14 rounded-[2rem] bg-gradient-band px-5 py-10 sm:px-10 sm:py-14">
      <SectionHeader v-reveal id="testimonials-heading" tone="light" title="ماذا يقول" highlight="زبائننا" />
      <AppCarousel v-if="allTestimonials.length" class="mt-8" :items="allTestimonials" label="آراء الزبائن" :per-view="{ base: 1, sm: 2, lg: 3 }"
        :autoplay="4500" loop dots dots-tone="light">
        <template #item="{ item }">
          <TestimonialCard :text="item.text" :source="item.source" :rating="item.rating" :name="item.name" />
        </template>
      </AppCarousel>
      <div class="mt-8 flex justify-center">
        <button type="button" class="btn-secondary border-white bg-white text-brand hover:bg-lavender" @click="openReview">شاركينا رأيك</button>
      </div>
      <dialog ref="reviewDialog" aria-labelledby="review-dialog-title" class="m-auto w-[min(92vw,30rem)] max-h-[90dvh] overflow-y-auto rounded-[var(--radius-card)] bg-white p-5 text-ink shadow-[var(--shadow-soft)] backdrop:bg-ink/60 sm:p-6"
        @close="onReviewClosed" @click.self="closeReview">
        <h3 id="review-dialog-title" class="mb-4 text-xl font-bold text-ink">شاركينا رأيك</h3>
        <ReviewForm v-if="reviewOpen" @done="closeReview" @cancel="closeReview" />
      </dialog>
    </section>

    <!-- About + Contact -->
    <section id="about-orkidea" tabindex="-1" aria-labelledby="about-heading" class="relative mt-14 scroll-mt-4 overflow-hidden rounded-[2rem] bg-gradient-brand px-5 pt-10 pb-16 text-white sm:px-10 sm:pt-14 sm:pb-20">
      <img src="/brand/flower-white.png" alt="" aria-hidden="true" width="320" height="320" loading="lazy" class="pointer-events-none absolute -bottom-20 -end-16 size-64 rotate-12 opacity-[0.08] sm:size-80">
      <span class="pointer-events-none absolute -top-20 -start-16 size-56 rounded-full bg-lilac/25 blur-3xl" aria-hidden="true"></span>
      <div class="relative mx-auto flex max-w-2xl flex-col items-center text-center">
        <span class="grid size-16 place-items-center rounded-full bg-white/15 ring-1 ring-white/30 backdrop-blur-sm">
          <img src="/brand/flower-white.png" alt="" width="40" height="40" class="size-10 object-contain motion-safe:animate-float">
        </span>
        <h2 v-reveal id="about-heading" class="mt-4 text-2xl font-bold sm:text-3xl">أوركيديا، الطبيعة بين إيديك</h2>
        <p class="mt-3 text-base leading-8 text-white/90">من ليبيا لبيتك: زيوت وصابون طبيعي نصنعوه بعناية، من مكونات نقية بدون تعقيد. تختاري منتجك، نوصلوه لك، وتدفعي عند الاستلام.</p>
        <ul class="mt-5 flex flex-wrap justify-center gap-2" aria-label="مزايا أوركيديا">
          <li class="inline-flex min-h-9 items-center gap-2 rounded-full bg-white/15 px-4 text-sm font-semibold ring-1 ring-white/25"><AppIcon name="leaf" :size="16" />طبيعي 100%</li>
          <li class="inline-flex min-h-9 items-center gap-2 rounded-full bg-white/15 px-4 text-sm font-semibold ring-1 ring-white/25"><AppIcon name="hand-heart" :size="16" />صنع يدوي</li>
          <li class="inline-flex min-h-9 items-center gap-2 rounded-full bg-white/15 px-4 text-sm font-semibold ring-1 ring-white/25"><AppIcon name="cash" :size="16" />الدفع عند الاستلام</li>
        </ul>
      </div>
    </section>

    <section id="contact" tabindex="-1" aria-labelledby="contact-heading" class="relative z-10 mx-auto -mt-8 mb-6 max-w-5xl scroll-mt-4 rounded-[var(--radius-card)] bg-lavender px-4 py-8 shadow-soft sm:px-8">
      <SectionHeader v-reveal id="contact-heading" title="هل لديكِ" highlight="استفسار؟" subtitle="تواصلي معانا وبنرد عليك في أقرب وقت" />
      <div class="mt-6 grid gap-6 lg:grid-cols-[1.2fr_1fr] lg:items-start">
        <ContactForm />
        <div class="flex flex-col gap-3">
          <p class="flex items-center gap-2 font-bold text-ink"><AppIcon name="sparkle" :size="18" class="text-brand" />أو تواصلي معانا مباشرة</p>
          <template v-if="contactLinks.length">
            <a v-for="link in contactLinks" :key="link.key" :href="link.href" target="_blank" rel="noopener noreferrer"
              class="contact-channel group flex min-h-16 items-center gap-4 rounded-[var(--radius-card)] bg-white p-3 shadow-soft outline-none ring-1 ring-brand/10 hover:ring-brand/40 focus-visible:ring-4 focus-visible:ring-brand/40">
              <span class="grid size-12 shrink-0 place-items-center rounded-full text-white" :style="{ background: link.color }" aria-hidden="true"><AppIcon :name="link.key" :size="24" /></span>
              <span class="min-w-0 flex-1">
                <span class="block font-bold text-ink">{{ link.label }}</span>
                <span class="block text-sm text-ink-soft">{{ link.hint }}</span>
              </span>
              <AppIcon name="chevron-left" :size="20" class="shrink-0 text-brand motion-safe:transition-transform motion-safe:group-hover:-translate-x-1" aria-hidden="true" />
            </a>
          </template>
          <p v-else class="rounded-[var(--radius-card)] bg-white p-4 text-ink-soft">قنوات التواصل ستتوفر قريبًا.</p>
          <p class="flex items-start gap-2 text-sm text-ink-soft"><AppIcon name="truck" :size="18" class="mt-0.5 shrink-0 text-brand" />توصيل لكل ليبيا والدفع عند الاستلام.</p>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
@media (prefers-reduced-motion: no-preference) {
  .contact-channel { transition: transform 250ms ease-out, box-shadow 250ms ease-out; }
  .contact-channel:hover, .contact-channel:focus-visible { transform: translateY(-2px); box-shadow: var(--shadow-lift); }
}
</style>
