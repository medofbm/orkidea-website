<script setup lang="ts">
// The Orkidea flower from the logo (white mark on the brand gradient). Decorative: the page title names the place.
withDefaults(defineProps<{ size?: 'sm' | 'md' | 'lg' }>(), { size: 'md' })
</script>

<template>
  <span class="brand-mark relative mx-auto inline-grid place-items-center rounded-full bg-gradient-brand shadow-(--shadow-glow) motion-safe:animate-glow-pulse"
    :class="{ 'size-12': size === 'sm', 'size-20': size === 'md', 'size-28': size === 'lg' }" aria-hidden="true">
    <span class="absolute inset-0 rounded-full ring-2 ring-brand/30 motion-safe:animate-confirm-ring" />
    <img src="/brand/flower-white.png" alt="" width="64" height="64" class="h-auto w-3/5 motion-safe:animate-float">
  </span>
</template>
