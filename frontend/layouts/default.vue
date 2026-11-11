<script setup lang="ts">
const { cart } = useCart()
const brandImage = '/images/logo.jpg'
</script>

<template>
  <div class="flex min-h-screen flex-col">
    <a href="#content" class="sr-only focus:not-sr-only">الانتقال إلى المحتوى</a>
    <header class="border-b border-purple-100 bg-white">
      <nav class="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-x-4 gap-y-1 px-4 py-3" aria-label="القائمة الرئيسية">
        <NuxtLink to="/" class="inline-flex min-h-11 items-center gap-2 rounded-lg text-xl font-bold text-purple-950">
          <img :src="brandImage" alt="" width="36" height="36"
            class="size-9 rounded-full motion-safe:transition-transform motion-safe:hover:scale-105">
          أوركيديا
        </NuxtLink>
        <div class="flex flex-wrap items-center gap-1 text-sm font-semibold text-purple-950">
          <NuxtLink :to="{ path: '/', hash: '#catalogue-heading' }" class="inline-flex min-h-11 items-center rounded-lg px-2 hover:bg-purple-50 sm:px-3">المنتجات</NuxtLink>
          <NuxtLink to="/cart" class="inline-flex min-h-11 items-center gap-2 rounded-lg px-2 hover:bg-purple-50 sm:px-3">
            <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" class="size-5">
              <path d="M5 8h14l1 12H4L5 8Z M9 9V6a3 3 0 0 1 6 0v3" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
            السلة <bdi v-if="cart?.count" class="rounded-full bg-purple-100 px-2 py-0.5 text-xs">{{ cart.count }}</bdi>
          </NuxtLink>
          <NuxtLink to="/account" class="inline-flex min-h-11 items-center rounded-lg px-2 hover:bg-purple-50 sm:px-3">الحساب</NuxtLink>
        </div>
      </nav>
    </header>
    <main id="content" class="mx-auto w-full max-w-6xl flex-1 px-4 py-8"><slot /></main>
    <footer class="border-t border-purple-100 bg-purple-50">
      <div class="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-x-6 gap-y-2 px-4 py-5">
        <p class="font-bold text-purple-950">أوركيديا</p>
        <nav aria-label="روابط أسفل الصفحة" class="flex flex-wrap gap-x-4 text-sm text-purple-950">
          <NuxtLink :to="{ path: '/', hash: '#about-orkidea' }" class="inline-flex min-h-11 items-center hover:underline">عن أوركيديا</NuxtLink>
          <NuxtLink :to="{ path: '/', hash: '#catalogue-heading' }" class="inline-flex min-h-11 items-center hover:underline">تصفّحي المنتجات</NuxtLink>
        </nav>
      </div>
    </footer>
  </div>
</template>
