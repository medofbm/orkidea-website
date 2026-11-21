<script setup lang="ts">
const { cart, load } = useCart()
const route = useRoute()
const { facebookUrl, instagramUrl, whatsappNumber } = useRuntimeConfig().public
const whatsappHref = computed(() => {
  const digits = String(whatsappNumber ?? '').replace(/\D/g, '')
  return digits ? `https://wa.me/${digits}` : ''
})
const year = new Date().getFullYear()
const navLink = 'storefront-action inline-flex min-h-11 items-center rounded-full px-4 hover:bg-lavender/60'
const footLink = 'inline-flex min-h-11 items-center text-white/85 hover:text-white hover:underline'
const socialBtn = 'storefront-action inline-flex size-11 items-center justify-center rounded-full bg-white/15 text-white hover:bg-white/25'
const whatsappDisplay = computed(() => {
  const digits = String(whatsappNumber ?? '').replace(/\D/g, '')
  return digits ? `+${digits}` : ''
})
const menuOpen = ref(false)
const menuButton = ref<HTMLButtonElement>()
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
onUnmounted(removePageFinishHook)
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
onMounted(() => {
  if (!cart.value && route.path !== '/cart') void load().catch(() => undefined)
})
</script>

<template>
  <div class="flex min-h-screen flex-col">
    <a href="#content" class="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:bg-white focus:px-4 focus:py-2">الانتقال إلى المحتوى</a>
    <header class="sticky top-0 z-40 border-b border-lavender bg-white/95 backdrop-blur">
      <nav class="mx-auto flex max-w-6xl flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2" aria-label="القائمة الرئيسية">
        <NuxtLink to="/" class="inline-flex min-h-11 items-center gap-3 rounded-full text-xl font-bold text-brand">
          <img src="/brand/flower-purple.png" alt="شعار أوركيديا" width="40" height="40" class="size-10 object-contain">
          أوركيديا
        </NuxtLink>
        <div id="store-navigation" :class="menuOpen ? 'flex' : 'hidden'" class="order-last w-full flex-col gap-1 border-t border-lavender pt-2 text-sm font-semibold text-ink sm:contents" @keydown.esc="closeMenu">
          <div class="flex flex-col gap-1 sm:order-2 sm:flex-row sm:items-center">
            <NuxtLink to="/" :class="[navLink, 'gap-2']" @click="menuOpen = false">
              <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" class="size-5"><path d="M4 11l8-7 8 7v9a1 1 0 0 1-1 1h-4v-6H9v6H5a1 1 0 0 1-1-1v-9Z" stroke-linecap="round" stroke-linejoin="round"/></svg>
              الرئيسية
            </NuxtLink>
            <NuxtLink :to="{ path: '/', hash: '#catalogue-heading' }" :class="navLink" @click="navigateFromMenu($event, '#catalogue-heading')">المنتجات</NuxtLink>
            <NuxtLink :to="{ path: '/', hash: '#categories' }" :class="navLink" @click="navigateFromMenu($event, '#categories')">الفئات</NuxtLink>
            <NuxtLink :to="{ path: '/', hash: '#how-to-order' }" :class="navLink" @click="navigateFromMenu($event, '#how-to-order')">طريقة الطلب</NuxtLink>
          </div>
          <div class="flex flex-col gap-2 pb-2 sm:order-4 sm:flex-row sm:items-center sm:pb-0">
            <NuxtLink to="/account/register" class="btn-secondary storefront-action text-sm" @click="menuOpen = false">إنشاء حساب</NuxtLink>
            <NuxtLink to="/account" class="btn-primary storefront-action text-sm" @click="menuOpen = false">دخول</NuxtLink>
          </div>
        </div>
        <div class="ms-auto flex items-center gap-1 text-sm font-semibold text-ink sm:order-3 sm:ms-0">
          <NuxtLink to="/cart" class="storefront-action inline-flex min-h-11 items-center gap-2 rounded-full border border-lavender-deep px-4 hover:bg-lavender/60">
            <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" class="size-5">
              <path d="M5 8h14l1 12H4L5 8Z M9 9V6a3 3 0 0 1 6 0v3" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
            السلة <bdi v-if="cart?.count" class="rounded-full bg-brand px-2 py-0.5 text-xs text-white">{{ cart.count }}</bdi>
          </NuxtLink>
          <button ref="menuButton" type="button" class="storefront-action inline-flex min-h-11 min-w-11 items-center justify-center gap-2 rounded-full border border-lavender-deep px-3 hover:bg-lavender/60 sm:hidden"
            :aria-expanded="menuOpen" aria-controls="store-navigation" @click="menuOpen = !menuOpen" @keydown.esc="closeMenu">
            <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" class="size-5"><path :d="menuOpen ? 'M6 6l12 12M18 6L6 18' : 'M4 7h16M4 12h16M4 17h16'" stroke-linecap="round" /></svg>
            {{ menuOpen ? 'إغلاق' : 'القائمة' }}
          </button>
        </div>
      </nav>
    </header>
    <main id="content" class="mx-auto w-full max-w-6xl flex-1 px-4 py-8"><slot /></main>
    <footer class="rounded-t-3xl bg-linear-to-b from-brand to-plum text-cream">
      <div class="mx-auto grid max-w-6xl gap-8 px-4 py-10 sm:grid-cols-2 sm:gap-10">
        <section aria-labelledby="footer-about">
          <NuxtLink to="/" class="inline-flex min-h-11 items-center gap-3 rounded-full text-lg font-bold text-white">
            <img src="/brand/flower-white.png" alt="" width="36" height="36" class="size-9 object-contain">
            أوركيديا
          </NuxtLink>
          <h2 id="footer-about" class="mt-2 text-lg font-bold text-white">عن أوركيديا</h2>
          <p class="mt-2 text-sm leading-7 text-white/90">متجر متخصص في بيع الزيوت والصابون الطبيعية. نقدّم منتجات طبيعية وعضوية للعناية بك.</p>
          <ul class="mt-3 flex flex-wrap gap-2" aria-label="مزايا المنتجات">
            <li class="inline-flex min-h-9 items-center rounded-full bg-white/15 px-4 text-sm font-semibold text-white">طبيعي 100%</li>
            <li class="inline-flex min-h-9 items-center rounded-full bg-white/15 px-4 text-sm font-semibold text-white">عضوي</li>
            <li class="inline-flex min-h-9 items-center rounded-full bg-white/15 px-4 text-sm font-semibold text-white">صنع يدوي</li>
          </ul>
        </section>
        <section aria-labelledby="footer-contact">
          <h2 id="footer-contact" class="text-lg font-bold text-white">تواصلي معنا</h2>
          <div v-if="whatsappHref || instagramUrl || facebookUrl" class="mt-3 flex flex-wrap items-center gap-3">
            <a v-if="whatsappHref" :href="whatsappHref" target="_blank" rel="noopener noreferrer" aria-label="تواصلي معنا عبر واتساب" :class="socialBtn">
              <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" class="size-5"><path d="M4 20l1.3-4.2A8 8 0 1 1 8.4 18.8L4 20Z" stroke-linejoin="round"/><path d="M9 8.5c0 3.5 3 6.5 6.5 6.5l1-1.5-2-1-1 .8c-1-.4-2-1.4-2.4-2.4l.8-1-1-2-1.9.6Z" stroke-linejoin="round"/></svg>
            </a>
            <a v-if="instagramUrl" :href="instagramUrl" target="_blank" rel="noopener noreferrer" aria-label="تابعي أوركيديا على إنستغرام" :class="socialBtn">
              <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" class="size-5"><rect x="4" y="4" width="16" height="16" rx="5"/><circle cx="12" cy="12" r="3.7"/><circle cx="16.8" cy="7.2" r="0.8" fill="currentColor" stroke="none"/></svg>
            </a>
            <a v-if="facebookUrl" :href="facebookUrl" target="_blank" rel="noopener noreferrer" aria-label="تابعي أوركيديا على فيسبوك" :class="socialBtn">
              <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" class="size-5"><path d="M14 21v-7h2.5l.5-3h-3V9.2c0-.9.4-1.7 1.8-1.7H17V5a17 17 0 0 0-2.4-.2C12.2 4.8 11 6.2 11 8.6V11H8.5v3H11v7" stroke-linecap="round" stroke-linejoin="round"/></svg>
            </a>
          </div>
          <a v-if="whatsappDisplay" :href="`tel:${whatsappDisplay}`" dir="ltr" class="mt-3 inline-flex min-h-11 items-center font-semibold text-white hover:underline">{{ whatsappDisplay }}</a>
        </section>
      </div>
      <div class="mx-auto max-w-6xl border-t border-white/20 px-4 py-4">
        <nav aria-label="روابط أسفل الصفحة" class="flex flex-wrap justify-center gap-x-6 text-sm font-medium">
          <NuxtLink to="/" :class="footLink">الرئيسية</NuxtLink>
          <NuxtLink to="/cart" :class="footLink">السلة</NuxtLink>
          <NuxtLink to="/account" :class="footLink">الحساب</NuxtLink>
          <NuxtLink to="/order-and-delivery" :class="footLink">طريقة الطلب والتوصيل</NuxtLink>
          <NuxtLink to="/privacy" :class="footLink">سياسة الخصوصية</NuxtLink>
        </nav>
        <p class="mt-2 text-center text-xs text-white/75">Orkidea {{ year }} جميع الحقوق محفوظة · صنع بـ ❤ في ليبيا</p>
      </div>
    </footer>
  </div>
</template>
