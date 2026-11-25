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
const sortOptions = [
  { value: 'default', label: 'الافتراضي' },
  { value: 'newest', label: 'الأحدث' },
  { value: 'price-asc', label: 'السعر: من الأقل' },
  { value: 'price-desc', label: 'السعر: من الأعلى' },
  { value: 'name', label: 'الاسم' }
]
const search = computed(() => {
  const q = typeof route.query.q === 'string' ? route.query.q.trim().slice(0, 100) : ''
  return q || undefined
})
const sort = computed(() => {
  const s = typeof route.query.sort === 'string' ? route.query.sort : ''
  return sortOptions.some(o => o.value === s) && s !== 'default' ? s : undefined
})
const { data, status, error, refresh } = await useFetch<CataloguePage>(
  () => useRuntimeConfig().public.apiBase + '/products',
  { query: { page, categoryId, search, sort } }
)
// Toolbar state lives in the URL (q, sort) next to page and categoryId; changing q or sort resets page.
const searchText = ref(search.value ?? '')
watch(search, value => { if ((value ?? '') !== searchText.value.trim()) searchText.value = value ?? '' })
function catalogueQuery(extra: { page?: number, q?: string, sort?: string, categoryId?: number } = {}) {
  const q = 'q' in extra ? extra.q : search.value
  const s = 'sort' in extra ? extra.sort : sort.value
  const c = 'categoryId' in extra ? extra.categoryId : categoryId.value
  // Same order as before search existed: page (when a pagination link sets it), then categoryId, then q and sort.
  const query: Record<string, string | number> = {}
  if (extra.page) query.page = extra.page
  if (c) query.categoryId = c
  if (q) query.q = q
  if (s && s !== 'default') query.sort = s
  return query
}
function applySearch() {
  const q = searchText.value.trim().slice(0, 100)
  if (q === (search.value ?? '')) return
  navigateTo({ query: catalogueQuery({ q }), hash: '#catalogue-heading' }, { replace: true })
}
let searchTimer: ReturnType<typeof setTimeout> | undefined
function onSearchInput() {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(applySearch, 300)
}
function submitSearch() {
  clearTimeout(searchTimer)
  applySearch()
}
function clearSearch() {
  clearTimeout(searchTimer)
  searchText.value = ''
  applySearch()
}
function onSortChange(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  navigateTo({ query: catalogueQuery({ sort: value }), hash: '#catalogue-heading' }, { replace: true })
}
onBeforeUnmount(() => clearTimeout(searchTimer))
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
const gridRef = ref<HTMLElement | null>(null)
useReveal(gridRef, { selector: '.reveal', stagger: 60 })
const steps = ['اختاري منتجك', 'أضيفيه للسلة', 'نوصلك الطلب']
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
  { label: 'واتساب', href: whatsappHref.value },
  { label: 'فيسبوك', href: facebookUrl ? String(facebookUrl) : '' },
  { label: 'إنستغرام', href: instagramUrl ? String(instagramUrl) : '' }
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
useSeoMeta({
  title: 'أوركيديا | زيوت ومنتجات طبيعية',
  description: 'أوركيديا للزيوت والمنتجات الطبيعية. منتجات طبيعية. اختاري منتجك واطلبي بسهولة والدفع عند الاستلام.'
})
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
      <div class="mt-6 grid grid-cols-2 gap-3 sm:flex sm:flex-wrap sm:justify-center sm:gap-5 sm:*:w-56 lg:*:w-64">
        <CategoryCard v-reveal.zoom name="كل المنتجات" to="/#catalogue-heading" image="/brand/cat-all.webp" />
        <CategoryCard v-for="(category, i) in data?.categories ?? []" :key="category.id" v-reveal.zoom="(i + 1) * 80" :name="category.name"
          :to="`/?categoryId=${category.id}#catalogue-heading`" :image="categoryImage(category.name)" />
      </div>
      <nav v-if="data?.categories?.length" aria-label="فئات المنتجات"
        class="-mx-4 mt-5 flex gap-2 overflow-x-auto px-4 pb-2 sm:mx-0 sm:flex-wrap sm:justify-center sm:overflow-visible sm:px-0">
        <NuxtLink :to="{ query: catalogueQuery({ categoryId: undefined }), hash: '#catalogue-heading' }"
          :aria-current="categoryId === undefined ? 'page' : undefined"
          class="chip shrink-0 whitespace-nowrap aria-[current=page]:bg-brand aria-[current=page]:text-white">كل المنتجات</NuxtLink>
        <NuxtLink v-for="category in data.categories" :key="category.id"
          :to="{ query: catalogueQuery({ categoryId: category.id }), hash: '#catalogue-heading' }"
          :aria-current="categoryId === category.id ? 'page' : undefined"
          class="chip shrink-0 whitespace-nowrap aria-[current=page]:bg-brand aria-[current=page]:text-white">{{ category.name }}</NuxtLink>
      </nav>
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
      <div class="mb-4 flex flex-wrap items-end justify-between gap-2">
        <h2 id="catalogue-heading" tabindex="-1" class="scroll-mt-4 text-2xl font-bold text-ink sm:text-3xl">كل منتجاتنا</h2>
        <p v-if="data?.items.length && !error" class="text-sm text-ink-soft">عدد المنتجات <bdi class="font-bold">{{ data.total }}</bdi></p>
      </div>

      <form role="search" class="mb-5 flex flex-col gap-3 sm:flex-row sm:items-end" @submit.prevent="submitSearch">
        <div class="min-w-0 flex-1">
          <label for="catalogue-search" class="mb-1 block text-sm font-semibold text-ink">ابحثي عن منتج</label>
          <div class="relative">
            <input id="catalogue-search" v-model="searchText" type="search" maxlength="100" autocomplete="off" enterkeyhint="search"
              class="field min-h-11 w-full pe-11" @input="onSearchInput">
            <button v-if="searchText" type="button" aria-label="مسح البحث" class="absolute inset-y-0 end-0 grid min-h-11 w-11 place-items-center text-ink-soft hover:text-brand" @click="clearSearch">
              <span aria-hidden="true">&times;</span>
            </button>
          </div>
        </div>
        <div class="sm:w-56">
          <label for="catalogue-sort" class="mb-1 block text-sm font-semibold text-ink">الترتيب</label>
          <select id="catalogue-sort" class="field min-h-11 w-full" :value="sort ?? 'default'" @change="onSortChange">
            <option v-for="option in sortOptions" :key="option.value" :value="option.value">{{ option.label }}</option>
          </select>
        </div>
      </form>

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
            {{ search ? 'ما لقيناش منتجات تطابق بحثك.' : categoryId ? 'لا توجد منتجات ضمن الفئة المختارة حاليًا.' : 'المنتجات قريبًا، راجعينا بعد قليل 🌿' }}
          </p>
          <button v-if="search" type="button" class="btn-secondary mt-4" @click="clearSearch">مسح البحث</button>
          <NuxtLink v-else-if="categoryId" :to="{ query: catalogueQuery({ categoryId: undefined }), hash: '#catalogue-heading' }" class="btn-primary mt-4">
            عرض كل المنتجات
          </NuxtLink>
        </div>
        <div v-else ref="gridRef" class="grid grid-cols-2 gap-3 sm:gap-5 md:grid-cols-3 lg:grid-cols-4">
          <ProductCard v-for="product in products" :key="product.id" :product="product" class="reveal" />
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
      <h2 v-reveal id="order-heading" class="text-center text-2xl font-bold text-ink sm:text-3xl">كيف تطلبي؟</h2>
      <ol class="mt-6 grid gap-4 sm:grid-cols-3 sm:gap-6">
        <li v-for="(step, i) in steps" :key="step" v-reveal="i * 120" class="flex items-center gap-4 rounded-[var(--radius-card)] bg-white p-4 shadow-soft motion-safe:transition-transform motion-safe:duration-300 hover:-translate-y-1 sm:flex-col sm:p-6 sm:text-center">
          <span class="grid size-11 shrink-0 place-items-center rounded-full bg-brand text-lg font-bold text-white" aria-hidden="true">{{ i + 1 }}</span>
          <span class="font-bold text-ink">{{ step }}</span>
        </li>
      </ol>
      <p class="mt-5 text-center text-sm font-semibold text-brand">الدفع عند الاستلام</p>
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

    <!-- About -->
    <section id="about-orkidea" tabindex="-1" aria-labelledby="about-heading" class="mt-10 scroll-mt-4 px-1 text-center">
      <h2 v-reveal id="about-heading" class="text-2xl font-bold text-ink sm:text-3xl">أوركيديا</h2>
      <p class="mx-auto mt-3 max-w-xl text-ink-soft">أوركيديا متجر ليبي للزيوت والصابون الطبيعية. منتجاتنا طبيعية.</p>
    </section>

    <!-- Contact -->
    <section id="contact" tabindex="-1" aria-labelledby="contact-heading" class="mt-10 mb-6 scroll-mt-4 rounded-[var(--radius-card)] bg-lavender px-5 py-8 sm:px-10">
      <SectionHeader v-reveal id="contact-heading" title="هل لديكِ" highlight="استفسار؟" subtitle="تواصلي معانا وبنرد عليك في أقرب وقت" />
      <div class="mt-6 grid gap-6 lg:grid-cols-2 lg:items-start">
        <ContactForm />
        <div class="flex flex-col gap-3 text-center">
          <p class="font-bold text-ink">أو تواصلي معانا مباشرة</p>
          <template v-if="contactLinks.length">
            <a v-for="link in contactLinks" :key="link.label" :href="link.href" target="_blank" rel="noopener noreferrer" class="btn-secondary bg-white">{{ link.label }}</a>
          </template>
          <p v-else class="text-ink-soft">قنوات التواصل ستتوفر قريبًا.</p>
        </div>
      </div>
    </section>
  </div>
</template>
