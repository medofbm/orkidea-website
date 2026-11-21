<script setup lang="ts">
useSeoMeta({ title: 'سياسة الخصوصية | أوركيديا' })
</script>

<template>
  <article class="mx-auto max-w-3xl space-y-5 text-ink">
    <header class="rounded-3xl bg-gradient-hero p-6 text-center md:p-10">
      <span class="mx-auto mb-3 inline-flex size-14 items-center justify-center rounded-full bg-gradient-brand text-white shadow-(--shadow-glow)"><AppIcon name="shield" :size="28" /></span>
      <h1 class="section-title">سياسة الخصوصية</h1>
      <p class="mt-2 text-ink-soft">نوضح لك بكل شفافية كيف نتعامل مع بياناتك.</p>
    </header>

    <section aria-labelledby="privacy-orders" class="card space-y-2 p-5 leading-loose sm:p-7">
      <h2 id="privacy-orders" class="text-xl font-bold text-brand">بيانات الطلب</h2>
      <p>عند الطلب، نطلب اسمك ورقم التواصل وبيانات الاستلام المناسبة لطريقة التوصيل. تحتفظ أوركيديا بسجل الطلب، بما فيه المنتجات والمبالغ، لتجهيزه ومتابعته ومساعدتك في الطلبات المستقبلية.</p>
    </section>

    <section aria-labelledby="privacy-delivery" class="card space-y-2 p-5 leading-loose sm:p-7">
      <h2 id="privacy-delivery" class="text-xl font-bold text-brand">مشاركة بيانات التوصيل</h2>
      <p>تستخدم أوركيديا بيانات الطلب لإدارته. نشارك مع شركات التوصيل البيانات اللازمة لتسليم الشحنة، مثل الاسم ورقم التواصل ووجهة التسليم.</p>
    </section>

    <section aria-labelledby="privacy-marketing" class="card space-y-2 p-5 leading-loose sm:p-7">
      <h2 id="privacy-marketing" class="text-xl font-bold text-brand">العروض والمنتجات الجديدة</h2>
      <p>قد نوفّر مستقبلًا اشتراكًا اختياريًا في رسائل العروض والمنتجات الجديدة. نرسل هذه الرسائل بعد موافقتك، ونوضح طريقة إلغاء الاشتراك عند إتاحته.</p>
    </section>

    <section aria-labelledby="privacy-session" class="card space-y-2 p-5 leading-loose sm:p-7">
      <h2 id="privacy-session" class="text-xl font-bold text-brand">جلسة التسوق والحساب</h2>
      <p>نستخدم ملفات تعريف الارتباط لتشغيل السلة وإتاحة الإيصال داخل جلسة الطلب، ولإبقاء الحساب متصلًا عند تسجيل الدخول. تقدري تشتري كضيفة.</p>
    </section>

    <section aria-labelledby="privacy-contact" class="card space-y-2 p-5 leading-loose sm:p-7">
      <h2 id="privacy-contact" class="text-xl font-bold text-brand">الاستفسار أو التصحيح</h2>
      <p>للاستفسار عن بيانات طلبك أو طلب تصحيحها، راسلينا على أحد الحسابين الرسميين.</p>
      <div class="flex flex-wrap gap-3 pt-2">
        <a href="https://www.facebook.com/ORKIDEALY/" class="btn-secondary">فيسبوك أوركيديا</a>
        <a href="https://www.instagram.com/_orkidea_1/" class="btn-secondary">إنستغرام أوركيديا</a>
      </div>
    </section>
  </article>
</template>
