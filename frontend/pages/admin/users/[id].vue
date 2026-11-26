<script setup lang="ts">
import type { AdminUserDetails } from '~/types/admin'
definePageMeta({ layout: 'admin', middleware: 'admin' })
useSeoMeta({ title: 'تفاصيل الحساب' })

const route = useRoute()
const id = computed(() => String(route.params.id ?? ''))
const validId = computed(() => /^[A-Za-z0-9-]{1,64}$/.test(id.value))
const { data, error, status, refresh } = useFetch<AdminUserDetails>(() => '/api/admin/users/' + encodeURIComponent(id.value), {
  server: false, cache: 'no-store', immediate: validId.value
})
const user = computed(() => data.value?.user)
const roleLabel = computed(() => !user.value ? '' : user.value.isSuperAdmin ? 'مدير رئيسي' : user.value.isAdmin ? 'مدير' : 'زبونة')
function exact(iso: string | null) {
  return iso ? adminOrderDate(iso) : 'غير معروف'
}
</script>

<template>
  <section aria-labelledby="user-title">
    <NuxtLink to="/admin/users" class="btn-ghost mb-3 !px-3 text-sm">
      <AppIcon name="chevron-right" :size="16" />كل الحسابات
    </NuxtLink>

    <p v-if="!validId" class="card p-6 text-center text-ink-soft">الحساب غير موجود.</p>
    <p v-else-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل الحساب…</p>
    <div v-else-if="error">
      <p v-if="error.statusCode === 404" class="card p-6 text-center text-ink-soft">الحساب غير موجود.</p>
      <template v-else>
        <AdminReadError :status-code="error.statusCode" />
        <button type="button" class="btn-secondary mt-3" @click="refresh()">إعادة المحاولة</button>
      </template>
    </div>
    <template v-else-if="user && data">
      <div class="card mb-5 p-5">
        <h1 id="user-title" class="break-all text-2xl font-extrabold"><bdi dir="ltr">{{ user.email ?? '—' }}</bdi></h1>
        <p class="mt-2 flex flex-wrap gap-1">
          <span class="badge" :class="user.isSuperAdmin ? 'bg-brand text-white' : user.isAdmin ? 'bg-lilac text-brand-deep' : ''">{{ roleLabel }}</span>
          <span class="badge" :class="user.emailConfirmed ? 'badge-status-delivered' : ''">{{ user.emailConfirmed ? 'البريد مؤكد' : 'البريد غير مؤكد' }}</span>
        </p>
        <dl class="mt-4 grid gap-x-6 gap-y-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
          <div><dt class="text-ink-soft">الهاتف</dt><dd class="font-semibold"><bdi dir="ltr">{{ user.phoneNumber || 'غير مسجل' }}</bdi></dd></div>
          <div><dt class="text-ink-soft">تاريخ التسجيل</dt><dd class="font-semibold">{{ exact(user.createdAtUtc) }}</dd></div>
          <div><dt class="text-ink-soft">آخر دخول</dt><dd class="font-semibold">{{ adminRelativeDate(user.lastSignInAtUtc) }}<span v-if="user.lastSignInAtUtc" class="block text-xs font-normal text-ink-soft">{{ exact(user.lastSignInAtUtc) }}</span></dd></div>
          <div><dt class="text-ink-soft">مرات الدخول</dt><dd class="font-semibold"><bdi>{{ user.signInCount ?? 'غير معروف' }}</bdi></dd></div>
          <div><dt class="text-ink-soft">عدد الطلبات</dt><dd class="font-semibold"><bdi>{{ user.orderCount }}</bdi></dd></div>
          <div><dt class="text-ink-soft">مجموع المشتريات (بدون الملغية)</dt><dd class="font-semibold"><bdi>{{ formatPrice(user.totalSpent) }}</bdi> د.ل</dd></div>
        </dl>
        <div v-if="user.phoneNumber" class="mt-4 flex flex-wrap gap-2">
          <a :href="'tel:+' + adminPhoneDigits(user.phoneNumber)" class="btn-secondary text-sm"><AppIcon name="phone" :size="16" />اتصال</a>
          <a :href="'https://wa.me/' + adminPhoneDigits(user.phoneNumber)" target="_blank" rel="noopener noreferrer" class="btn-secondary text-sm">
            <AppIcon name="whatsapp" :size="16" />واتساب</a>
        </div>
      </div>

      <h2 class="mb-3 text-xl font-bold">طلبات الحساب</h2>
      <p v-if="!data.orders.length" class="card p-6 text-center text-ink-soft">ما عندهاش طلبات من الحساب. (الطلبات كضيف أو من فيسبوك ما تظهرش هنا.)</p>
      <ul v-else class="grid gap-3">
        <li v-for="order in data.orders" :key="order.id">
          <NuxtLink :to="'/admin/orders/' + order.id"
            class="card flex flex-wrap items-center justify-between gap-3 p-4 hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand">
            <span>
              <span class="block font-semibold">طلب #<bdi>{{ order.id }}</bdi></span>
              <time class="text-xs text-ink-soft" :datetime="order.orderDate">{{ adminOrderDate(order.orderDate) }}</time>
            </span>
            <span class="flex flex-wrap items-center gap-3">
              <span class="badge" :class="adminOrderStatusClass(order.status)">{{ adminOrderStatus(order.status) }}</span>
              <span class="font-semibold"><bdi>{{ formatPrice(order.total) }}</bdi> د.ل</span>
            </span>
          </NuxtLink>
        </li>
      </ul>
    </template>
  </section>
</template>
