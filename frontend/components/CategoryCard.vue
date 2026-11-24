<script setup lang="ts">
// Category tile. With `image`, it shows a photo with the name on top; without it, the flower icon tile.
// Put an inline SVG in the `icon` slot; the `icon` string prop is accepted but ignored.
defineProps<{
  name: string
  to: string
  count?: number
  icon?: string
  image?: string
}>()
</script>

<template>
  <NuxtLink
    v-if="image"
    :to="to"
    class="photo-card group relative block aspect-[4/5] overflow-hidden rounded-card bg-lavender shadow-soft outline-none focus-visible:ring-4 focus-visible:ring-brand/40 focus-visible:ring-offset-2"
  >
    <img :src="image" alt="" width="480" height="600" loading="lazy" decoding="async"
      class="absolute inset-0 size-full object-cover motion-safe:transition-transform motion-safe:duration-700 motion-safe:group-hover:scale-105">
    <span class="absolute inset-x-0 bottom-0 h-2/3 bg-gradient-to-t from-ink/80 via-ink/30 to-transparent" aria-hidden="true"></span>
    <span class="absolute inset-x-0 bottom-0 flex items-center justify-between gap-2 p-3 text-white sm:p-5">
      <span class="text-base font-bold sm:text-xl">{{ name }}</span>
      <span class="grid size-8 shrink-0 place-items-center rounded-full bg-white/20 backdrop-blur-sm motion-safe:transition-colors group-hover:bg-white group-hover:text-brand sm:size-10" aria-hidden="true">
        <AppIcon name="chevron-left" :size="18" />
      </span>
    </span>
    <span v-if="count !== undefined" class="absolute top-3 start-3 rounded-full bg-white/85 px-2.5 py-0.5 text-xs font-semibold text-brand">{{ count }} منتج</span>
  </NuxtLink>
  <NuxtLink
    v-else
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
  .category-card, .photo-card { transition: transform 250ms ease-out, box-shadow 250ms ease-out, color 250ms ease-out; }
  .category-card::before { transition: opacity 250ms ease-out; }
  .category-card__circle { transition: background-color 250ms ease-out, color 250ms ease-out; }
  .category-card:hover, .category-card:focus-visible,
  .photo-card:hover, .photo-card:focus-visible { transform: translateY(-4px); box-shadow: var(--shadow-lift); }
}
</style>
