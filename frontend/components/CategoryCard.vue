<script setup lang="ts">
// Category tile. Put an inline SVG in the `icon` slot; the `icon` string prop is accepted but ignored.
defineProps<{
  name: string
  to: string
  count?: number
  icon?: string
}>()
</script>

<template>
  <NuxtLink
    :to="to"
    class="category-card group flex min-h-[44px] flex-col items-center justify-center gap-3 rounded-card border border-brand/10 bg-white p-6 text-center text-ink shadow-soft outline-none focus-visible:ring-4 focus-visible:ring-brand/40 focus-visible:ring-offset-2"
  >
    <span class="category-card__circle flex h-16 w-16 items-center justify-center rounded-full bg-lavender text-brand" aria-hidden="true">
      <slot name="icon">
        <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3c3 3 4 6 0 9-4-3-3-6 0-9zM12 12c3 2 6 2 8 0-2-3-5-3-8 0zM12 12c-3 2-6 2-8 0 2-3 5-3 8 0zM12 12v9" /></svg>
      </slot>
    </span>
    <span class="text-lg font-bold">{{ name }}</span>
    <span v-if="count !== undefined" class="text-sm opacity-75">{{ count }} منتج</span>
  </NuxtLink>
</template>

<style scoped>
.category-card { position: relative; isolation: isolate; overflow: hidden; }
.category-card::before {
  content: ""; position: absolute; inset: 0; z-index: -1; opacity: 0;
  background-image: var(--gradient-brand);
}
.category-card:hover, .category-card:focus-visible { color: #fff; }
.category-card:hover::before, .category-card:focus-visible::before { opacity: 1; }
.category-card:hover .category-card__circle,
.category-card:focus-visible .category-card__circle { background: rgb(255 255 255 / 0.2); color: #fff; }
@media (prefers-reduced-motion: no-preference) {
  .category-card { transition: transform 250ms ease-out, box-shadow 250ms ease-out, color 250ms ease-out; }
  .category-card::before { transition: opacity 250ms ease-out; }
  .category-card__circle { transition: background-color 250ms ease-out, color 250ms ease-out; }
  .category-card:hover, .category-card:focus-visible { transform: translateY(-4px); box-shadow: var(--shadow-lift); }
}
</style>
