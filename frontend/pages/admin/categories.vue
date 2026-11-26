<script setup lang="ts">
definePageMeta({ layout: 'admin', middleware: 'admin' })
useSeoMeta({ title: 'إدارة الفئات' })

type Category = { categoryId: number, name: string, products: number, isArchived?: boolean }
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
const toast = useToast()
const confirmDialog = useConfirm()
const notice = ref('')
const ordered = computed(() => [...(data.value?.items ?? [])].sort((a, b) => Number(!!a.isArchived) - Number(!!b.isArchived)))
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

const creation = useIdempotencyKey()
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
        // Creating a category carries an Idempotency-Key so a repeat never makes a duplicate.
        headers: category ? { RequestVerificationToken: session.requestToken }
          : { RequestVerificationToken: session.requestToken, 'Idempotency-Key': creation.get({ name: value }) },
        body: category ? { name: value, expectedName: category.name } : { name: value }
      })
    if (!response.ok || response._data?.success !== true) {
      needsRefresh.value = response.status === 409 || response.status === 404 || response.status >= 500
      failure.value = response._data?.message ?? 'تعذر حفظ الفئة. راجع الاسم وحساب الدخول.'
      return
    }

    if (!category) creation.reset()
    notice.value = category ? 'تم تعديل اسم الفئة.' : 'تمت إضافة الفئة.'
    await reload()
  } catch {
    needsRefresh.value = true
    failure.value = 'تعذر التحقق من الحفظ. حدّث القائمة وراجعها قبل إعادة المحاولة.'
  } finally {
    saving.value = false
  }
}

async function toggleArchive(category: Category) {
  if (disabled.value) return
  const archived = !category.isArchived
  saving.value = true
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const response = await $fetch.raw<{ message?: string }>(url + '/' + category.categoryId + '/archive', {
      method: 'POST', retry: 0, ignoreResponseError: true, body: { archived },
      headers: { RequestVerificationToken: session.requestToken }
    })
    if (!response.ok) {
      toast.error(response._data?.message ?? 'تعذر تحديث أرشفة الفئة.')
      return
    }
    toast.success(archived ? 'تمت أرشفة الفئة.' : 'تم إلغاء أرشفة الفئة.')
    await refresh()
  } catch {
    toast.error('تعذر تحديث أرشفة الفئة. حاول مرة أخرى.')
  } finally {
    saving.value = false
  }
}

async function removeCategory(category: Category) {
  if (disabled.value) return
  const ok = await confirmDialog({ title: 'حذف الفئة؟', message: 'ستُحذف فئة «' + category.name + '» نهائيًا.', confirmLabel: 'حذف', tone: 'danger' })
  if (!ok) return
  saving.value = true
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const response = await $fetch.raw<{ message?: string }>(url + '/' + category.categoryId + '/delete', {
      method: 'POST', retry: 0, ignoreResponseError: true,
      headers: { RequestVerificationToken: session.requestToken }
    })
    if (!response.ok) {
      toast.error(response._data?.message ?? 'تعذر حذف الفئة.')
      return
    }
    toast.success('تم حذف الفئة.')
    if (selected.value?.categoryId === category.categoryId) edit()
    await refresh()
  } catch {
    toast.error('تعذر حذف الفئة. حاول مرة أخرى.')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <section aria-labelledby="categories-title" class="mx-auto max-w-6xl">
    <NuxtLink to="/admin/products" class="mb-3 inline-flex min-h-10 items-center gap-1.5 rounded-full border border-brand/30 bg-white px-3 text-sm font-semibold text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand motion-safe:transition-colors"><AppIcon name="chevron-right" :size="16" />العودة للمنتجات</NuxtLink>
    <h1 id="categories-title" class="mb-2 text-3xl font-bold">إدارة الفئات</h1>
    <p class="mb-5 text-ink-soft">الفئات هنا تظهر في خيارات المنتج. تغيير الاسم يبقى مرتبط بنفس المنتجات.</p>

    <p v-if="notice" role="status" class="mb-3 text-success">{{ notice }}</p>
    <p v-if="failure" role="alert" class="mb-3 text-danger">{{ failure }}</p>
    <button v-if="needsRefresh" type="button" :disabled="saving || status === 'pending'"
      class="btn-secondary mb-4" @click="reload()">تحديث قائمة الفئات</button>

    <div class="mb-6 grid gap-4 lg:grid-cols-2">
      <form class="card p-4 sm:p-6" @submit.prevent="save">
        <fieldset :disabled="disabled" class="flex flex-wrap items-end gap-3">
          <legend class="mb-3 text-xl font-bold">{{ selected ? 'تعديل اسم الفئة' : 'إضافة فئة' }}</legend>
          <label class="min-w-40 flex-1 font-medium">اسم الفئة
            <input v-model="name" maxlength="150" required class="field mt-1 block min-h-11 w-full">
          </label>
          <button type="submit" :disabled="!name.trim()" class="btn-primary min-h-11 disabled:opacity-60">
            {{ saving ? 'جاري الحفظ…' : 'حفظ الفئة' }}
          </button>
          <button v-if="selected" type="button" class="btn-secondary min-h-11" @click="edit()">إلغاء التعديل</button>
        </fieldset>
      </form>

      <form class="card flex flex-wrap items-end gap-3 p-4 sm:p-6"
        @submit.prevent="query = { search: search.trim(), page: 1 }">
        <label class="min-w-40 flex-1 font-medium">بحث في الفئات
          <input v-model="search" type="search" maxlength="100" class="field mt-1 block min-h-11 w-full">
        </label>
        <button type="submit" :disabled="saving" class="btn-secondary min-h-11">بحث</button>
      </form>
    </div>

    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل الفئات…</p>
    <AdminReadError v-else-if="error" :status-code="error.statusCode" />
    <template v-else-if="data">
      <p class="mb-3 text-ink-soft" role="status">{{ data.total ? data.total + ' فئة' : 'ما فيش فئات مطابقة.' }}</p>
      <ul class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <li v-for="category in ordered" :key="category.categoryId"
          class="card flex min-w-0 flex-col gap-3 p-4" :class="category.isArchived ? 'opacity-60' : ''">
          <div class="min-w-0">
            <p class="break-words font-bold">{{ category.name }}
              <span v-if="category.isArchived" class="badge badge-status-pending ms-1">مؤرشفة</span></p>
            <span class="chip mt-2 inline-flex text-sm">{{ category.products }} {{ category.products === 1 ? 'منتج' : 'منتجات' }}</span>
          </div>
          <div class="mt-auto flex flex-wrap gap-1">
            <button type="button" :disabled="disabled" :aria-label="'تعديل فئة ' + category.name"
              class="btn-ghost min-h-11 px-3 text-sm disabled:opacity-60" @click="edit(category)">تعديل الاسم</button>
            <button type="button" :disabled="disabled" :aria-label="(category.isArchived ? 'إلغاء أرشفة فئة ' : 'أرشفة فئة ') + category.name"
              class="btn-ghost min-h-11 px-3 text-sm disabled:opacity-60" @click="toggleArchive(category)">{{ category.isArchived ? 'إلغاء الأرشفة' : 'أرشفة' }}</button>
            <button type="button" :disabled="disabled" :aria-label="'حذف فئة ' + category.name"
              class="btn-ghost min-h-11 px-3 text-sm !text-danger disabled:opacity-60" @click="removeCategory(category)">حذف</button>
          </div>
        </li>
      </ul>
      <nav v-if="data.pages > 1" aria-label="صفحات الفئات" class="mt-4 flex items-center justify-between gap-3">
        <button type="button" :disabled="saving || data.page <= 1" class="btn-secondary min-h-11 disabled:opacity-50"
          @click="query.page = data.page - 1">السابق</button>
        <span>{{ data.page }} من {{ data.pages }}</span>
        <button type="button" :disabled="saving || data.page >= data.pages" class="btn-secondary min-h-11 disabled:opacity-50"
          @click="query.page = data.page + 1">التالي</button>
      </nav>
    </template>
  </section>
</template>
