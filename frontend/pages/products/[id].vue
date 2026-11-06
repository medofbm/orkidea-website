<script setup lang="ts">
import type { CatalogueProduct } from '~/types/catalogue'
const route = useRoute()
const { data: product, error, refresh } = await useFetch<CatalogueProduct>(
  () => useRuntimeConfig().public.apiBase + '/products/' + route.params.id
)
const selectedImage = ref(0)
const selectedVariant = ref(0)
const currentVariant = computed(() => product.value?.variants[selectedVariant.value])
useSeoMeta({ title: () => product.value?.name ?? 'المنتج', description: () => product.value?.description ?? '' })
</script>

<template>
  <div v-if="error" role="alert">
    <p>{{ error.statusCode === 404 ? 'المنتج غير موجود.' : 'تعذر تحميل المنتج.' }}</p>
    <button class="mt-3 min-h-11 rounded-lg bg-purple-800 px-4 text-white" @click="refresh()">إعادة المحاولة</button>
  </div>
  <article v-else-if="product" class="grid gap-6 md:grid-cols-2">
    <section aria-label="صور المنتج">
      <img v-if="product.images[selectedImage]" :src="product.images[selectedImage]" :alt="product.name"
        class="h-64 w-full rounded-2xl bg-white object-contain md:h-96" width="480" height="480">
      <div v-if="product.images.length > 1" class="mt-3 flex flex-wrap gap-2">
        <button v-for="(image, index) in product.images" :key="image" type="button"
          :aria-label="'عرض الصورة ' + (index + 1)" :aria-pressed="selectedImage === index"
          class="rounded-lg border-2 bg-white p-1" :class="selectedImage === index ? 'border-purple-700' : 'border-transparent'"
          @click="selectedImage = index">
          <img :src="image" alt="" class="h-14 w-14 object-contain" width="56" height="56">
        </button>
      </div>
    </section>
    <section>
      <p v-if="product.category" class="text-purple-700">{{ product.category }}</p>
      <h1 class="mt-2 text-3xl font-bold">{{ product.name }}</h1>
      <p v-if="currentVariant" class="my-5 text-2xl font-semibold text-purple-900">
        {{ currentVariant.price }} د.ل
      </p>
      <fieldset v-if="product.variants.length" class="mb-6">
        <legend class="mb-2 font-semibold">اختاري الحجم</legend>
        <div class="flex flex-wrap gap-2">
          <label v-for="(variant, index) in product.variants" :key="variant.id" class="min-h-11 rounded-lg border bg-white px-3 py-2">
            <input v-model="selectedVariant" type="radio" name="variant" :value="index">
            {{ variant.size }}
          </label>
        </div>
      </fieldset>
      <p class="whitespace-pre-line leading-8">{{ product.description }}</p>
      <NuxtLink to="/" class="mt-6 inline-block min-h-11 text-purple-900 underline">متابعة التسوق</NuxtLink>
    </section>
  </article>
</template>
