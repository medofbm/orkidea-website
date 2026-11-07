<script setup lang="ts">
useSeoMeta({ robots: 'noindex, nofollow, noarchive' })
</script>

<template>
  <div class="min-h-screen">
    <a href="#admin-content" class="sr-only focus:not-sr-only">الانتقال إلى المحتوى</a>
    <header class="border-b border-purple-100 bg-white">
      <nav class="mx-auto flex max-w-6xl flex-wrap items-center gap-3 px-4 py-4" aria-label="قائمة الإدارة">
        <NuxtLink to="/admin" class="me-4 text-xl font-bold text-purple-900">إدارة أوركيديا</NuxtLink>
        <NuxtLink to="/admin/orders" class="rounded-lg px-3 py-2">الطلبات</NuxtLink>
        <NuxtLink to="/account" class="rounded-lg px-3 py-2">الحساب</NuxtLink>
        <NuxtLink to="/" class="rounded-lg px-3 py-2">المتجر</NuxtLink>
      </nav>
    </header>
    <main id="admin-content" class="mx-auto max-w-6xl px-4 py-8"><slot /></main>
  </div>
</template>
