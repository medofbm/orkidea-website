<script setup lang="ts">
// Accordion with the product information sections. Texts for use and storage are generic and only shown for oils.
const props = defineProps<{ name: string; description: string | null; category: string | null }>()
const paragraphs = computed(() =>
  (props.description ?? '').split(/\r?\n+/).map(p => p.trim()).filter(Boolean))
const isOil = computed(() => /زيت|زيوت/.test(props.category ?? ''))
const sections = computed(() => {
  const list: { id: string; title: string; open: boolean }[] = []
  if (paragraphs.value.length) list.push({ id: 'about', title: 'عن المنتج', open: true })
  if (isOil.value) {
    list.push({ id: 'usage', title: 'طريقة الاستخدام', open: false })
    list.push({ id: 'storage', title: 'الحفظ', open: false })
  }
  list.push({ id: 'delivery', title: 'التوصيل والدفع', open: false })
  return list
})
</script>

<template>
  <section aria-labelledby="product-info-heading" class="mt-6">
    <h2 id="product-info-heading" class="sr-only">معلومات {{ name }}</h2>
    <div class="divide-y divide-lavender overflow-hidden rounded-2xl border border-lavender bg-white">
      <details v-for="section in sections" :key="section.id" :open="section.open" class="group">
        <summary class="flex min-h-12 cursor-pointer list-none items-center justify-between gap-3 px-4 py-3 text-lg font-bold text-ink hover:bg-lavender/40 focus-visible:outline-3 focus-visible:-outline-offset-3 focus-visible:outline-brand [&::-webkit-details-marker]:hidden">
          <span>{{ section.title }}</span>
          <span aria-hidden="true" class="text-brand motion-safe:transition-transform group-open:rotate-180">⌄</span>
        </summary>
        <div class="px-4 pb-4">
          <template v-if="section.id === 'about'">
            <p v-for="(paragraph, index) in paragraphs" :key="index" class="mb-3 leading-8 wrap-anywhere text-ink last:mb-0">{{ paragraph }}</p>
          </template>
          <ul v-else-if="section.id === 'usage'" class="list-disc space-y-2 ps-5 leading-8 text-ink">
            <li>استخدمي كم نقطة على البشرة أو الشعر ودلّكي بلطف.</li>
            <li>جرّبي كمية صغيرة على جزء صغير من الجلد أولًا قبل الاستخدام.</li>
            <li>للاستخدام الخارجي فقط.</li>
          </ul>
          <ul v-else-if="section.id === 'storage'" class="list-disc space-y-2 ps-5 leading-8 text-ink">
            <li>احفظيه بعيدًا عن الحرارة وأشعة الشمس المباشرة.</li>
            <li>أغلقي العبوة جيدًا بعد كل استخدام.</li>
          </ul>
          <div v-else class="space-y-2 leading-8 text-ink">
            <p>نوصّل لباب بيتك، والدفع عند الاستلام.</p>
            <NuxtLink to="/order-and-delivery" class="inline-flex min-h-11 items-center font-semibold text-brand underline underline-offset-4">تفاصيل الطلب والتوصيل</NuxtLink>
          </div>
        </div>
      </details>
    </div>
  </section>
</template>
