<script setup lang="ts">
// Shows a server-provided delivery price. When the server sent an original price (free-delivery promotion),
// it shows «مجاني» with the normal price struck through. Never computes a price itself.
defineProps<{ price: string, original?: string | null }>()
</script>

<template>
  <span v-if="original" class="inline-flex items-center gap-1.5">
    <s class="text-xs text-ink-soft"><bdi>{{ formatPrice(original) }}</bdi> د.ل</s>
    <span class="font-bold text-success">مجاني</span>
  </span>
  <span v-else><bdi>{{ formatPrice(price) }}</bdi> د.ل</span>
</template>
