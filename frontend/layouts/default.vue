<script setup lang="ts">
const { cart, pulse, load } = useCart()
const { account, refresh: refreshAccount } = useAccount()
const route = useRoute()
const { facebookUrl, instagramUrl, whatsappNumber } = useRuntimeConfig().public
const whatsappHref = computed(() => {
  const digits = String(whatsappNumber ?? '').replace(/\D/g, '')
  return digits ? `https://wa.me/${digits}` : ''
})
const year = new Date().getFullYear()
// Desktop: plain pills. Mobile (inside the offcanvas): soft lavender chips with icons.
const navLink = 'nav-underline storefront-action inline-flex min-h-12 items-center gap-3 whitespace-nowrap rounded-2xl bg-lavender/60 px-4 text-brand hover:bg-lavender sm:min-h-11 sm:rounded-full sm:bg-transparent sm:px-2.5 lg:px-4 sm:text-ink sm:hover:bg-lavender/60'
const footLink = 'footer-link inline-flex min-h-11 items-center gap-2 text-white/85 hover:text-white'
const socialBtn = 'storefront-action inline-flex size-11 items-center justify-center rounded-full bg-white/15 text-white ring-1 ring-white/25 hover:bg-white/25'
const menuSocialBtn = 'storefront-action inline-flex size-11 items-center justify-center rounded-full bg-lavender text-brand hover:bg-lavender-deep'
const whatsappDisplay = computed(() => {
  const digits = String(whatsappNumber ?? '').replace(/\D/g, '')
  return digits ? `+${digits}` : ''
})
const contactChannels = computed(() => [
  { key: 'whatsapp' as const, label: 'واتساب', href: whatsappHref.value, aria: 'تواصلي معنا عبر واتساب', hover: 'hover:bg-[#25D366] hover:text-white' },
  { key: 'instagram' as const, label: 'إنستغرام', href: instagramUrl ? String(instagramUrl) : '', aria: 'تابعي أوركيديا على إنستغرام', hover: 'hover:bg-[#d62976] hover:text-white' },
  { key: 'facebook' as const, label: 'فيسبوك', href: facebookUrl ? String(facebookUrl) : '', aria: 'تابعي أوركيديا على فيسبوك', hover: 'hover:bg-[#1877f2] hover:text-white' }
].filter(c => c.href))
const trust = [
  { icon: 'cash', title: 'الدفع عند الاستلام', text: 'ما فيش دفع مسبق' },
  { icon: 'truck', title: 'توصيل لكل ليبيا', text: 'لين باب بيتك' },
  { icon: 'leaf', title: 'طبيعي 100%', text: 'مكونات نقية' },
  { icon: 'hand-heart', title: 'صنع يدوي', text: 'بعناية وحب' }
] as const
const menuOpen = ref(false)
const scrolled = ref(false)
const progress = ref(0)
const menuButton = ref<HTMLButtonElement>()
const panel = ref<HTMLElement>()
const nuxtApp = useNuxtApp()
let pendingFocus: string | undefined
async function focusPendingTarget() {
  await nextTick()
  if (!pendingFocus || route.path !== '/' || route.hash !== pendingFocus) return
  const target = document.getElementById(pendingFocus.slice(1))
  if (target) {
    target.focus()
    pendingFocus = undefined
  }
}
const removePageFinishHook = nuxtApp.hook('page:finish', focusPendingTarget)
watch(() => route.fullPath, () => { menuOpen.value = false })
async function navigateFromMenu(event: MouseEvent, hash: string) {
  if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return
  event.preventDefault()
  menuOpen.value = false
  pendingFocus = hash
  await navigateTo({ path: '/', hash })
  await focusPendingTarget()
}
function closeMenu() {
  if (!menuOpen.value) return
  menuOpen.value = false
  menuButton.value?.focus()
}
const isMobile = () => window.matchMedia('(max-width: 639px)').matches
// Keep Tab inside the open offcanvas (menu button + panel links).
function trapTab(event: KeyboardEvent) {
  if (event.key !== 'Tab' || !menuOpen.value || !isMobile() || !panel.value) return
  const items = [menuButton.value, ...Array.from(panel.value.querySelectorAll<HTMLElement>('a[href], button'))]
    .filter((el): el is HTMLElement => !!el)
  const first = items[0]
  const last = items[items.length - 1]
  if (!first || !last) return
  const active = document.activeElement
  if (!items.includes(active as HTMLElement)) {
    event.preventDefault()
    first.focus()
  } else if (event.shiftKey && active === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && active === last) {
    event.preventDefault()
    first.focus()
  }
}
watch(menuOpen, async (open) => {
  if (!import.meta.client) return
  document.body.style.overflow = open && isMobile() ? 'hidden' : ''
  if (open && isMobile()) {
    await nextTick()
    panel.value?.querySelector<HTMLElement>('a[href]')?.focus()
  }
})
function onScroll() {
  scrolled.value = window.scrollY > 30
  const max = document.documentElement.scrollHeight - window.innerHeight
  progress.value = max > 0 ? Math.min(1, window.scrollY / max) : 0
}
function onResize() { if (menuOpen.value && !isMobile()) menuOpen.value = false }
onMounted(() => {
  window.addEventListener('scroll', onScroll, { passive: true })
  window.addEventListener('resize', onResize, { passive: true })
  onScroll()
  if (!cart.value && route.path !== '/cart') void load().catch(() => undefined)
  // The account page loads the session itself and shares it through useAccount().
  if (!account.value && route.path !== '/account') void refreshAccount().catch(() => undefined)
})
onUnmounted(() => {
  removePageFinishHook()
  window.removeEventListener('scroll', onScroll)
  window.removeEventListener('resize', onResize)
  document.body.style.overflow = ''
})
</script>

<template>
  <div class="flex min-h-screen flex-col">
    <a href="#content" class="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:bg-white focus:px-4 focus:py-2">الانتقال إلى المحتوى</a>
    <header class="header-enter sticky top-0 z-40 border-b border-lavender motion-safe:transition-shadow" :class="scrolled ? 'scrolled shadow-lg shadow-brand/15' : 'shadow-sm shadow-brand/5'">
      <!-- Glass lives on its own layer: backdrop-filter on the header itself would trap the fixed offcanvas. -->
      <div class="glass absolute inset-0 -z-10" aria-hidden="true" />
      <div class="bg-gradient-strip h-[3px] motion-safe:animate-gradient-shift" aria-hidden="true" />
      <div class="absolute inset-x-0 bottom-0 h-0.5 origin-right bg-gradient-brand" :style="{ transform: `scaleX(${progress})` }" aria-hidden="true" />
      <nav class="mx-auto flex max-w-6xl flex-nowrap items-center gap-2 px-4 motion-safe:transition-[padding] lg:grid lg:grid-cols-[1fr_auto_1fr]" :class="scrolled ? 'py-1' : 'py-2'" aria-label="القائمة الرئيسية" @keydown.esc="closeMenu" @keydown="trapTab">
        <NuxtLink to="/" class="group inline-flex min-h-11 shrink-0 items-center gap-2 rounded-full sm:order-1 lg:justify-self-start">
          <img src="/brand/flower-purple.png" alt="شعار أوركيديا" width="40" height="40" class="size-10 object-contain motion-safe:transition-transform motion-safe:duration-500 motion-safe:group-hover:rotate-[20deg] motion-safe:group-hover:scale-110">
          <span class="text-gradient-brand text-shine text-xl font-extrabold">أوركيديا</span>
        </NuxtLink>

        <div class="ms-auto flex items-center gap-1 text-sm font-semibold text-ink sm:hidden">
          <NuxtLink to="/cart" class="storefront-action relative inline-flex min-h-11 min-w-11 items-center justify-center gap-2 rounded-full border border-lavender-deep px-3 hover:bg-lavender/60 sm:px-4">
            <AppIcon name="bag" :size="20" />
            <span class="max-sm:sr-only">السلة</span>
            <bdi v-if="cart?.count" :key="pulse" class="rounded-full bg-brand px-2 py-0.5 text-xs text-white max-sm:absolute max-sm:-top-1 max-sm:-end-1" :class="pulse ? 'motion-safe:animate-pulse-once' : ''">{{ cart.count }}</bdi>
          </NuxtLink>
          <button ref="menuButton" type="button" class="storefront-action relative z-20 inline-flex min-h-11 min-w-11 items-center justify-center rounded-full border border-lavender-deep bg-white/80 hover:bg-lavender/60 sm:hidden"
            :aria-expanded="menuOpen" aria-controls="store-navigation" @click="menuOpen = !menuOpen">
            <span class="relative inline-flex size-5 items-center justify-center" aria-hidden="true">
              <AppIcon name="menu" :size="20" class="absolute motion-safe:transition-all motion-safe:duration-300" :class="menuOpen ? 'rotate-90 opacity-0' : 'rotate-0 opacity-100'" />
              <AppIcon name="x" :size="20" class="absolute motion-safe:transition-all motion-safe:duration-300" :class="menuOpen ? 'rotate-0 opacity-100' : '-rotate-90 opacity-0'" />
            </span>
            <span class="sr-only">{{ menuOpen ? 'إغلاق' : 'القائمة' }}</span>
          </button>
        </div>

        <!-- Backdrop (mobile only) -->
        <div class="fixed inset-0 z-0 bg-ink/40 motion-safe:transition-opacity motion-safe:duration-300 sm:hidden" :class="menuOpen ? 'opacity-100' : 'invisible opacity-0'" aria-hidden="true" @click="closeMenu" />

        <!-- Offcanvas on mobile (slides from the inline-start side), plain inline nav from sm up -->
        <div id="store-navigation" ref="panel"
          class="text-sm font-semibold text-ink max-sm:fixed max-sm:inset-y-0 max-sm:start-0 max-sm:z-10 max-sm:flex max-sm:w-[min(20rem,85vw)] max-sm:flex-col max-sm:overflow-y-auto max-sm:bg-white max-sm:shadow-2xl motion-safe:max-sm:transition-[transform,visibility] motion-safe:max-sm:duration-300 sm:contents"
          :class="menuOpen ? 'max-sm:visible max-sm:translate-x-0' : 'max-sm:invisible max-sm:translate-x-full'">
          <div class="bg-gradient-brand flex min-h-16 items-center gap-3 px-5 py-4 text-white sm:hidden">
            <img src="/brand/flower-white.png" alt="" width="36" height="36" class="size-9 object-contain">
            <span class="text-lg font-extrabold">أوركيديا</span>
          </div>
          <div class="flex flex-col gap-2 p-4 sm:order-2 sm:flex-1 sm:flex-row sm:items-center sm:justify-center sm:gap-1 sm:p-0 lg:flex-none">
            <NuxtLink to="/" :class="navLink" @click="menuOpen = false">
              <AppIcon name="home" :size="20" class="sm:hidden" />
              الرئيسية
            </NuxtLink>
            <NuxtLink :to="{ path: '/', hash: '#catalogue-heading' }" :class="navLink" @click="navigateFromMenu($event, '#catalogue-heading')">
              <AppIcon name="bag" :size="20" class="sm:hidden" />
              المنتجات
            </NuxtLink>
            <NuxtLink :to="{ path: '/', hash: '#categories' }" :class="navLink" @click="navigateFromMenu($event, '#categories')">
              <img src="/brand/flower-purple.png" alt="" width="20" height="20" class="size-5 object-contain sm:hidden">
              الفئات
            </NuxtLink>
            <NuxtLink :to="{ path: '/', hash: '#how-to-order' }" :class="navLink" @click="navigateFromMenu($event, '#how-to-order')">
              <AppIcon name="sparkle" :size="20" class="sm:hidden" />
              طريقة الطلب
            </NuxtLink>
            <NuxtLink :to="{ path: '/', hash: '#contact' }" :class="navLink" class="sm:hidden" @click="navigateFromMenu($event, '#contact')">
              <AppIcon name="whatsapp" :size="20" />
              تواصلي معنا
            </NuxtLink>
          </div>
          <div v-if="whatsappHref || instagramUrl || facebookUrl" class="px-4 pb-4 sm:hidden">
            <p class="mb-2 text-sm font-bold text-ink-soft">تابعينا</p>
            <div class="flex items-center gap-3">
              <a v-if="whatsappHref" :href="whatsappHref" target="_blank" rel="noopener noreferrer" aria-label="تواصلي معنا عبر واتساب" :class="menuSocialBtn"><AppIcon name="whatsapp" :size="20" /></a>
              <a v-if="instagramUrl" :href="instagramUrl" target="_blank" rel="noopener noreferrer" aria-label="تابعي أوركيديا على إنستغرام" :class="menuSocialBtn"><AppIcon name="instagram" :size="20" /></a>
              <a v-if="facebookUrl" :href="facebookUrl" target="_blank" rel="noopener noreferrer" aria-label="تابعي أوركيديا على فيسبوك" :class="menuSocialBtn"><AppIcon name="facebook" :size="20" /></a>
            </div>
          </div>
          <div class="mt-auto flex flex-col gap-2 border-t border-lavender p-4 sm:order-3 sm:mt-0 sm:flex-row sm:items-center sm:border-0 sm:p-0 lg:justify-self-end">
            <template v-if="account?.signedIn">
              <NuxtLink v-if="account.isAdmin" to="/admin" class="btn-secondary storefront-action text-sm whitespace-nowrap sm:min-h-11! sm:px-4! sm:max-lg:hidden" @click="menuOpen = false">
                <AppIcon name="shield" :size="18" class="sm:hidden" />
                لوحة الإدارة
              </NuxtLink>
              <NuxtLink to="/account" class="btn-primary storefront-action text-sm whitespace-nowrap sm:min-h-11! sm:px-5!" @click="menuOpen = false">
                <AppIcon name="user" :size="18" />
                حسابي
              </NuxtLink>
            </template>
            <template v-else>
              <NuxtLink to="/account/register" class="btn-secondary storefront-action text-sm whitespace-nowrap sm:min-h-11! sm:px-4! sm:max-lg:hidden" @click="menuOpen = false">
                <AppIcon name="user-plus" :size="18" class="sm:hidden" />
                إنشاء حساب
              </NuxtLink>
              <NuxtLink to="/account" class="btn-primary storefront-action text-sm whitespace-nowrap sm:min-h-11! sm:px-5!" @click="menuOpen = false">
                <AppIcon name="login" :size="18" class="sm:hidden" />
                دخول
              </NuxtLink>
            </template>
            <NuxtLink to="/cart" class="storefront-action relative inline-flex min-h-11 min-w-11 items-center justify-center gap-2 rounded-full border border-lavender-deep px-3 hover:bg-lavender/60 max-sm:hidden md:px-4">
              <AppIcon name="bag" :size="20" :class="pulse ? 'motion-safe:animate-pulse-once' : ''" :key="'bag-' + pulse" />
              <span class="max-md:sr-only">السلة</span>
              <bdi v-if="cart?.count" :key="pulse" class="rounded-full bg-brand px-2 py-0.5 text-xs text-white" :class="pulse ? 'motion-safe:animate-pulse-once' : ''">{{ cart.count }}</bdi>
            </NuxtLink>
          </div>
        </div>
      </nav>
    </header>
    <main id="content" class="mx-auto w-full max-w-6xl flex-1 px-4 py-5 sm:py-8"><slot /></main>
    <footer class="relative mt-6 overflow-hidden rounded-t-3xl bg-gradient-brand text-white">
      <img src="/brand/flower-white.png" alt="" aria-hidden="true" width="280" height="280" loading="lazy" class="pointer-events-none absolute -bottom-16 -start-12 size-56 rotate-12 opacity-[0.07] sm:size-72">
      <img src="/brand/flower-white.png" alt="" aria-hidden="true" width="160" height="160" loading="lazy" class="pointer-events-none absolute -top-8 end-6 size-32 -rotate-12 opacity-[0.07]">
      <span class="pointer-events-none absolute -top-24 start-1/3 size-64 rounded-full bg-lilac/20 blur-3xl" aria-hidden="true"></span>
      <div class="relative mx-auto max-w-6xl px-4 pt-8">
        <ul class="grid grid-cols-2 gap-3 lg:grid-cols-4" aria-label="مزايا التسوق من أوركيديا">
          <li v-for="item in trust" :key="item.title" class="flex items-center gap-3 rounded-2xl bg-white/10 p-3 ring-1 ring-white/20 backdrop-blur-sm">
            <span class="grid size-10 shrink-0 place-items-center rounded-full bg-white/20" aria-hidden="true"><AppIcon :name="item.icon" :size="20" /></span>
            <span class="min-w-0 text-sm leading-tight"><span class="block font-bold">{{ item.title }}</span><span class="text-white/80">{{ item.text }}</span></span>
          </li>
        </ul>
      </div>
      <div class="relative mx-auto grid max-w-6xl gap-8 px-4 py-10 sm:grid-cols-2 lg:grid-cols-[1.4fr_1fr_1fr] lg:gap-12">
        <section aria-labelledby="footer-about">
          <NuxtLink to="/" class="group inline-flex min-h-11 items-center gap-3 rounded-full text-xl font-extrabold text-white">
            <img src="/brand/flower-white.png" alt="" width="36" height="36" class="size-9 object-contain motion-safe:transition-transform motion-safe:duration-500 motion-safe:group-hover:rotate-[20deg]">
            أوركيديا
          </NuxtLink>
          <h2 id="footer-about" class="mt-2 text-lg font-bold text-white">عن أوركيديا</h2>
          <p class="mt-2 max-w-sm text-sm leading-7 text-white/90">متجر متخصص في بيع الزيوت والصابون الطبيعية. نقدّم منتجات طبيعية للعناية بك.</p>
        </section>
        <section aria-labelledby="footer-links">
          <h2 id="footer-links" class="text-lg font-bold text-white">روابط سريعة</h2>
          <nav aria-label="روابط أسفل الصفحة" class="mt-2 flex flex-col text-sm font-medium">
            <NuxtLink to="/" :class="footLink">الرئيسية</NuxtLink>
            <NuxtLink to="/cart" :class="footLink">السلة</NuxtLink>
            <NuxtLink to="/account" :class="footLink">الحساب</NuxtLink>
            <NuxtLink to="/order-and-delivery" :class="footLink">طريقة الطلب والتوصيل</NuxtLink>
            <NuxtLink to="/privacy" :class="footLink">سياسة الخصوصية</NuxtLink>
          </nav>
        </section>
        <section aria-labelledby="footer-contact">
          <h2 id="footer-contact" class="text-lg font-bold text-white">تواصلي معنا</h2>
          <p class="mt-2 text-sm text-white/85">يسعدنا نرد على استفساراتك.</p>
          <div v-if="contactChannels.length" class="mt-3 flex flex-wrap items-center gap-3">
            <a v-for="c in contactChannels" :key="c.key" :href="c.href" target="_blank" rel="noopener noreferrer" :aria-label="c.aria"
              :class="[socialBtn, c.hover, 'motion-safe:hover:-translate-y-0.5']"><AppIcon :name="c.key" :size="20" /></a>
          </div>
          <a v-if="whatsappDisplay" :href="`tel:${whatsappDisplay}`" dir="ltr" class="footer-link mt-3 inline-flex min-h-11 items-center gap-2 font-semibold text-white"><AppIcon name="phone" :size="18" />{{ whatsappDisplay }}</a>
        </section>
      </div>
      <div class="relative mx-auto max-w-6xl border-t border-white/20 px-4 py-4">
        <p class="text-center text-xs text-white/75">Orkidea {{ year }} جميع الحقوق محفوظة · صنع بـ ❤ في ليبيا</p>
      </div>
    </footer>
  </div>
</template>

<style scoped>
.footer-link { position: relative; width: fit-content; }
.footer-link::after {
  content: ""; position: absolute; inset-inline: 0; bottom: 0.55rem; height: 2px; border-radius: 2px;
  background: currentColor; transform: scaleX(0); transform-origin: right; opacity: 0.8;
}
.footer-link:focus-visible { outline: 3px solid #fff; outline-offset: 2px; border-radius: 6px; }
.footer-link:hover::after, .footer-link:focus-visible::after { transform: scaleX(1); }
@media (prefers-reduced-motion: no-preference) {
  .footer-link::after { transition: transform 250ms ease-out; }
}
</style>
