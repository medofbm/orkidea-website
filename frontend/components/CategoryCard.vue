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
        <img src="/brand/flower-purple.png" alt="" width="36" height="36" class="size-9 object-contain motion-safe:transition-transform motion-safe:duration-500 motion-safe:group-hover:rotate-[20deg] motion-safe:group-hover:scale-110">
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
