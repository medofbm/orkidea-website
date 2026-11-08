<script setup lang="ts">
definePageMeta({ layout: 'admin' })
useSeoMeta({ title: 'إدارة الفئات' })

type Category = { categoryId: number, name: string, products: number }
type CategoryPage = { items: Category[], page: number, pages: number, total: number }
const url = '/api/admin/categories'
const search = ref('')
const query = ref({ search: '', page: 1 })
const { data, status, error, refresh } = useFetch<CategoryPage>(url, { query, server: false, cache: 'no-store' })
const selected = ref<Category>()
const name = ref('')
const saving = ref(false)
const needsRefresh = ref(false)
const failure = ref('')
const notice = ref('')
const disabled = computed(() => saving.value || needsRefresh.value || status.value !== 'success')

function edit(category?: Category) {
  selected.value = category
  name.value = category?.name ?? ''
  failure.value = ''
}

async function reload() {
  await refresh()
  needsRefresh.value = !!error.value
  if (!needsRefresh.value) edit()
}

async function save() {
  const value = name.value.trim()
  if (disabled.value || !value) return
  const category = selected.value
  saving.value = true
  notice.value = ''
  failure.value = ''
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const response = await $fetch.raw<{ success?: boolean, message?: string }>(
      category ? url + '/' + category.categoryId : url, {
        method: 'POST', retry: 0, ignoreResponseError: true,
        headers: { RequestVerificationToken: session.requestToken },
        body: category ? { name: value, expectedName: category.name } : { name: value }
      })
    if (!response.ok || response._data?.success !== true) {
      needsRefresh.value = response.status === 409 || response.status === 404 || response.status >= 500
      failure.value = response._data?.message ?? 'تعذر حفظ الفئة. راجع الاسم وحساب الدخول.'
      return
    }

    notice.value = category ? 'تم تعديل اسم الفئة.' : 'تمت إضافة الفئة.'
    await reload()
  } catch {
    needsRefresh.value = true
    failure.value = 'تعذر التحقق من الحفظ. حدّث القائمة وراجعها قبل إعادة المحاولة.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <section aria-labelledby="categories-title" class="mx-auto max-w-4xl">
    <NuxtLink to="/admin/products" class="inline-flex min-h-11 items-center text-purple-900 underline">العودة للمنتجات</NuxtLink>
    <h1 id="categories-title" class="mb-2 text-3xl font-bold">إدارة الفئات</h1>
    <p class="mb-5">الفئات هنا تظهر في خيارات المنتج. تغيير الاسم يبقى مرتبط بنفس المنتجات.</p>

    <p v-if="notice" role="status" class="mb-3 text-emerald-800">{{ notice }}</p>
    <p v-if="failure" role="alert" class="mb-3 text-red-800">{{ failure }}</p>
    <button v-if="needsRefresh" type="button" :disabled="saving || status === 'pending'"
      class="mb-4 min-h-11 rounded-lg border px-4" @click="reload()">تحديث قائمة الفئات</button>

    <form class="mb-6 rounded-xl bg-white p-4" @submit.prevent="save">
      <fieldset :disabled="disabled" class="flex flex-wrap items-end gap-3">
        <legend class="mb-3 text-xl font-bold">{{ selected ? 'تعديل اسم الفئة' : 'إضافة فئة' }}</legend>
        <label class="min-w-40 flex-1">اسم الفئة
          <input v-model="name" maxlength="150" required class="mt-1 block min-h-11 w-full rounded-lg border px-3">
        </label>
        <button type="submit" :disabled="!name.trim()" class="min-h-11 rounded-lg bg-purple-900 px-4 text-white disabled:opacity-60">
          {{ saving ? 'جاري الحفظ…' : 'حفظ الفئة' }}
        </button>
        <button v-if="selected" type="button" class="min-h-11 rounded-lg border px-4" @click="edit()">إلغاء التعديل</button>
      </fieldset>
    </form>

    <form class="mb-4 flex flex-wrap items-end gap-3 rounded-xl bg-white p-4"
      @submit.prevent="query = { search: search.trim(), page: 1 }">
      <label class="min-w-40 flex-1">بحث في الفئات
        <input v-model="search" type="search" maxlength="100" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
      </label>
      <button type="submit" :disabled="saving" class="min-h-11 rounded-lg border px-4">بحث</button>
    </form>
    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل الفئات…</p>
    <AdminReadError v-else-if="error" :status-code="error.statusCode" />
    <template v-else-if="data">
      <p class="mb-3" role="status">{{ data.total ? data.total + ' فئة' : 'ما فيش فئات مطابقة.' }}</p>
      <ul class="grid gap-3 sm:grid-cols-2">
        <li v-for="category in data.items" :key="category.categoryId"
          class="flex min-w-0 flex-wrap items-center justify-between gap-2 rounded-xl bg-white p-4">
          <div class="min-w-0">
            <p class="break-words font-semibold">{{ category.name }}</p>
            <p class="text-sm">{{ category.products }} منتج مرتبط</p>
          </div>
          <button type="button" :disabled="disabled" :aria-label="'تعديل فئة ' + category.name"
            class="min-h-11 px-2 text-purple-900 underline disabled:opacity-60" @click="edit(category)">تعديل الاسم</button>
        </li>
      </ul>
      <nav v-if="data.pages > 1" aria-label="صفحات الفئات" class="mt-4 flex items-center justify-between gap-3">
        <button type="button" :disabled="saving || data.page <= 1" class="min-h-11 rounded-lg border px-4 disabled:opacity-50"
          @click="query.page = data.page - 1">السابق</button>
        <span>{{ data.page }} من {{ data.pages }}</span>
        <button type="button" :disabled="saving || data.page >= data.pages" class="min-h-11 rounded-lg border px-4 disabled:opacity-50"
          @click="query.page = data.page + 1">التالي</button>
      </nav>
    </template>
  </section>
</template>
