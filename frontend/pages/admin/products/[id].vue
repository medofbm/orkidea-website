<script setup lang="ts">
import type { AdminProductDetails } from '~/types/admin'

definePageMeta({ layout: 'admin', key: route => route.path })
useSeoMeta({ title: 'تعديل المنتج' })

const id = String(useRoute().params.id)
const isNew = id === 'new'
const router = useRouter()
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

async function post(path: string, body: FormData | Record<string, unknown>) {
  const session = await $fetch<{ requestToken: string }>('/api/session')
  const response = await $fetch.raw<{ success?: boolean, productId?: number, message?: string, errors?: Record<string, string[]> }>(path, {
    method: 'POST', retry: 0, ignoreResponseError: true, body,
    headers: { RequestVerificationToken: session.requestToken }
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
    const result = await post(url, payload)
    if (!result) return
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
      variant ? { input, expectedSize: variant.size, expectedPrice: variant.price } : input)
    if (!result) return
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
</script>

<template>
  <section class="mx-auto max-w-3xl" aria-labelledby="product-editor-title">
    <NuxtLink to="/admin/products" class="inline-flex min-h-11 items-center text-purple-900 underline">العودة للمنتجات</NuxtLink>
    <h1 id="product-editor-title" class="mb-5 text-3xl font-bold">{{ isNew ? 'إضافة منتج' : 'تعديل المنتج' }}</h1>
    <p v-if="notice" class="mb-3 text-emerald-800" role="status">{{ notice }}</p>
    <p v-if="failure" class="mb-3 text-red-800" role="alert">{{ failure }}</p>
    <button v-if="needsRefresh" type="button" :disabled="saving" class="mb-3 min-h-11 rounded-lg border px-4"
      @click="reloadProduct">تحديث التفاصيل وإعادة تحميل النموذج</button>
    <p v-if="categories.status.value !== 'success' && !categories.error.value ||
        (!isNew && (status === 'pending' || status === 'idle'))" role="status">جاري تحميل البيانات…</p>
    <p v-else-if="error?.statusCode === 404" role="alert">المنتج غير موجود.</p>
    <AdminReadError v-else-if="error || categories.error.value" :status-code="error?.statusCode ?? categories.error.value?.statusCode" />
    <template v-else>
      <form class="rounded-xl bg-white p-4" @submit.prevent="saveProduct">
        <fieldset :disabled="disabled" class="space-y-4">
          <label class="block">اسم المنتج
            <input v-model="form.name" maxlength="150" required class="mt-1 block min-h-11 w-full rounded-lg border px-3">
          </label>
          <label class="block">الفئة
            <select v-model="form.categoryId" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
              <option value="">بدون فئة</option>
              <option v-for="category in categories.data.value" :key="category.categoryId" :value="String(category.categoryId)">
                {{ category.name }}
              </option>
            </select>
          </label>
          <label class="block">الوصف
            <textarea v-model="form.description" maxlength="10000" rows="4" class="mt-1 block w-full rounded-lg border p-2" />
          </label>
          <div class="border-t pt-4">
            <h2 class="mb-3 text-xl font-bold">صورة الغلاف</h2>
            <label class="block">رابط HTTPS أو مسار صورة محلية
              <input v-model="form.imageUrl" dir="ltr" maxlength="2048" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
            </label>
            <label class="mt-3 block">أو اختر ملف غلاف
              <input type="file" accept=".png,.jpg,.jpeg" class="mt-1 block max-w-full" @change="selectImages($event, true)">
            </label>
            <img v-if="cover || imageSource(form.imageUrl)" :src="cover?.url ?? imageSource(form.imageUrl)"
              alt="معاينة الغلاف" referrerpolicy="no-referrer" class="mt-3 size-24 rounded-lg object-cover">
            <button v-if="cover" type="button" class="min-h-11 text-purple-900 underline" @click="removeCover">إلغاء اختيار الغلاف الجديد</button>
          </div>
          <div class="border-t pt-4">
            <h2 class="text-xl font-bold">صور إضافية</h2>
            <p class="mb-3 text-sm">حتى خمس صور إضافية. إجمالي الصور الجديدة 5 ميجابايت.</p>
            <label class="block">اختيار الصور
              <input type="file" multiple accept=".png,.jpg,.jpeg" class="mt-1 block max-w-full" @change="selectImages($event, false)">
            </label>
            <p v-if="imageError" class="mt-2 text-red-800" role="alert">{{ imageError }}</p>
            <ul class="mt-3 flex flex-wrap gap-3">
              <li v-for="(image, index) in stored" :key="image + index" class="relative w-24">
                <img :src="imageSource(image)" :alt="'صورة محفوظة ' + (index + 1)" class="size-24 rounded-lg object-cover">
                <button v-if="!removed.includes(image)" type="button" :aria-label="'إزالة الصورة المحفوظة ' + (index + 1)"
                  class="absolute end-1 top-1 size-11 rounded-full bg-white text-xl text-red-800 shadow"
                  @click="removed.push(image)">×</button>
                <button v-else type="button" class="min-h-11 text-sm underline"
                  @click="removed = removed.filter(item => item !== image)">تراجع عن الإزالة</button>
              </li>
              <li v-for="image in gallery" :key="image.url" class="relative w-24">
                <img :src="image.url" alt="" class="size-24 rounded-lg object-cover">
                <button type="button" :aria-label="'إزالة ' + image.file.name"
                  class="absolute end-1 top-1 size-11 rounded-full bg-white text-xl text-red-800 shadow"
                  @click="removePending(image)">×</button>
                <p class="truncate text-xs" dir="auto">{{ image.file.name }}</p>
              </li>
            </ul>
          </div>
          <button type="submit" class="min-h-11 rounded-lg bg-purple-900 px-4 text-white disabled:opacity-60">
            {{ saving ? 'جاري الحفظ…' : 'حفظ المنتج' }}
          </button>
        </fieldset>
      </form>

      <section v-if="!isNew && data" class="mt-6 rounded-xl bg-white p-4" aria-labelledby="variant-title">
        <h2 id="variant-title" class="mb-3 text-xl font-bold">الأحجام والأسعار</h2>
        <ul class="mb-3 divide-y">
          <li v-for="variant in data.variants" :key="variant.productVariantId" class="flex flex-wrap items-center justify-between gap-3 py-2">
            <span>{{ variant.size }} · <bdi>{{ variant.price }}</bdi> د.ل</span>
            <button type="button" :disabled="disabled" class="min-h-11 px-2 text-purple-900 underline"
              @click="chooseVariant(variant)">تعديل</button>
          </li>
        </ul>
        <form @submit.prevent="saveVariant">
          <fieldset :disabled="disabled" class="space-y-3">
            <legend class="font-semibold">{{ selected ? 'تعديل ' + selected.size : 'إضافة حجم' }}</legend>
            <label class="block">اسم الحجم
              <input v-model="size" maxlength="50" required class="mt-1 block min-h-11 w-full rounded-lg border px-3">
            </label>
            <label class="block">السعر بالدينار الليبي
              <input v-model.trim="price" inputmode="decimal" dir="ltr" required maxlength="19" pattern="[0-9]{1,16}([.][0-9]{1,2})?"
                class="mt-1 block min-h-11 w-full rounded-lg border px-3">
            </label>
            <button type="submit" class="min-h-11 rounded-lg bg-purple-900 px-4 text-white">حفظ الحجم</button>
            <button v-if="selected" type="button" class="ms-3 min-h-11 underline" @click="chooseVariant()">إلغاء التعديل</button>
          </fieldset>
        </form>
      </section>
    </template>
  </section>
</template>
