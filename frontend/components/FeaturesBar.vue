<script setup lang="ts">
import type { AppIconName } from './AppIcon.vue'

withDefaults(defineProps<{
  items: { icon: string, title: string, text?: string }[]
  overlap?: boolean
}>(), { overlap: true })
</script>

<template>
  <section class="relative z-10 px-4" :class="overlap ? 'lg:-mt-12' : ''">
    <ul class="card-glass mx-auto grid max-w-6xl grid-cols-2 gap-4 p-4 lg:grid-cols-4 lg:p-6">
      <li v-for="(item, i) in items" :key="i" class="flex flex-col items-center gap-2 text-center sm:flex-row sm:text-start">
        <span class="inline-flex size-12 shrink-0 items-center justify-center rounded-full bg-lavender text-brand">
          <AppIcon :name="item.icon as AppIconName" :size="22" />
        </span>
        <span class="min-w-0">
          <span class="block font-bold text-ink">{{ item.title }}</span>
          <span v-if="item.text" class="block text-sm text-ink-soft">{{ item.text }}</span>
        </span>
      </li>
    </ul>
  </section>
</template>
