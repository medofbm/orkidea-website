<script setup lang="ts">
import type { AppIconName } from './AppIcon.vue'

defineProps<{ items: { value: string, label: string, icon?: string }[] }>()
</script>

<template>
  <dl class="flex flex-wrap items-stretch justify-center gap-y-4">
    <div
      v-for="(item, i) in items"
      :key="i"
      class="flex min-w-24 flex-1 basis-24 flex-col items-center gap-0.5 px-3 text-center"
      :class="i > 0 ? 'border-s border-brand/15' : ''"
    >
      <AppIcon v-if="item.icon" :name="item.icon as AppIconName" :size="18" class="text-brand-soft" />
      <dd class="order-1 text-xl font-extrabold text-brand md:text-2xl">{{ item.value }}</dd>
      <dt class="order-2 text-sm text-ink-soft">{{ item.label }}</dt>
    </div>
  </dl>
</template>
