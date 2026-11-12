<script setup lang="ts">
import type { CatalogueProduct } from '~/types/catalogue'

const props = defineProps<{ product: CatalogueProduct & { startingPrice?: string } }>()
const imageFailed = ref(false)
watch(() => props.product.images[0], () => { imageFailed.value = false })
const brandImage = '/images/logo.jpg'
</script>

<template>
  <NuxtLink :to="'/products/' + product.id"
    class="group flex min-w-0 flex-col overflow-hidden rounded-2xl border border-purple-100 bg-white shadow-sm hover:border-purple-300 hover:shadow-md focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-800 motion-safe:transition motion-safe:duration-200 motion-safe:hover:-translate-y-0.5">
    <div class="grid aspect-square place-items-center overflow-hidden bg-purple-50">
      <img v-if="product.images[0] && product.images[0] !== brandImage && !imageFailed"
        :src="product.images[0]" @error="imageFailed = true" alt="" class="size-full object-contain" loading="lazy" width="320" height="320">
      <div v-else class="grid place-items-center gap-2 p-2 text-center text-xs text-purple-900">
        <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" class="size-8">
          <rect x="3" y="4" width="18" height="16" rx="3" />
          <path d="m3 16 5-5 4 4 3-3 6 6M15 8h.01" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
        <span>صورة غير متاحة</span>
      </div>
    </div>
    <div class="flex flex-1 flex-col gap-1 p-3 sm:p-4">
      <h3 class="text-sm leading-relaxed font-bold wrap-anywhere text-purple-950 sm:text-base">{{ product.name }}</h3>
      <p v-if="product.category" class="text-xs wrap-anywhere text-purple-800">{{ product.category }}</p>
      <p v-if="product.startingPrice !== undefined" class="mt-auto pt-2 text-sm text-purple-900">
        <span v-if="product.variants.length > 1">من </span><bdi class="font-bold">{{ product.startingPrice }}</bdi> د.ل
      </p>
      <span aria-hidden="true" class="pt-2 text-xs font-bold text-purple-900 group-hover:text-purple-700">عرض التفاصيل ←</span>
    </div>
  </NuxtLink>
</template>
