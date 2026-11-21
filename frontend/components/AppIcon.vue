<script lang="ts">
export const APP_ICON_NAMES = [
  'leaf', 'hand-heart', 'star', 'star-filled', 'truck', 'cash', 'phone', 'whatsapp', 'facebook',
  'instagram', 'cart', 'bag', 'user', 'user-plus', 'login', 'home', 'check', 'check-circle', 'x',
  'chevron-left', 'chevron-right', 'heart', 'mail', 'menu', 'sparkle', 'shield', 'gift', 'droplet', 'flower'
] as const
export type AppIconName = typeof APP_ICON_NAMES[number]
</script>

<script setup lang="ts">
// Simple hand-drawn stroke icons (24x24, currentColor). Decorative unless `label` is given.
const props = withDefaults(defineProps<{ name: AppIconName, size?: number | string, label?: string }>(), { size: 20 })

const star = 'M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1L3.2 9.5l6.1-.9z'
const paths: Record<AppIconName, string[]> = {
  leaf: ['M5 19c0-8 5-13 14-14 0 9-5 14-13 14', 'M5 19c3-5 6-8 10-10'],
  'hand-heart': ['M3 15h3l4 2h5a2 2 0 0 0 0-4h-3', 'M6 15v5H3', 'M10 17l5.5 1.5L21 15', 'M14 10c-1.5-1.3-3-2.2-3-3.8A1.9 1.9 0 0 1 14 5a1.9 1.9 0 0 1 3 1.2c0 1.6-1.5 2.5-3 3.8z'],
  star: [star],
  'star-filled': [star],
  truck: ['M2 6h12v10H2z', 'M14 9h4l3 3v4h-7', 'M6.5 16a1.8 1.8 0 1 0 .01 0', 'M17.5 16a1.8 1.8 0 1 0 .01 0'],
  cash: ['M3 6h18v12H3z', 'M12 9.5a2.5 2.5 0 1 0 .01 0', 'M6 9v.01M18 15v.01'],
  phone: ['M5 4h4l2 5-2.5 1.5a11 11 0 0 0 5 5L15 13l5 2v4a2 2 0 0 1-2 2A15 15 0 0 1 3 6a2 2 0 0 1 2-2z'],
  whatsapp: ['M3 21l1.7-5A8.5 8.5 0 1 1 8 19.3z', 'M9 8.5c0 3.5 3 6.5 6.5 6.5l1-1.5-2-1-1 .8a4 4 0 0 1-2-2l.8-1-1-2z'],
  facebook: ['M14 8h3V4h-3a4 4 0 0 0-4 4v3H7v4h3v6h4v-6h3l1-4h-4V8z'],
  instagram: ['M4 8a4 4 0 0 1 4-4h8a4 4 0 0 1 4 4v8a4 4 0 0 1-4 4H8a4 4 0 0 1-4-4z', 'M12 8.5a3.5 3.5 0 1 0 .01 0', 'M17 7v.01'],
  cart: ['M3 4h2l2.2 11h10.5l2-8H6', 'M9 20a1 1 0 1 0 .01 0', 'M17 20a1 1 0 1 0 .01 0'],
  bag: ['M5 8h14l-1 12H6z', 'M9 8V6a3 3 0 0 1 6 0v2'],
  user: ['M12 4a4 4 0 1 0 .01 0', 'M4 20c0-4 3.5-6 8-6s8 2 8 6'],
  'user-plus': ['M10 4a4 4 0 1 0 .01 0', 'M2 20c0-4 3-6 8-6 1.5 0 2.8.2 4 .6', 'M19 14v6M16 17h6'],
  login: ['M14 4h5a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1h-5', 'M4 12h11', 'M11 8l4 4-4 4'],
  home: ['M3 11l9-8 9 8', 'M5 10v10h5v-6h4v6h5V10'],
  check: ['M5 12.5l4.5 4.5L19 7'],
  'check-circle': ['M12 3a9 9 0 1 0 .01 0', 'M8 12.5l3 3 5-6'],
  x: ['M6 6l12 12M18 6L6 18'],
  'chevron-left': ['M15 5l-7 7 7 7'],
  'chevron-right': ['M9 5l7 7-7 7'],
  heart: ['M12 20s-8-4.8-8-11a4.5 4.5 0 0 1 8-2.8A4.5 4.5 0 0 1 20 9c0 6.2-8 11-8 11z'],
  mail: ['M3 6h18v12H3z', 'M3 7l9 7 9-7'],
  menu: ['M4 6h16M4 12h16M4 18h16'],
  sparkle: ['M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z', 'M19 16l.7 1.8 1.8.7-1.8.7L19 21l-.7-1.8-1.8-.7 1.8-.7z'],
  shield: ['M12 3l8 3v6c0 4.5-3.5 8-8 9-4.5-1-8-4.5-8-9V6z', 'M8.5 12l2.5 2.5 4.5-5'],
  gift: ['M4 11h16v9H4z', 'M3 8h18v3H3z', 'M12 8v12', 'M12 8C9 8 8 5 10 4.5S12 7 12 8z', 'M12 8c3 0 4-3 2-3.5S12 7 12 8z'],
  droplet: ['M12 3s6 6.5 6 11a6 6 0 0 1-12 0c0-4.5 6-11 6-11z', 'M9.5 14.5a2.5 2.5 0 0 0 2 2.5'],
  flower: ['M12 9.5a2.5 2.5 0 1 0 .01 0', 'M12 7c-2-3 .5-4.5 0-4.5M12 7c2-3-.5-4.5 0-4.5', 'M14.5 12c3-.5 4.5 2 3 3.5s-3.5.5-3-3.5zM9.5 12c-3-.5-4.5 2-3 3.5s3.5.5 3-3.5z', 'M12 14.5c1.5 2.5 0 5-1 6.5M12 14.5c-1.5 2.5 0 5 1 6.5']
}
const filled = computed(() => props.name === 'star-filled')
</script>

<template>
  <svg
    xmlns="http://www.w3.org/2000/svg"
    viewBox="0 0 24 24"
    :width="size"
    :height="size"
    :fill="filled ? 'currentColor' : 'none'"
    stroke="currentColor"
    stroke-width="1.8"
    stroke-linecap="round"
    stroke-linejoin="round"
    class="inline-block shrink-0"
    :role="label ? 'img' : undefined"
    :aria-label="label || undefined"
    :aria-hidden="label ? undefined : 'true'"
    focusable="false"
  >
    <path v-for="(d, i) in paths[name]" :key="i" :d="d" />
  </svg>
</template>
