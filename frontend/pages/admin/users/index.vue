<script setup lang="ts">
import type { AdminUserSummary, AdminUsersPage } from '~/types/admin'
definePageMeta({ layout: 'admin', middleware: 'admin' })
useSeoMeta({ title: 'الزبائن والحسابات' })

const route = useRoute()
const router = useRouter()
const roles = { all: 'الكل', customers: 'الزبائن', admins: 'المديرين' }
type Role = keyof typeof roles

const routeFilters = computed(() => {
  const value = route.query.page
  const page = typeof value === 'string' && /^[1-9][0-9]{0,8}$/.test(value) ? Number(value) : 1
  const role = typeof route.query.role === 'string' && Object.hasOwn(roles, route.query.role) ? route.query.role as Role : 'all'
  return { role, page }
})
// Emails and phone numbers stay out of navigational URLs and browser history (same rule as the orders page).
const searchField = ref('')
const appliedSearch = ref('')
const filters = computed(() => ({ ...routeFilters.value, search: appliedSearch.value }))
const { data, error, status, refresh } = useFetch<AdminUsersPage>('/api/admin/users', {
  query: filters, server: false, cache: 'no-store'
})

function goTo(role: Role, page: number) {
  return router.push({ query: { role: role === 'all' ? undefined : role, page: page > 1 ? String(page) : undefined } })
}
async function apply() {
  await goTo(routeFilters.value.role, 1)
  appliedSearch.value = searchField.value.trim()
}
function clearSearch() {
  searchField.value = ''
  appliedSearch.value = ''
}

function roleBadge(user: AdminUserSummary) {
  if (user.isSuperAdmin) return { label: 'مدير رئيسي', tone: 'bg-brand text-white' }
  if (user.isAdmin) return { label: 'مدير', tone: 'bg-lilac text-brand-deep' }
  return { label: 'زبونة', tone: '' }
}
</script>

<template>
  <section aria-labelledby="users-title">
    <h1 id="users-title" class="mb-2 text-3xl font-extrabold">الزبائن</h1>
    <p class="mb-5 text-ink-soft">كل الحسابات المسجلة في المتجر، الأحدث أولًا. افتح الحساب لتشوف طلباته.</p>

    <div role="group" aria-label="نوع الحساب" class="mb-4 flex flex-wrap gap-2">
      <button v-for="(label, value) in roles" :key="value" type="button" class="chip border border-transparent"
        :aria-pressed="routeFilters.role === value"
        :class="routeFilters.role === value ? '!bg-brand !text-white' : 'hover:border-lavender-deep'"
        @click="goTo(value, 1)">{{ label }}</button>
    </div>

    <form class="mb-6 flex flex-wrap items-end gap-3 rounded-xl bg-white p-4" role="search" @submit.prevent="apply">
      <label class="min-w-48 flex-1">البحث
        <input v-model="searchField" type="search" maxlength="100" placeholder="البريد الإلكتروني أو رقم الهاتف" dir="auto"
          class="field mt-1 block">
      </label>
      <button type="submit" class="btn-primary">بحث</button>
      <button v-if="appliedSearch" type="button" class="btn-ghost" @click="clearSearch">مسح البحث</button>
    </form>

    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل الحسابات…</p>
    <div v-else-if="error">
      <AdminReadError :status-code="error.statusCode" />
      <button type="button" class="btn-secondary mt-3" @click="refresh()">إعادة المحاولة</button>
    </div>
    <template v-else-if="data">
      <p class="mb-3 text-sm text-ink-soft" role="status"><bdi>{{ data.total }}</bdi> حساب</p>
      <p v-if="!data.items.length" class="card p-6 text-center text-ink-soft">ما فيش حسابات تطابق البحث.</p>

      <!-- Desktop: table -->
      <div v-if="data.items.length" class="card hidden lg:block">
        <table class="w-full table-fixed text-start text-sm">
          <thead class="bg-blush/60 text-xs text-ink-soft">
            <tr>
              <th scope="col" class="w-[42%] px-4 py-3 text-start font-bold">الحساب</th>
              <th scope="col" class="px-3 py-3 text-start font-bold">التسجيل</th>
              <th scope="col" class="px-3 py-3 text-start font-bold">آخر دخول</th>
              <th scope="col" class="px-3 py-3 text-start font-bold">الطلبات</th>
              <th scope="col" class="w-24 px-3 py-3"><span class="sr-only">فتح</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="user in data.items" :key="user.id" class="border-t border-lavender align-middle">
              <td class="max-w-0 px-4 py-3">
                <bdi dir="ltr" class="block truncate font-semibold" :title="user.email ?? undefined">{{ user.email ?? '—' }}</bdi>
                <bdi v-if="user.phoneNumber" dir="ltr" class="block text-xs text-ink-soft">{{ user.phoneNumber }}</bdi>
                <span class="mt-1 flex flex-wrap gap-1">
                  <span class="badge" :class="roleBadge(user).tone">{{ roleBadge(user).label }}</span>
                  <span class="badge" :class="user.emailConfirmed ? 'badge-status-delivered' : ''">{{ user.emailConfirmed ? 'البريد مؤكد' : 'البريد غير مؤكد' }}</span>
                </span>
              </td>
              <td class="whitespace-nowrap px-3 py-3"><time v-if="user.createdAtUtc" :datetime="user.createdAtUtc" :title="adminOrderDate(user.createdAtUtc)">{{ adminRelativeDate(user.createdAtUtc) }}</time><template v-else>غير معروف</template></td>
              <td class="whitespace-nowrap px-3 py-3">
                <time v-if="user.lastSignInAtUtc" :datetime="user.lastSignInAtUtc" :title="adminOrderDate(user.lastSignInAtUtc)">{{ adminRelativeDate(user.lastSignInAtUtc) }}</time><template v-else>غير معروف</template>
                <span v-if="user.signInCount !== null" class="block text-xs text-ink-soft"><bdi>{{ user.signInCount }}</bdi> مرة دخول</span>
              </td>
              <td class="whitespace-nowrap px-3 py-3"><bdi>{{ user.orderCount }}</bdi> طلب<span class="block text-xs font-semibold text-brand"><bdi>{{ formatPrice(user.totalSpent) }}</bdi> د.ل</span></td>
              <td class="px-3 py-3 text-end">
                <NuxtLink :to="'/admin/users/' + user.id" class="btn-secondary !min-h-10 !px-4 text-sm"
                  :aria-label="'فتح حساب ' + (user.email ?? '')">فتح</NuxtLink>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Phone: cards -->
      <ul v-if="data.items.length" class="grid gap-3 lg:hidden">
        <li v-for="user in data.items" :key="user.id">
          <NuxtLink :to="'/admin/users/' + user.id"
            class="card block p-4 hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand">
            <bdi dir="ltr" class="block truncate font-semibold">{{ user.email ?? '—' }}</bdi>
            <span class="mt-1 flex flex-wrap gap-1">
              <span class="badge" :class="roleBadge(user).tone">{{ roleBadge(user).label }}</span>
              <span class="badge" :class="user.emailConfirmed ? 'badge-status-delivered' : ''">{{ user.emailConfirmed ? 'البريد مؤكد' : 'البريد غير مؤكد' }}</span>
            </span>
            <dl class="mt-3 grid grid-cols-2 gap-x-3 gap-y-1 text-sm">
              <dt class="text-ink-soft">آخر دخول</dt><dd>{{ adminRelativeDate(user.lastSignInAtUtc) }}</dd>
              <dt class="text-ink-soft">التسجيل</dt><dd>{{ adminRelativeDate(user.createdAtUtc) }}</dd>
              <dt class="text-ink-soft">الطلبات</dt><dd><bdi>{{ user.orderCount }}</bdi> · <bdi>{{ formatPrice(user.totalSpent) }}</bdi> د.ل</dd>
              <template v-if="user.phoneNumber"><dt class="text-ink-soft">الهاتف</dt><dd><bdi dir="ltr">{{ user.phoneNumber }}</bdi></dd></template>
            </dl>
          </NuxtLink>
        </li>
      </ul>

      <nav v-if="data.pages > 1" class="mt-5 flex flex-wrap items-center justify-between gap-3" aria-label="صفحات الحسابات">
        <button type="button" :disabled="data.page <= 1" class="btn-secondary" @click="goTo(routeFilters.role, data.page - 1)">السابق</button>
        <span><bdi>{{ data.page }}</bdi> من <bdi>{{ data.pages }}</bdi></span>
        <button type="button" :disabled="data.page >= data.pages" class="btn-secondary" @click="goTo(routeFilters.role, data.page + 1)">التالي</button>
      </nav>
      <p class="mt-4 text-xs text-ink-soft">«غير معروف» يعني الحساب أقدم من بداية تسجيل النشاط. مرات الدخول تُحسب من يوم بدأ التسجيل. المجموع لا يشمل الطلبات الملغية.</p>
    </template>
  </section>
</template>
