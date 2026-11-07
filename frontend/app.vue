<template>
  <div class="min-h-screen">
    <a href="#content" class="sr-only focus:not-sr-only">الانتقال إلى المحتوى</a>
    <header class="border-b border-purple-100 bg-white">
      <nav class="mx-auto flex max-w-6xl items-center justify-between px-4 py-5" aria-label="القائمة الرئيسية">
        <NuxtLink to="/" class="text-2xl font-bold text-purple-900">أوركيديا</NuxtLink>
        <div class="flex flex-wrap gap-1">
          <NuxtLink to="/cart" class="rounded-lg px-3 py-2">السلة</NuxtLink>
          <NuxtLink to="/account" class="rounded-lg px-3 py-2">الحساب</NuxtLink>
        </div>
      </nav>
    </header>
    <main id="content" class="mx-auto max-w-6xl px-4 py-8"><NuxtPage /></main>
  </div>
</template>
