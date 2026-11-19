<script setup lang="ts">
const { cart, load } = useCart()
const route = useRoute()
const brandImage = '/images/logo.jpg'
const { facebookUrl, instagramUrl, whatsappNumber } = useRuntimeConfig().public
const whatsappHref = computed(() => {
  const digits = String(whatsappNumber ?? '').replace(/\D/g, '')
  return digits ? `https://wa.me/${digits}` : ''
})
const year = new Date().getFullYear()
const navLink = 'storefront-action inline-flex min-h-11 items-center rounded-full px-4 hover:bg-lavender/60'
const footLink = 'inline-flex min-h-11 items-center text-white/85 hover:text-white hover:underline'
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
    <header class="sticky top-0 z-40 border-b border-lavender bg-cream/95 backdrop-blur">
      <nav class="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-x-3 gap-y-1 px-4 py-2" aria-label="القائمة الرئيسية">
        <NuxtLink to="/" class="inline-flex min-h-11 items-center gap-3 rounded-full text-xl font-bold text-brand">
          <img :src="brandImage" alt="" width="40" height="40" class="size-10 rounded-full object-cover">
          أوركيديا
        </NuxtLink>
        <div id="store-navigation" :class="menuOpen ? 'flex' : 'hidden'" class="order-last w-full flex-col gap-1 border-t border-lavender pt-2 text-sm font-semibold text-ink sm:order-none sm:flex sm:w-auto sm:flex-row sm:items-center sm:border-0 sm:pt-0" @keydown.esc="closeMenu">
          <NuxtLink to="/" :class="navLink" @click="menuOpen = false">الرئيسية</NuxtLink>
          <NuxtLink :to="{ path: '/', hash: '#catalogue-heading' }" :class="navLink" @click="navigateFromMenu($event, '#catalogue-heading')">المنتجات</NuxtLink>
          <NuxtLink :to="{ path: '/', hash: '#categories' }" :class="navLink" @click="navigateFromMenu($event, '#categories')">الفئات</NuxtLink>
          <NuxtLink :to="{ path: '/', hash: '#how-to-order' }" :class="navLink" @click="navigateFromMenu($event, '#how-to-order')">طريقة الطلب</NuxtLink>
          <NuxtLink to="/account" :class="navLink" @click="menuOpen = false">الحساب</NuxtLink>
        </div>
        <div class="flex items-center gap-1 text-sm font-semibold text-ink">
          <NuxtLink to="/cart" class="storefront-action inline-flex min-h-11 items-center gap-2 rounded-full px-3 hover:bg-lavender/60">
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
    <footer class="bg-plum text-cream">
      <div class="mx-auto grid max-w-6xl gap-6 px-4 py-10 sm:grid-cols-[minmax(12rem,1fr)_2fr] sm:gap-10">
        <div>
          <NuxtLink to="/" class="inline-flex min-h-11 items-center gap-3 rounded-full text-lg font-bold text-white">
            <img src="/brand/flower-icon.webp" alt="" width="36" height="36" class="size-9 rounded-full object-cover">
            أوركيديا
          </NuxtLink>
          <p class="mt-1 text-sm text-white/80">للزيوت والمنتجات الطبيعية</p>
          <p class="mt-2 text-sm font-semibold text-gold">الدفع عند الاستلام</p>
        </div>
        <nav aria-label="روابط أسفل الصفحة" class="flex flex-wrap content-start gap-x-6 text-sm font-medium">
          <NuxtLink to="/" :class="footLink">الرئيسية</NuxtLink>
          <NuxtLink :to="{ path: '/', hash: '#catalogue-heading' }" :class="footLink">المنتجات</NuxtLink>
          <NuxtLink :to="{ path: '/', hash: '#how-to-order' }" :class="footLink">طريقة الطلب والتوصيل</NuxtLink>
          <NuxtLink to="/cart" :class="footLink">السلة</NuxtLink>
          <NuxtLink to="/account" :class="footLink">الحساب</NuxtLink>
          <NuxtLink to="/order-and-delivery" :class="footLink">سياسة الطلب والتوصيل</NuxtLink>
          <NuxtLink to="/privacy" :class="footLink">سياسة الخصوصية</NuxtLink>
          <a v-if="whatsappHref" :href="whatsappHref" rel="noopener" :class="footLink">واتساب</a>
          <a v-if="facebookUrl" :href="facebookUrl" rel="noopener" :class="footLink">فيسبوك</a>
          <a v-if="instagramUrl" :href="instagramUrl" rel="noopener" :class="footLink">إنستغرام</a>
        </nav>
      </div>
      <p class="border-t border-white/15 px-4 py-4 text-center text-xs text-white/70">© {{ year }} أوركيديا. جميع الحقوق محفوظة.</p>
    </footer>
  </div>
</template>
