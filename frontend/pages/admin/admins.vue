<script setup lang="ts">
import type { AdminAdminsList } from '~/types/admin'
definePageMeta({ layout: 'admin', middleware: 'admin' })
useSeoMeta({ title: 'المديرين' })

type Admin = AdminAdminsList['items'][number]
const toast = useToast()
const confirm = useConfirm()
const { data, status, error, refresh } = useFetch<AdminAdminsList>('/api/admin/admins', { server: false, cache: 'no-store' })

const email = ref('')
const adding = ref(false)
const busy = ref<string[]>([])

async function post(path: string, body?: Record<string, string>) {
  const session = await $fetch<{ requestToken: string }>('/api/session')
  const response = await $fetch.raw<{ message?: string }>('/api/admin/' + path, {
    method: 'POST', retry: 0, ignoreResponseError: true, body,
    headers: { RequestVerificationToken: session.requestToken }
  })
  if (!response.ok) throw new Error(response._data?.message || 'تعذر حفظ التغيير. حاول مرة أخرى.')
  return response._data
}

async function add() {
  const value = email.value.trim()
  if (!value || adding.value) return
  const ok = await confirm({
    title: 'إضافة مدير؟',
    message: 'الحساب ' + value + ' بيقدر يشوف ويغيّر الطلبات والمنتجات والمخزون. لو ما عندهش كلمة مرور، بيوصله بريد لاختيارها.',
    confirmLabel: 'إضافة'
  })
  if (!ok) return
  adding.value = true
  try {
    const result = await post('admins', { email: value })
    toast.success(result?.message || 'تمت إضافة المدير.')
    email.value = ''
    await refresh()
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    adding.value = false
  }
}

async function remove(admin: Admin) {
  if (busy.value.includes(admin.id)) return
  const ok = await confirm({
    title: 'حذف المدير؟',
    message: 'الحساب ' + (admin.email ?? '') + ' ما يقدرش يدخل لوحة الإدارة بعد الآن. الحساب نفسه وطلباته يبقوا.',
    confirmLabel: 'حذف من المديرين', tone: 'danger'
  })
  if (!ok) return
  busy.value.push(admin.id)
  try {
    const result = await post('admins/' + encodeURIComponent(admin.id) + '/remove')
    toast.success(result?.message || 'تم حذف المدير.')
    await refresh()
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    busy.value = busy.value.filter(id => id !== admin.id)
  }
}
</script>

<template>
  <section aria-labelledby="admins-title" class="mx-auto max-w-3xl">
    <h1 id="admins-title" class="mb-2 text-3xl font-extrabold">المديرين</h1>
    <p class="mb-5 text-ink-soft">الحسابات اللي تقدر تدخل لوحة الإدارة.</p>

    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل المديرين…</p>
    <div v-else-if="error">
      <AdminReadError :status-code="error.statusCode" />
      <button type="button" class="btn-secondary mt-3" @click="refresh()">إعادة المحاولة</button>
    </div>
    <template v-else-if="data">
      <form v-if="data.isSuperAdmin" class="card mb-6 p-5" @submit.prevent="add">
        <h2 class="mb-1 text-lg font-bold">إضافة مدير</h2>
        <p class="mb-3 text-sm text-ink-soft">اكتب بريد الشخص. لو ما عندهش حساب، ننشئه ونرسله رابط يختار منه كلمة المرور بنفسه (صالح لمدة ساعتين).</p>
        <div class="flex flex-wrap items-end gap-3">
          <label class="min-w-56 flex-1">البريد الإلكتروني
            <input v-model="email" type="email" required maxlength="254" autocomplete="off" dir="ltr" class="field mt-1 block"
              placeholder="name@example.com">
          </label>
          <button type="submit" class="btn-primary" :disabled="adding || !email.trim()">
            <AppIcon name="user-plus" :size="18" />{{ adding ? 'جاري الإضافة…' : 'إضافة' }}
          </button>
        </div>
      </form>
      <p v-else class="card mb-6 p-4 text-sm text-ink-soft">
        <template v-if="data.superAdminConfigured">إضافة المديرين وحذفهم متاحة للمدير الرئيسي فقط.</template>
        <template v-else>ما فيش مدير رئيسي محدد في إعدادات الخادم، لهذا إضافة المديرين وحذفهم متوقفة.</template>
      </p>

      <p v-if="!data.items.length" class="card p-6 text-center text-ink-soft">ما فيش مديرين.</p>
      <ul v-else class="grid gap-3">
        <li v-for="admin in data.items" :key="admin.id" class="card flex flex-wrap items-center gap-3 p-4">
          <span class="grid size-10 shrink-0 place-items-center rounded-full bg-lavender text-brand" aria-hidden="true">
            <AppIcon :name="admin.isSuperAdmin ? 'shield' : 'user'" :size="18" />
          </span>
          <div class="min-w-0 flex-1">
            <bdi dir="ltr" class="block truncate font-semibold" :title="admin.email ?? undefined">{{ admin.email ?? '—' }}</bdi>
            <p class="mt-1 flex flex-wrap gap-1">
              <span class="badge" :class="admin.isSuperAdmin ? 'bg-brand text-white' : 'bg-lilac text-brand-deep'">{{ admin.isSuperAdmin ? 'مدير رئيسي' : 'مدير' }}</span>
              <span v-if="admin.isYou" class="badge">أنت</span>
              <span v-if="!admin.hasPassword" class="badge badge-status-pending">لم يختر كلمة المرور بعد</span>
            </p>
            <p class="mt-1 text-xs text-ink-soft">آخر دخول: {{ adminRelativeDate(admin.lastSignInAtUtc) }}</p>
          </div>
          <div class="flex w-full flex-wrap justify-end gap-2 sm:w-auto">
            <NuxtLink :to="'/admin/users/' + admin.id" class="btn-ghost text-sm">الحساب</NuxtLink>
            <button v-if="data.isSuperAdmin && !admin.isSuperAdmin && !admin.isYou" type="button" class="btn-ghost text-sm !text-danger"
              :disabled="busy.includes(admin.id)" :aria-label="'حذف ' + (admin.email ?? '') + ' من المديرين'" @click="remove(admin)">
              <AppIcon name="trash" :size="16" />حذف
            </button>
          </div>
        </li>
      </ul>
      <p class="mt-4 text-xs text-ink-soft">المدير المضاف يسجل خروج ودخول من جديد باش تظهر له لوحة الإدارة. المدير المحذوف يفقد الوصول خلال نصف ساعة على الأكثر.</p>
    </template>
  </section>
</template>
