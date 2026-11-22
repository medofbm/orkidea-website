<script setup lang="ts">
// Customer comment card. A first name is shown only when the API provides one (customer opted in).
const props = defineProps<{
  text: string
  product?: string
  source: 'facebook' | 'site'
  rating?: number
  /** First name, only when the customer allowed it. */
  name?: string | null
}>()

const stars = computed(() => Math.min(5, Math.max(0, Math.round(props.rating ?? 0))))
</script>

<template>
  <figure class="card relative flex h-full flex-col gap-3 p-6">
    <span class="pointer-events-none absolute end-5 top-1 select-none text-7xl font-black leading-none text-lilac" aria-hidden="true">&rdquo;</span>

    <div v-if="rating" class="flex gap-0.5 text-gold" role="img" :aria-label="`${stars} من 5`">
      <svg v-for="n in 5" :key="n" width="18" height="18" viewBox="0 0 24 24" :fill="n <= stars ? 'currentColor' : 'none'" stroke="currentColor" stroke-width="1.5" :class="n <= stars ? '' : 'opacity-40'" aria-hidden="true">
        <path d="M12 2.5l2.9 6 6.6.9-4.8 4.6 1.2 6.5L12 17.4l-5.9 3.1 1.2-6.5L2.5 9.4l6.6-.9z" stroke-linejoin="round" />
      </svg>
    </div>

    <blockquote class="relative z-10 flex-1 text-base leading-8 text-ink">{{ text }}</blockquote>

    <p v-if="name" class="text-sm font-semibold text-ink-soft">{{ name }}</p>

    <p v-if="product" class="text-sm font-bold text-brand">{{ product }}</p>

    <figcaption v-if="source === 'facebook'" class="inline-flex w-fit items-center gap-1.5 rounded-full bg-lavender px-3 py-1 text-xs font-bold text-brand-deep">
      <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M13.5 22v-8.2h2.8l.5-3.3h-3.3V8.4c0-.9.4-1.7 1.8-1.7h1.6V3.8S15.6 3.5 14.3 3.5c-2.6 0-4.3 1.6-4.3 4.4v2.6H7.2v3.3H10V22z" /></svg>
      من تعليقات فيسبوك
    </figcaption>
    <figcaption v-else class="inline-flex w-fit rounded-full bg-lavender px-3 py-1 text-xs font-bold text-brand-deep">من عملائنا</figcaption>
  </figure>
</template>
