<script setup lang="ts">
const { cart, load } = useCart()
const route = useRoute()
const brandImage = '/images/logo.jpg'
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
    <a href="#content" class="sr-only focus:not-sr-only">الانتقال إلى المحتوى</a>
    <header class="border-b border-lavender bg-white shadow-sm">
      <nav class="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-x-3 gap-y-2 px-4 py-3" aria-label="القائمة الرئيسية">
        <NuxtLink to="/" class="inline-flex min-h-11 items-center gap-2 rounded-lg text-xl font-bold text-ink">
          <img :src="brandImage" alt="" width="36" height="36"
            class="size-9 rounded-full motion-safe:transition-transform motion-safe:hover:scale-105">
          أوركيديا
        </NuxtLink>
        <div class="flex items-center gap-1 text-sm font-semibold text-ink">
          <NuxtLink to="/cart" class="storefront-action inline-flex min-h-11 items-center gap-1 rounded-lg px-2 hover:bg-lavender/50 sm:gap-2 sm:px-3">
            <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" class="size-5">
              <path d="M5 8h14l1 12H4L5 8Z M9 9V6a3 3 0 0 1 6 0v3" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
            السلة <bdi v-if="cart?.count" class="rounded-full bg-lavender px-2 py-0.5 text-xs">{{ cart.count }}</bdi>
          </NuxtLink>
          <button ref="menuButton" type="button" class="storefront-action inline-flex min-h-11 min-w-11 items-center justify-center gap-2 rounded-lg border border-lavender-deep px-3 hover:bg-lavender/50 sm:hidden"
            :aria-expanded="menuOpen" aria-controls="store-navigation" @click="menuOpen = !menuOpen" @keydown.esc="closeMenu">
            <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" class="size-5"><path :d="menuOpen ? 'M6 6l12 12M18 6L6 18' : 'M4 7h16M4 12h16M4 17h16'" stroke-linecap="round" /></svg>
            {{ menuOpen ? 'إغلاق' : 'القائمة' }}
          </button>
        </div>
        <div id="store-navigation" :class="menuOpen ? 'flex' : 'hidden'" class="w-full flex-col gap-1 border-t border-lavender pt-2 text-sm font-semibold text-ink sm:flex sm:w-auto sm:flex-row sm:items-center sm:border-0 sm:pt-0" @keydown.esc="closeMenu">
          <NuxtLink :to="{ path: '/', hash: '#catalogue-heading' }" class="inline-flex min-h-11 items-center rounded-lg px-3 hover:bg-lavender/50" @click="navigateFromMenu($event, '#catalogue-heading')">المنتجات</NuxtLink>
          <NuxtLink :to="{ path: '/', hash: '#categories' }" class="inline-flex min-h-11 items-center rounded-lg px-3 hover:bg-lavender/50" @click="navigateFromMenu($event, '#categories')">الفئات</NuxtLink>
          <NuxtLink :to="{ path: '/', hash: '#how-to-order' }" class="inline-flex min-h-11 items-center rounded-lg px-3 hover:bg-lavender/50" @click="navigateFromMenu($event, '#how-to-order')">طريقة الطلب</NuxtLink>
          <NuxtLink to="/account" class="inline-flex min-h-11 items-center rounded-lg px-3 hover:bg-lavender/50" @click="menuOpen = false">الحساب</NuxtLink>
        </div>
      </nav>
    </header>
    <main id="content" class="mx-auto w-full max-w-6xl flex-1 px-4 py-8"><slot /></main>
    <footer class="border-t border-lavender bg-lavender/40">
      <div class="mx-auto grid max-w-6xl gap-5 px-4 py-8 sm:grid-cols-[minmax(12rem,1fr)_2fr] sm:gap-8">
        <div>
          <NuxtLink to="/" class="inline-flex min-h-11 items-center gap-2 rounded-lg text-lg font-bold text-ink">
            <img :src="brandImage" alt="" width="36" height="36" class="size-9 rounded-full">
            أوركيديا
          </NuxtLink>
          <p class="mt-1 text-sm text-ink-soft">منتجات أوركيديا من وحي الطبيعة</p>
        </div>
        <nav aria-label="روابط أسفل الصفحة" class="flex flex-wrap content-start gap-x-5 text-sm font-medium text-ink">
          <NuxtLink :to="{ path: '/', hash: '#about-orkidea' }" class="inline-flex min-h-11 items-center hover:underline">عن أوركيديا</NuxtLink>
          <NuxtLink :to="{ path: '/', hash: '#catalogue-heading' }" class="inline-flex min-h-11 items-center hover:underline">تصفّحي المنتجات</NuxtLink>
          <NuxtLink :to="{ path: '/', hash: '#how-to-order' }" class="inline-flex min-h-11 items-center hover:underline">طريقة الطلب والتوصيل</NuxtLink>
          <NuxtLink to="/cart" class="inline-flex min-h-11 items-center hover:underline">السلة</NuxtLink>
          <NuxtLink to="/account" class="inline-flex min-h-11 items-center hover:underline">الحساب</NuxtLink>
          <a href="https://www.facebook.com/ORKIDEALY/" class="inline-flex min-h-11 items-center hover:underline">فيسبوك</a>
          <a href="https://www.instagram.com/_orkidea_1/" class="inline-flex min-h-11 items-center hover:underline">إنستغرام</a>
          <NuxtLink to="/order-and-delivery" class="inline-flex min-h-11 items-center hover:underline">سياسة الطلب والتوصيل</NuxtLink>
          <NuxtLink to="/privacy" class="inline-flex min-h-11 items-center hover:underline">سياسة الخصوصية</NuxtLink>
        </nav>
      </div>
    </footer>
  </div>
</template>
