<script setup lang="ts">
import type { AppIconName } from './AppIcon.vue'

defineOptions({ inheritAttrs: false })
const props = withDefaults(defineProps<{
  title: string
  highlight?: string
  eyebrow?: string
  icon?: AppIconName
  subtitle?: string
  align?: 'center' | 'start'
  as?: 'h1' | 'h2' | 'h3' | 'h4'
  /** 'light' is for sections on dark brand gradients (white title, lilac highlight). */
  tone?: 'default' | 'light'
}>(), { align: 'center', as: 'h2', tone: 'default' })
const centered = computed(() => props.align === 'center')
const light = computed(() => props.tone === 'light')
</script>

<template>
  <header class="flex flex-col gap-3" :class="centered ? 'items-center text-center' : 'items-start text-start'">
    <span v-if="eyebrow" class="section-eyebrow">
      <AppIcon v-if="icon" :name="icon" :size="16" />
      {{ eyebrow }}
    </span>
    <span v-else-if="icon" class="inline-flex size-12 items-center justify-center rounded-full"
      :class="light ? 'bg-white/15 text-white ring-1 ring-white/30' : 'bg-gradient-brand text-white shadow-(--shadow-glow)'">
      <AppIcon :name="icon" :size="22" />
    </span>
    <component :is="as" v-bind="$attrs" class="section-title" :class="light ? 'text-white' : ''">
      {{ title }}<template v-if="highlight">{{ ' ' }}<span :class="light ? 'text-lilac' : 'text-gradient-brand'">{{ highlight }}</span></template>
    </component>
    <p v-if="subtitle" class="max-w-2xl" :class="light ? 'text-white/85' : 'text-ink-soft'">{{ subtitle }}</p>
    <slot />
  </header>
</template>
