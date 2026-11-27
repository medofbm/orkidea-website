<script setup lang="ts">
const { facebookUrl, instagramUrl } = useRuntimeConfig().public
const siteUrl = useRuntimeConfig().public.siteUrl
useSeoMeta({
  title: 'سياسة الطلب والتوصيل | أوركيديا',
  description: 'كيف تطلبي من أوركيديا: الدفع عند الاستلام، مدة التوصيل ورسومه لكل المدن في ليبيا.',
  ogTitle: 'سياسة الطلب والتوصيل | أوركيديا',
  ogUrl: siteUrl + '/order-and-delivery'
})
useHead({ link: [{ rel: 'canonical', href: siteUrl + '/order-and-delivery' }] })
</script>

<template>
  <article class="mx-auto max-w-3xl space-y-5 text-ink">
    <header class="rounded-3xl bg-gradient-hero p-6 text-center md:p-10">
      <span class="mx-auto mb-3 inline-flex size-14 items-center justify-center rounded-full bg-gradient-brand text-white shadow-(--shadow-glow)"><AppIcon name="truck" :size="28" /></span>
      <h1 class="section-title">سياسة الطلب والتوصيل</h1>
      <p class="mt-2 text-ink-soft">كل اللي تحتاجي تعرفيه عن الطلب والدفع والتوصيل في مكان واحد.</p>
    </header>

    <section aria-labelledby="policy-payment" class="card space-y-2 p-5 leading-loose sm:p-7">
      <h2 id="policy-payment" class="text-xl font-bold text-brand">الطلب والدفع</h2>
      <p>تقدري تطلبي من المتجر كضيفة. اختاري المنتج والحجم والكمية، وأدخلي اسمك ورقم التواصل وبيانات الاستلام. قبل التأكيد، راجعي المنتجات ورسوم التوصيل والإجمالي بالدينار الليبي. الدفع عند الاستلام. بعد إرسال الطلب، احتفظي برقم الطلب الظاهر في الإيصال لمتابعته أو طلب المساعدة.</p>
    </section>

    <section aria-labelledby="policy-delivery" class="card space-y-2 p-5 leading-loose sm:p-7">
      <h2 id="policy-delivery" class="text-xl font-bold text-brand">خيارات التوصيل</h2>
      <p>تظهر خيارات الاستلام ورسومها عند إتمام الطلب حسب الوجهة. إذا توفر الاستلام من مكتب، تقدري تختاريه من الخيارات المعروضة. لو منطقتك مش ظاهرة أو ما ظهرش سعر التوصيل، تواصلي مع أوركيديا للتأكد من إمكانية التوصيل. مدة الوصول تعتمد على الوجهة وترتيب الشحنة.</p>
    </section>

    <section aria-labelledby="policy-changes" class="card space-y-2 p-5 leading-loose sm:p-7">
      <h2 id="policy-changes" class="text-xl font-bold text-brand">تعديل الطلب أو إلغاؤه</h2>
      <p>لو تبي تعدّلي أو تلغي الطلب، تواصلي معنا في أقرب وقت واذكري رقمه. نراجع حالة التجهيز والشحن ونوضح لك الخيارات المتاحة قبل تنفيذ أي تعديل.</p>
    </section>

    <section aria-labelledby="policy-problems" class="card space-y-2 p-5 leading-loose sm:p-7">
      <h2 id="policy-problems" class="text-xl font-bold text-brand">مشكلة عند الاستلام</h2>
      <p>لو وصل منتج مختلف أو عبوة متضررة أو فيها تسريب، تواصلي معنا في أقرب وقت مع رقم الطلب وصورة توضح المشكلة إن أمكن. احتفظي بالعبوة إلى أن نراجع الحالة ونوضح لك الخطوة التالية.</p>
    </section>

    <section aria-labelledby="policy-contact" class="card space-y-2 p-5 sm:p-7">
      <h2 id="policy-contact" class="text-xl font-bold text-brand">التواصل</h2>
      <div class="flex flex-wrap gap-3 pt-2">
        <a v-if="facebookUrl" :href="facebookUrl" class="btn-secondary">فيسبوك أوركيديا</a>
        <a v-if="instagramUrl" :href="instagramUrl" class="btn-secondary">إنستغرام أوركيديا</a>
      </div>
    </section>
  </article>
</template>
