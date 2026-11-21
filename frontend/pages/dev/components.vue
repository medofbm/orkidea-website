<script setup lang="ts">
// Internal component gallery for visual review during development only.
// Production builds return 404 for this route.
import { testimonials } from '~/data/testimonials'

if (!import.meta.dev) {
  throw createError({ statusCode: 404, statusMessage: 'Not Found' })
}

useSeoMeta({ title: 'مكوّنات أوركيديا (تطوير)', robots: 'noindex, nofollow' })

const toast = useToast()
const confirm = useConfirm()
const confirmResult = ref('')

const stats = [
  { value: '100%', label: 'طبيعي' },
  { value: 'يدوي', label: 'صنع' },
  { value: '5', label: 'تقييم', icon: 'star-filled' }
]
const features = [
  { icon: 'leaf', title: '100% طبيعي', text: 'زيوت وصابون من مكونات طبيعية' },
  { icon: 'hand-heart', title: 'صنع يدوي', text: 'نعبي ونجهز كل طلب بإيدينا' },
  { icon: 'truck', title: 'توصيل', text: 'نوصلك طلبك لباب البيت' },
  { icon: 'cash', title: 'الدفع عند الاستلام', text: 'ادفعي لما يوصلك طلبك' }
]
const categories = [
  { name: 'الزيوت', to: '/?category=oils', count: 3 },
  { name: 'الصابون', to: '/?category=soap', count: 1 }
]

async function askConfirm(tone: 'danger' | 'default') {
  const ok = await confirm({
    title: tone === 'danger' ? 'حذف المنتج؟' : 'تأكيد الطلب؟',
    message: 'هذا مثال لنافذة التأكيد.',
    confirmLabel: tone === 'danger' ? 'حذف' : 'تأكيد',
    tone
  })
  confirmResult.value = ok ? 'تم التأكيد' : 'تم الإلغاء'
}
</script>

<template>
  <div class="mx-auto max-w-6xl space-y-16 px-4 py-10">
    <SectionHeader id="dev-h" icon="flower" title="مكوّنات" highlight="أوركيديا" subtitle="صفحة مراجعة داخلية للمكوّنات المشتركة. لا تظهر في النسخة المنشورة." as="h1" />

    <section class="space-y-6">
      <SectionHeader eyebrow="الإحصائيات" icon="sparkle" title="صف" highlight="الإحصائيات" align="start" />
      <StatsRow :items="stats" />
    </section>

    <section class="space-y-6 bg-gradient-hero rounded-[var(--radius-card)] p-6 pb-0">
      <SectionHeader title="شريط" highlight="الميزات" />
      <div class="h-16" />
      <FeaturesBar :items="features" />
    </section>

    <section class="space-y-6">
      <SectionHeader icon="flower" title="تسوقي" highlight="حسب الفئة" />
      <div class="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <CategoryCard v-for="c in categories" :key="c.name" v-bind="c" />
      </div>
    </section>

    <section class="space-y-6 rounded-[var(--radius-card)] bg-gradient-band px-4 py-10">
      <SectionHeader icon="heart" title="ماذا يقول" highlight="زبائننا" tone="light" />
      <AppCarousel :items="testimonials" label="آراء الزبائن" :per-view="{ base: 1, sm: 2, lg: 3 }" :autoplay="4500" loop dots arrows dots-tone="light">
        <template #item="{ item }">
          <TestimonialCard v-bind="item" />
        </template>
      </AppCarousel>
    </section>

    <section class="space-y-4">
      <SectionHeader title="التنبيهات" highlight="والتأكيد" align="start" />
      <div class="flex flex-wrap gap-3">
        <button class="btn-primary" type="button" @click="toast.success('تمت إضافة زيت الحبة السوداء للسلة', { action: { label: 'افتحي السلة', to: '/cart' } })">تنبيه نجاح</button>
        <button class="btn-secondary" type="button" @click="toast.error('تعذر تحديث السلة.')">تنبيه خطأ</button>
        <button class="btn-secondary" type="button" @click="toast.show('معلومة صغيرة')">تنبيه معلومة</button>
        <button class="btn-secondary" type="button" @click="askConfirm('danger')">تأكيد حذف</button>
        <button class="btn-secondary" type="button" @click="askConfirm('default')">تأكيد عادي</button>
      </div>
      <p class="text-ink-soft" role="status">{{ confirmResult }}</p>
    </section>

    <section class="space-y-4">
      <SectionHeader title="الأيقونات" align="start" />
      <div class="flex flex-wrap gap-4 text-brand">
        <AppIcon v-for="n in ['leaf','hand-heart','star','star-filled','truck','cash','phone','whatsapp','facebook','instagram','cart','bag','user','user-plus','login','home','check','check-circle','x','chevron-left','chevron-right','heart','mail','menu','sparkle','shield','gift','droplet','flower']" :key="n" :name="n as any" :size="28" :label="n" />
      </div>
    </section>
  </div>
</template>
