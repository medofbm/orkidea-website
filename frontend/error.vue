<script setup lang="ts">
interface ErrorProps { statusCode?: number, statusMessage?: string, message?: string }
const props = defineProps<{ error: ErrorProps }>()
const isNotFound = computed(() => props.error?.statusCode === 404)
const title = computed(() => isNotFound.value ? 'الصفحة اللي تدوري عليها مش موجودة' : 'صار خطأ غير متوقع')
const hint = computed(() => isNotFound.value
  ? 'يمكن الرابط تغيّر أو انحذفت الصفحة. ارجعي للرئيسية وكملي تسوقك.'
  : 'جرّبي مرة ثانية بعد لحظات، أو ارجعي للرئيسية.')
useSeoMeta({ title: () => `${title.value} | أوركيديا`, robots: 'noindex, nofollow' })
</script>

<template>
  <NuxtLayout>
    <section class="relative isolate overflow-hidden rounded-3xl bg-gradient-hero px-4 py-14 text-center md:py-24" aria-labelledby="error-title">
      <span class="pointer-events-none absolute -top-16 -start-16 size-56 rounded-full bg-lilac/50 blur-2xl motion-safe:animate-float" aria-hidden="true" />
      <span class="pointer-events-none absolute -bottom-20 -end-12 size-64 rounded-full bg-blush blur-2xl motion-safe:animate-float" aria-hidden="true" />
      <div class="relative mx-auto max-w-xl motion-safe:animate-fade-in-up">
        <span class="mx-auto mb-4 inline-flex size-20 items-center justify-center rounded-full bg-gradient-brand text-white shadow-(--shadow-glow)"><AppIcon name="flower" :size="40" /></span>
        <p v-if="isNotFound" class="text-gradient-brand text-7xl font-extrabold md:text-9xl" dir="ltr" aria-hidden="true">404</p>
        <h1 id="error-title" class="section-title mt-2">{{ title }}</h1>
        <p class="mt-3 text-ink-soft">{{ hint }}</p>
        <div class="mt-8 flex flex-col items-stretch justify-center gap-3 sm:flex-row sm:items-center">
          <button type="button" class="btn-primary" @click="clearError({ redirect: '/' })">الرئيسية</button>
          <NuxtLink to="/#catalogue-heading" class="btn-secondary" @click="clearError()">تسوقي الآن</NuxtLink>
        </div>
      </div>
    </section>
  </NuxtLayout>
</template>
