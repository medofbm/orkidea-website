<script setup lang="ts">
import type { AdminProductDetails } from '~/types/admin'

definePageMeta({ layout: 'admin', middleware: 'admin', key: route => route.path })
useSeoMeta({ title: 'تعديل المنتج' })

const id = String(useRoute().params.id)
const isNew = id === 'new'
const router = useRouter()
const toast = useToast()
const confirmDialog = useConfirm()
const url = '/api/admin/products' + (isNew ? '' : '/' + id)
const { data, status, error, refresh } = useFetch<AdminProductDetails>(url, {
  key: 'admin-product-' + id, immediate: !isNew, server: false, cache: 'no-store'
})
const categories = useFetch<{ categoryId: number, name: string }[]>('/api/admin/products/categories', {
  server: false, cache: 'no-store'
})
const form = reactive({ name: '', description: '', categoryId: '', imageUrl: '' })
const version = ref('')
const stored = ref<string[]>([])
const removed = ref<string[]>([])
type Preview = { file: File, url: string }
const cover = shallowRef<Preview>()
const gallery = shallowRef<Preview[]>([])
const selected = ref<AdminProductDetails['variants'][number]>()
const size = ref('')
const price = ref('')
const saving = ref(false)
const needsRefresh = ref(false)
const failure = ref('')
const notice = ref('')
const imageError = ref('')
const disabled = computed(() => saving.value || needsRefresh.value)
const visibleStored = computed(() => stored.value.filter(url => !removed.value.includes(url)))
const maxUpload = 5 * 1024 * 1024

watch(data, product => {
  if (product && !version.value) loadProduct(product)
}, { immediate: true })

function loadProduct(product: AdminProductDetails) {
  form.name = product.name
  form.description = product.description ?? ''
  form.categoryId = product.categoryId?.toString() ?? ''
  form.imageUrl = product.imageUrl ?? ''
  version.value = product.version
  stored.value = product.gallery
  removed.value = []
  clearUploads()
}

function clearUploads() {
  if (cover.value) URL.revokeObjectURL(cover.value.url)
  gallery.value.forEach(image => URL.revokeObjectURL(image.url))
  cover.value = undefined
  gallery.value = []
  imageError.value = ''
}
onBeforeUnmount(clearUploads)

function selectImages(event: Event, asCover: boolean) {
  const input = event.target as HTMLInputElement
  const files = Array.from(input.files ?? [])
  input.value = ''
  if (!files.length) return

  imageError.value = ''
  const total = [...gallery.value.map(image => image.file), ...files]
  if (!asCover && cover.value) total.push(cover.value.file)
  if (files.some(file => !/[.](png|jpe?g)$/i.test(file.name) || file.size === 0)) {
    imageError.value = 'اختر صور PNG أو JPEG صالحة.'
    return
  }
  if (!asCover && visibleStored.value.length + gallery.value.length + files.length > 5) {
    imageError.value = 'المسموح خمس صور إضافية.'
    return
  }
  if (total.reduce((sum, file) => sum + file.size, 0) > maxUpload) {
    imageError.value = 'إجمالي الصور الجديدة لا يتجاوز 5 ميجابايت.'
    return
  }

  if (asCover && files[0]) {
    if (cover.value) URL.revokeObjectURL(cover.value.url)
    cover.value = { file: files[0], url: URL.createObjectURL(files[0]) }
  } else {
    gallery.value = [...gallery.value, ...files.map(file => ({ file, url: URL.createObjectURL(file) }))]
  }
}

function removePending(image: Preview) {
  URL.revokeObjectURL(image.url)
  gallery.value = gallery.value.filter(item => item !== image)
  imageError.value = ''
}

function removeCover() {
  if (cover.value) URL.revokeObjectURL(cover.value.url)
  cover.value = undefined
  imageError.value = ''
}

function imageSource(url: string) {
  return url.startsWith('/images/') || url.startsWith('https://') ? url : undefined
}

function chooseVariant(variant?: AdminProductDetails['variants'][number]) {
  selected.value = variant
  size.value = variant?.size ?? ''
  price.value = variant?.price ?? ''
}

async function reloadProduct() {
  await refresh()
  if (error.value || !data.value) return false
  loadProduct(data.value)
  chooseVariant()
  needsRefresh.value = false
  failure.value = ''
  return true
}

// Creating a product or a size carries an Idempotency-Key so a repeat never makes a duplicate.
const productCreation = useIdempotencyKey()
const variantCreation = useIdempotencyKey()
async function post(path: string, body: FormData | Record<string, unknown>, idempotencyKey?: string) {
  const session = await $fetch<{ requestToken: string }>('/api/session')
  const headers: Record<string, string> = { RequestVerificationToken: session.requestToken }
  if (idempotencyKey) headers['Idempotency-Key'] = idempotencyKey
  const response = await $fetch.raw<{ success?: boolean, productId?: number, message?: string, errors?: Record<string, string[]> }>(path, {
    method: 'POST', retry: 0, ignoreResponseError: true, body, headers
  })
  if (!response.ok || response._data?.success !== true) {
    needsRefresh.value = !isNew && (response.status === 409 || response.status >= 500)
    failure.value = Object.values(response._data?.errors ?? {}).flat().join(' ') ||
      response._data?.message || (response.status === 413 ? 'حجم الرفع أكبر من المسموح.' : 'تعذر حفظ البيانات.')
    return null
  }
  return response._data
}

async function saveProduct() {
  if (disabled.value || !form.name.trim() || (!isNew && !version.value)) return
  saving.value = true
  failure.value = ''
  notice.value = ''
  const payload = new FormData()
  for (const [key, value] of Object.entries(form)) payload.append(key, value.trim())
  if (!isNew) payload.append('version', version.value)
  if (cover.value) payload.append('imageFile', cover.value.file)
  for (const image of gallery.value) payload.append('galleryFiles', image.file)
  for (const item of removed.value) payload.append('removeGallery', item)
  try {
    const result = await post(url, payload, isNew
      ? productCreation.get({ ...form, cover: cover.value?.file.name, gallery: gallery.value.map(image => image.file.name) })
      : undefined)
    if (!result) return
    if (isNew) productCreation.reset()
    if (isNew && result.productId) {
      await router.replace('/admin/products/' + result.productId)
    } else {
      needsRefresh.value = !await reloadProduct()
      notice.value = 'تم حفظ المنتج.'
    }
  } catch {
    needsRefresh.value = !isNew
    failure.value = 'تعذر التحقق من الحفظ. راجع قائمة المنتجات قبل إعادة المحاولة.'
  } finally {
    saving.value = false
  }
}

async function saveVariant() {
  if (disabled.value || !data.value || !size.value.trim() ||
      !/^[0-9]{1,16}([.][0-9]{1,2})?$/.test(price.value) || !/[1-9]/.test(price.value)) return
  const variant = selected.value
  const input = { productId: data.value.productId, productVariantId: variant?.productVariantId ?? 0,
    size: size.value.trim(), price: price.value }
  saving.value = true
  failure.value = ''
  notice.value = ''
  try {
    const result = await post(url + '/variants' + (variant ? '/' + variant.productVariantId : ''),
      variant ? { input, expectedSize: variant.size, expectedPrice: variant.price } : input,
      variant ? undefined : variantCreation.get(input))
    if (!result) return
    if (!variant) variantCreation.reset()
    chooseVariant()
    await refresh()
    needsRefresh.value = !!error.value
    notice.value = 'تم حفظ الحجم والسعر.'
  } catch {
    needsRefresh.value = true
    failure.value = 'تعذر التحقق من الحفظ. حدّث تفاصيل المنتج قبل إعادة المحاولة.'
  } finally {
    saving.value = false
  }
}

async function deletePost(path: string) {
  const session = await $fetch<{ requestToken: string }>('/api/session')
  return await $fetch.raw<{ message?: string }>(path, {
    method: 'POST', retry: 0, ignoreResponseError: true,
    headers: { RequestVerificationToken: session.requestToken }
  })
}

async function setArchived(path: string, archived: boolean, okText: string, failText: string) {
  if (disabled.value) return
  saving.value = true
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const response = await $fetch.raw<{ message?: string }>(path, {
      method: 'POST', retry: 0, ignoreResponseError: true, body: { archived },
      headers: { RequestVerificationToken: session.requestToken }
    })
    if (!response.ok) { toast.error(response._data?.message ?? failText); return }
    toast.success(okText)
    await refresh()
    needsRefresh.value = !!error.value
  } catch {
    toast.error(failText)
  } finally {
    saving.value = false
  }
}

function toggleProductArchive() {
  const archived = !data.value?.isArchived
  return setArchived(url + '/archive', archived, archived ? 'تمت أرشفة المنتج.' : 'تم إلغاء أرشفة المنتج.', 'تعذر تحديث أرشفة المنتج.')
}

function toggleVariantArchive(variant: AdminProductDetails['variants'][number]) {
  const archived = !variant.isArchived
  return setArchived(url + '/variants/' + variant.productVariantId + '/archive', archived,
    archived ? 'تمت أرشفة الحجم.' : 'تم إلغاء أرشفة الحجم.', 'تعذر تحديث أرشفة الحجم.')
}

async function removeProduct() {
  if (disabled.value || !data.value) return
  const ok = await confirmDialog({ title: 'حذف المنتج؟', message: 'سيُحذف المنتج وأحجامه نهائيًا.', confirmLabel: 'حذف', tone: 'danger' })
  if (!ok) return
  saving.value = true
  try {
    const response = await deletePost(url + '/delete')
    if (!response.ok) { toast.error(response._data?.message ?? 'تعذر حذف المنتج.'); return }
    toast.success('تم حذف المنتج.')
    await router.push('/admin/products')
  } catch {
    toast.error('تعذر حذف المنتج. حاول مرة أخرى.')
  } finally {
    saving.value = false
  }
}

async function removeVariant(variant: AdminProductDetails['variants'][number]) {
  if (disabled.value) return
  const ok = await confirmDialog({ title: 'حذف الحجم؟', message: 'سيُحذف الحجم «' + variant.size + '» نهائيًا.', confirmLabel: 'حذف', tone: 'danger' })
  if (!ok) return
  saving.value = true
  try {
    const response = await deletePost(url + '/variants/' + variant.productVariantId + '/delete')
    if (!response.ok) { toast.error(response._data?.message ?? 'تعذر حذف الحجم.'); return }
    toast.success('تم حذف الحجم.')
    if (selected.value?.productVariantId === variant.productVariantId) chooseVariant()
    await refresh()
    needsRefresh.value = !!error.value
  } catch {
    toast.error('تعذر حذف الحجم. حاول مرة أخرى.')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <section class="mx-auto max-w-6xl" aria-labelledby="product-editor-title">
    <NuxtLink to="/admin/products" class="mb-3 inline-flex min-h-10 items-center gap-1.5 rounded-full border border-brand/30 bg-white px-3 text-sm font-semibold text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand motion-safe:transition-colors"><AppIcon name="chevron-right" :size="16" />العودة للمنتجات</NuxtLink>
    <div class="mb-5 flex flex-wrap items-center justify-between gap-3">
      <h1 id="product-editor-title" class="text-3xl font-bold">{{ isNew ? 'إضافة منتج' : 'تعديل المنتج' }}
        <span v-if="data?.isArchived" class="badge badge-status-pending ms-2 align-middle text-sm">مؤرشف</span></h1>
      <a v-if="!isNew && data && !data.isArchived" :href="'/products/' + data.productId" target="_blank" rel="noopener"
        class="btn-secondary min-h-11 gap-1.5 text-sm"><AppIcon name="sparkle" :size="16" />عرض في المتجر</a>
    </div>
    <p v-if="notice" class="mb-3 text-success" role="status">{{ notice }}</p>
    <p v-if="failure" class="mb-3 text-danger" role="alert">{{ failure }}</p>
    <button v-if="needsRefresh" type="button" :disabled="saving" class="mb-3 min-h-11 rounded-lg border px-4"
      @click="reloadProduct">تحديث التفاصيل وإعادة تحميل النموذج</button>
    <p v-if="categories.status.value !== 'success' && !categories.error.value ||
        (!isNew && (status === 'pending' || status === 'idle'))" role="status">جاري تحميل البيانات…</p>
    <p v-else-if="error?.statusCode === 404" role="alert">المنتج غير موجود.</p>
    <AdminReadError v-else-if="error || categories.error.value" :status-code="error?.statusCode ?? categories.error.value?.statusCode" />
    <div v-else class="grid items-start gap-6 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]">
      <form class="card p-4 sm:p-6" @submit.prevent="saveProduct">
        <fieldset :disabled="disabled" class="space-y-4">
          <h2 class="text-xl font-bold">البيانات الأساسية</h2>
          <label class="block">اسم المنتج
            <input v-model="form.name" maxlength="150" required class="field mt-1 block min-h-11 w-full">
          </label>
          <div>
            <label for="product-category" class="block">الفئة</label>
            <select id="product-category" v-model="form.categoryId" class="field mt-1 block min-h-11 w-full">
              <option value="">بدون فئة</option>
              <option v-for="category in categories.data.value" :key="category.categoryId" :value="String(category.categoryId)">
                {{ category.name }}
              </option>
            </select>
          </div>
          <label class="block">الوصف
            <textarea v-model="form.description" maxlength="10000" rows="4" class="field mt-1 block w-full" />
          </label>
          <div class="border-t pt-4">
            <h2 class="mb-3 text-xl font-bold">صورة الغلاف</h2>
            <label class="block">رابط HTTPS أو مسار صورة محلية
              <input v-model="form.imageUrl" dir="ltr" maxlength="2048" class="field mt-1 block min-h-11 w-full">
            </label>
            <label class="mt-3 block">أو اختر ملف غلاف
              <input type="file" accept=".png,.jpg,.jpeg" class="mt-1 block max-w-full" @change="selectImages($event, true)">
            </label>
            <img v-if="cover || imageSource(form.imageUrl)" :src="cover?.url ?? imageSource(form.imageUrl)"
              alt="معاينة الغلاف" referrerpolicy="no-referrer" class="mt-3 size-32 rounded-xl object-cover shadow-soft">
            <button v-if="cover" type="button" class="mt-2 inline-flex min-h-10 items-center gap-1.5 rounded-full border border-brand/30 bg-white px-3 text-sm font-semibold text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand motion-safe:transition-colors" @click="removeCover">إلغاء اختيار الغلاف الجديد</button>
          </div>
          <div class="border-t pt-4">
            <h2 class="text-xl font-bold">صور إضافية</h2>
            <p class="mb-3 text-sm">حتى خمس صور إضافية. إجمالي الصور الجديدة 5 ميجابايت.</p>
            <label class="block">اختيار الصور
              <input type="file" multiple accept=".png,.jpg,.jpeg" class="mt-1 block max-w-full" @change="selectImages($event, false)">
            </label>
            <p v-if="imageError" class="mt-2 text-danger" role="alert">{{ imageError }}</p>
            <ul class="mt-3 flex flex-wrap gap-3">
              <li v-for="(image, index) in stored" :key="image + index" class="relative w-24">
                <img :src="imageSource(image)" :alt="'صورة محفوظة ' + (index + 1)" class="size-24 rounded-lg object-cover">
                <button v-if="!removed.includes(image)" type="button" :aria-label="'إزالة الصورة المحفوظة ' + (index + 1)"
                  class="absolute end-1 top-1 size-11 rounded-full bg-white text-xl text-danger shadow"
                  @click="removed.push(image)">×</button>
                <button v-else type="button" class="mt-1 inline-flex min-h-10 items-center gap-1.5 rounded-full border border-brand/30 bg-white px-3 text-sm font-semibold text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand motion-safe:transition-colors"
                  @click="removed = removed.filter(item => item !== image)">تراجع عن الإزالة</button>
              </li>
              <li v-for="image in gallery" :key="image.url" class="relative w-24">
                <img :src="image.url" alt="" class="size-24 rounded-lg object-cover">
                <button type="button" :aria-label="'إزالة ' + image.file.name"
                  class="absolute end-1 top-1 size-11 rounded-full bg-white text-xl text-danger shadow"
                  @click="removePending(image)">×</button>
                <p class="truncate text-xs" dir="auto">{{ image.file.name }}</p>
              </li>
            </ul>
          </div>
          <div class="sticky bottom-0 -mx-4 border-t border-lavender bg-white/95 px-4 py-3 backdrop-blur sm:-mx-6 sm:px-6">
            <button type="submit" class="min-h-11 w-full btn-primary disabled:opacity-60 sm:w-auto">
              {{ saving ? 'جاري الحفظ…' : 'حفظ المنتج' }}
            </button>
          </div>
        </fieldset>
      </form>
      <div class="grid gap-6 lg:sticky lg:top-20">

      <section v-if="!isNew && data" class="card p-4 sm:p-6" aria-labelledby="variant-title">
        <h2 id="variant-title" class="mb-3 text-xl font-bold">الأحجام والأسعار</h2>
        <p v-if="!data.variants.length" class="mb-3 rounded-xl bg-status-pending p-3 text-sm text-warning">ما فيش أحجام. المنتج ما يتباعش لين تضيف حجم بسعر.</p>
        <ul class="mb-4 space-y-2">
          <li v-for="variant in data.variants" :key="variant.productVariantId"
            class="flex flex-wrap items-center gap-x-2 gap-y-1 rounded-xl border border-lavender-deep px-3 py-2"
            :class="{ 'opacity-60': variant.isArchived, 'ring-2 ring-brand': selected?.productVariantId === variant.productVariantId }">
            <span class="me-auto"><span class="chip bg-lavender text-brand-deep">{{ variant.size }}</span>
              <bdi class="ms-2 font-bold">{{ variant.price }}</bdi> د.ل
              <span v-if="variant.isArchived" class="badge badge-status-pending ms-1">مؤرشف</span></span>
            <button type="button" :disabled="disabled" class="inline-flex min-h-10 items-center gap-1.5 rounded-full border border-brand/30 bg-white px-3 text-sm font-semibold text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand motion-safe:transition-colors disabled:opacity-60"
              @click="chooseVariant(variant)">تعديل</button>
            <button type="button" :disabled="disabled" :aria-label="(variant.isArchived ? 'إلغاء أرشفة الحجم ' : 'أرشفة الحجم ') + variant.size"
              class="inline-flex min-h-10 items-center gap-1.5 rounded-full border border-brand/30 bg-white px-3 text-sm font-semibold text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand motion-safe:transition-colors disabled:opacity-60" @click="toggleVariantArchive(variant)">{{ variant.isArchived ? 'إلغاء الأرشفة' : 'أرشفة' }}</button>
            <button type="button" :disabled="disabled" :aria-label="'حذف الحجم ' + variant.size"
              class="inline-flex min-h-10 items-center gap-1.5 rounded-full border border-danger/30 bg-white px-3 text-sm font-semibold text-danger hover:bg-status-cancelled focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-danger disabled:opacity-60" @click="removeVariant(variant)">حذف</button>
          </li>
        </ul>
        <form @submit.prevent="saveVariant">
          <fieldset :disabled="disabled" class="space-y-3">
            <legend class="mb-1 font-semibold">{{ selected ? 'تعديل ' + selected.size : 'إضافة حجم' }}</legend>
            <label class="block">اسم الحجم
              <input v-model="size" maxlength="50" required class="field mt-1 block min-h-11 w-full">
            </label>
            <label class="block">السعر بالدينار الليبي
              <input v-model.trim="price" inputmode="decimal" dir="ltr" required maxlength="19" pattern="[0-9]{1,16}([.][0-9]{1,2})?"
                class="field mt-1 block min-h-11 w-full">
            </label>
            <button type="submit" class="min-h-11 btn-primary">{{ selected ? 'حفظ التعديل' : 'حفظ الحجم' }}</button>
            <button v-if="selected" type="button" class="btn-secondary ms-3 min-h-11" @click="chooseVariant()">إلغاء التعديل</button>
          </fieldset>
        </form>
      </section>
      <section v-if="!isNew && data" class="card border border-danger/20 p-4 sm:p-6" aria-labelledby="product-danger-title">
        <h2 id="product-danger-title" class="mb-1 text-lg font-bold">إخفاء أو حذف المنتج</h2>
        <p class="mb-3 text-sm text-ink-soft">الأرشفة تخفي المنتج من المتجر وتقدر ترجعه. الحذف نهائي.</p>
        <div class="flex flex-wrap gap-3">
          <button type="button" :disabled="disabled" class="btn-secondary min-h-11 disabled:opacity-60"
            @click="toggleProductArchive">{{ data.isArchived ? 'إلغاء الأرشفة' : 'أرشفة المنتج' }}</button>
          <button type="button" :disabled="disabled" class="min-h-11 rounded-full border border-danger px-4 text-danger hover:bg-danger/10 disabled:opacity-60"
            @click="removeProduct">حذف المنتج</button>
        </div>
      </section>
      </div>
    </div>
  </section>
</template>
