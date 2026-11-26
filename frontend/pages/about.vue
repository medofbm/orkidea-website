<script setup lang="ts">
useSeoMeta({
  title: 'عن أوركيديا',
  description: 'أوركيديا متجر ليبي للزيوت والصابون الطبيعي، معصور على البارد وصنع يدوي، والدفع عند الاستلام مع توصيل لكل ليبيا.'
})
const points = [
  { icon: 'cash', title: 'الدفع عند الاستلام', text: 'ما فيش دفع مسبق، تدفعي وقت ما يوصلك طلبك.' },
  { icon: 'truck', title: 'توصيل لكل ليبيا', text: 'نوصّل لين باب بيتك في كل المدن.' },
  { icon: 'leaf', title: 'طبيعي 100%', text: 'مكونات نقية بدون إضافات.' },
  { icon: 'hand-heart', title: 'صنع يدوي', text: 'نحضّرها بعناية وحب.' }
] as const
</script>

<template>
  <article class="mx-auto max-w-4xl space-y-6 text-ink">
    <header class="relative overflow-hidden rounded-3xl bg-gradient-hero p-6 text-center md:p-12">
      <span class="pointer-events-none absolute -top-10 end-6 size-40 rounded-full bg-lilac/40 motion-safe:animate-float" aria-hidden="true" />
      <img src="/brand/flower-purple.png" alt="" aria-hidden="true" width="96" height="96" class="relative mx-auto mb-3 size-20 object-contain md:size-24">
      <h1 class="section-title relative">عن أوركيديا</h1>
      <p class="relative mx-auto mt-3 max-w-xl text-lg leading-8 text-ink-soft">زيوت وصابون طبيعي من ليبيا، للعناية بك بكل بساطة.</p>
    </header>

    <section aria-labelledby="about-story" class="card-glass p-5 md:p-8">
      <h2 id="about-story" class="mb-3 text-2xl font-bold text-brand">قصتنا</h2>
      <div class="space-y-3 leading-8">
        <p>أوركيديا متجر متخصص في الزيوت والصابون الطبيعي من ليبيا. نختار المكونات بعناية ونعصر زيوتنا على البارد عشان تحافظ على فوايدها.</p>
        <p>كل منتج نحضّره بإيدينا، بدون مواد صناعية ولا إضافات ما تحتاجيها. هدفنا تلقي في بيتك منتجات تعتني بيك وبعائلتك وتثقي فيها.</p>
        <p>تطلبي من الموقع، ويوصلك الطلب لباب بيتك في كل ليبيا، وتدفعي عند الاستلام.</p>
      </div>
    </section>

    <section aria-labelledby="about-points">
      <h2 id="about-points" class="sr-only">ليش أوركيديا</h2>
      <ul class="grid gap-3 sm:grid-cols-2">
        <li v-for="p in points" :key="p.title" class="card-glass flex items-start gap-3 p-4">
          <span class="grid size-11 shrink-0 place-items-center rounded-full bg-lavender text-brand" aria-hidden="true"><AppIcon :name="p.icon" :size="22" /></span>
          <span><span class="block font-bold">{{ p.title }}</span><span class="text-sm leading-7 text-ink-soft">{{ p.text }}</span></span>
        </li>
      </ul>
    </section>

    <div class="flex flex-wrap justify-center gap-3 pb-2">
      <NuxtLink :to="{ path: '/', hash: '#catalogue-heading' }" class="storefront-action btn-primary">تسوقي الآن</NuxtLink>
      <NuxtLink :to="{ path: '/', hash: '#contact' }" class="storefront-action btn-secondary">تواصلي معنا</NuxtLink>
    </div>
  </article>
</template>
