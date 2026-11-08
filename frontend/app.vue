<script setup lang="ts">
const preview = useRuntimeConfig().public.preview
useSeoMeta({ robots: preview ? 'noindex, nofollow, noarchive' : undefined })
</script>

<template>
  <div v-if="preview" class="bg-purple-900 px-4 py-2 text-center text-sm font-semibold text-white">
    نسخة تجريبية. استخدم بيانات اختبار فقط.
  </div>
  <NuxtLayout><NuxtPage /></NuxtLayout>
</template>
