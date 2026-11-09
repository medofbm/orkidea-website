<script setup lang="ts">
defineProps<{ statusCode?: number }>()
</script>

<template>
  <div role="alert" class="rounded-xl border border-red-200 bg-white p-4">
    <p v-if="statusCode === 401">سجّل الدخول بحساب الإدارة لعرض الصفحة.</p>
    <p v-else-if="statusCode === 403">هذه الصفحة متاحة للمدير فقط.</p>
    <p v-else-if="statusCode === 404">الطلب غير موجود.</p>
    <p v-else>تعذر تحميل البيانات. أعد المحاولة بعد التحقق من الاتصال.</p>
    <NuxtLink v-if="statusCode === 401 || statusCode === 403" to="/account"
      class="mt-3 inline-block min-h-11 text-purple-900 underline">صفحة الحساب</NuxtLink>
  </div>
</template>
