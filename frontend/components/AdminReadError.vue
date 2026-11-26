<script setup lang="ts">
defineProps<{ statusCode?: number }>()
</script>

<template>
  <div role="alert" class="rounded-xl border border-danger bg-white p-4">
    <p v-if="statusCode === 401">سجّل الدخول بحساب الإدارة لعرض الصفحة.</p>
    <p v-else-if="statusCode === 403">هذه الصفحة متاحة للمدير فقط.</p>
    <p v-else-if="statusCode === 404">الطلب غير موجود.</p>
    <p v-else>تعذر تحميل البيانات. أعد المحاولة بعد التحقق من الاتصال.</p>
    <NuxtLink v-if="statusCode === 401 || statusCode === 403" to="/account"
      class="mt-3 inline-flex min-h-10 items-center gap-1.5 rounded-full border border-brand/30 bg-white px-3 text-sm font-semibold text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand motion-safe:transition-colors">صفحة الحساب</NuxtLink>
  </div>
</template>
