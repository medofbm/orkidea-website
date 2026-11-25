<script setup lang="ts">
import type { AppIconName } from '~/components/AppIcon.vue'

useSeoMeta({ robots: 'noindex, nofollow, noarchive' })

const route = useRoute()
type NavLink = { to: string, label: string, icon: AppIconName, badge?: 'orders' | 'messages' }
// Grouped so the owner finds things by task: selling, catalogue, customers.
const groups: { title: string, links: NavLink[] }[] = [
  { title: 'نظرة عامة', links: [{ to: '/admin', label: 'لوحة الإدارة', icon: 'home' }] },
  {
    title: 'البيع',
    links: [
      { to: '/admin/orders', label: 'الطلبات', icon: 'cart', badge: 'orders' },
      { to: '/admin/orders/manual', label: 'طلب من فيسبوك', icon: 'facebook' },
      { to: '/admin/delivery', label: 'التوصيل والرواجع', icon: 'truck' },
      { to: '/admin/shipping-rates', label: 'أسعار التوصيل', icon: 'cash' }
    ]
  },
  {
    title: 'الكتالوج',
    links: [
      { to: '/admin/products', label: 'المنتجات', icon: 'bag' },
      { to: '/admin/categories', label: 'الفئات', icon: 'flower' },
      { to: '/admin/inventory', label: 'المخزون', icon: 'droplet' }
    ]
  },
  {
    title: 'الزبائن',
    links: [
      { to: '/admin/messages', label: 'الرسائل', icon: 'mail', badge: 'messages' },
      { to: '/admin/reviews', label: 'التقييمات', icon: 'star' }
    ]
  }
]
// Bottom bar on phones: the four places used most, plus «المزيد» for the full menu.
const quick: NavLink[] = [
  { to: '/admin', label: 'الرئيسية', icon: 'home' },
  { to: '/admin/orders', label: 'الطلبات', icon: 'cart', badge: 'orders' },
  { to: '/admin/products', label: 'المنتجات', icon: 'bag' },
  { to: '/admin/inventory', label: 'المخزون', icon: 'droplet' }
]
const allLinks = groups.flatMap(group => group.links)

// Light counters (best effort; never block the layout): new orders and unread messages.
const counts = reactive({ orders: 0, messages: 0 })
async function loadCounts() {
  const [orders, messages] = await Promise.allSettled([
    $fetch<{ total: number }>('/api/admin/orders', { query: { status: 'pending' }, retry: 0 }),
    $fetch<{ items: { isRead?: boolean }[] }>('/api/admin/contact-messages', { retry: 0 })
  ])
  counts.orders = orders.status === 'fulfilled' ? orders.value.total : 0
  counts.messages = messages.status === 'fulfilled' ? messages.value.items.filter(item => item.isRead === false).length : 0
}
onMounted(loadCounts)
watch(() => route.fullPath, loadCounts)

// The most specific link wins, so «طلب من فيسبوك» is active on /admin/orders/manual, not «الطلبات».
const activeTo = computed(() => {
  const matches = allLinks.filter(link => link.to === '/admin'
    ? route.path === '/admin'
    : route.path === link.to || route.path.startsWith(link.to + '/'))
  return matches.sort((a, b) => b.to.length - a.to.length)[0]?.to
})
const isActive = (to: string) => activeTo.value === to
const pageTitle = computed(() => allLinks.find(link => link.to === activeTo.value)?.label ?? 'لوحة الإدارة')

// Signed-in admin and logout.
const { account, refresh: refreshAccount } = useAccount()
onMounted(() => { if (!account.value) void refreshAccount().catch(() => undefined) })
const loggingOut = ref(false)
async function logout() {
  if (loggingOut.value) return
  loggingOut.value = true
  try {
    await postAccount('logout')
    account.value = null
    await navigateTo('/account')
  } finally {
    loggingOut.value = false
  }
}

const open = ref(false)
const menuButton = ref<HTMLButtonElement | null>(null)
const sidebar = ref<HTMLElement | null>(null)
const focusables = () => Array.from(sidebar.value?.querySelectorAll<HTMLElement>('a[href], button:not([disabled])') ?? [])

async function setOpen(value: boolean) {
  open.value = value
  await nextTick()
  if (value) focusables()[0]?.focus()
  else menuButton.value?.focus()
}
function onKeydown(event: KeyboardEvent) {
  if (!open.value) return
  if (event.key === 'Escape') { event.preventDefault(); setOpen(false); return }
  if (event.key !== 'Tab') return
  const items = focusables()
  if (!items.length) return
  const first = items[0]!
  const last = items[items.length - 1]!
  if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
  else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
}
watch(() => route.fullPath, () => { open.value = false })
const badgeFor = (link: NavLink) => link.badge ? counts[link.badge] : 0
</script>

<template>
  <div class="min-h-screen bg-cream" @keydown="onKeydown">
    <a href="#admin-content" class="sr-only focus:not-sr-only">الانتقال إلى المحتوى</a>

    <!-- Phone top bar -->
    <header class="sticky top-0 z-30 flex items-center gap-3 border-b border-lavender bg-white/95 px-4 py-2 backdrop-blur lg:hidden">
      <button ref="menuButton" type="button" class="btn-ghost inline-flex min-h-11 min-w-11 items-center justify-center"
        aria-controls="admin-sidebar" :aria-expanded="open" aria-label="فتح قائمة الإدارة" @click="setOpen(!open)">
        <AppIcon name="menu" :size="24" />
      </button>
      <span class="min-w-0 flex-1 truncate text-lg font-bold text-brand">{{ pageTitle }}</span>
      <NuxtLink to="/admin/orders/manual" class="btn-primary min-h-11 gap-1 px-3 text-sm" aria-label="تسجيل طلب من فيسبوك">
        <AppIcon name="facebook" :size="16" /><span class="max-[380px]:sr-only">طلب جديد</span>
      </NuxtLink>
    </header>

    <div v-if="open" class="fixed inset-0 z-40 bg-ink/50 lg:hidden" aria-hidden="true" @click="setOpen(false)" />

    <aside id="admin-sidebar" ref="sidebar"
      class="fixed inset-y-0 start-0 z-50 flex w-[272px] max-w-[85vw] flex-col bg-white shadow-lift motion-safe:transition-transform motion-safe:duration-200 lg:z-20 lg:max-w-none lg:translate-x-0 lg:border-e lg:border-lavender lg:shadow-none"
      :class="open ? 'translate-x-0' : 'max-lg:invisible max-lg:ltr:-translate-x-full max-lg:rtl:translate-x-full'"
      :role="open ? 'dialog' : undefined" :aria-modal="open ? 'true' : undefined" :aria-label="open ? 'قائمة الإدارة' : undefined">
      <div class="bg-gradient-brand flex items-center gap-3 px-5 py-5 text-white">
        <img src="/brand/flower-white.png" alt="" width="36" height="36" class="h-9 w-9" />
        <NuxtLink to="/admin" class="text-xl font-extrabold text-white">إدارة أوركيديا</NuxtLink>
        <button v-if="open" type="button" class="ms-auto inline-flex min-h-11 min-w-11 items-center justify-center rounded-lg text-white lg:hidden"
          aria-label="إغلاق القائمة" @click="setOpen(false)">
          <AppIcon name="x" :size="22" />
        </button>
      </div>
      <nav class="flex-1 overflow-y-auto p-3" aria-label="قائمة الإدارة">
        <div v-for="group in groups" :key="group.title" class="mb-3">
          <p class="mb-1 px-3 text-xs font-bold tracking-wide text-ink-soft/80">{{ group.title }}</p>
          <ul class="flex flex-col gap-0.5">
            <li v-for="link in group.links" :key="link.to">
              <NuxtLink :to="link.to" :aria-current="isActive(link.to) ? 'page' : undefined"
                class="relative flex min-h-11 items-center gap-3 rounded-xl px-3 py-2 font-semibold text-ink-soft hover:bg-lavender hover:text-brand-deep focus-visible:bg-lavender"
                :class="isActive(link.to) ? 'nav-active bg-lavender text-brand-deep' : ''">
                <AppIcon :name="link.icon" :size="20" />
                {{ link.label }}
                <span v-if="badgeFor(link)" class="ms-auto grid min-w-6 place-items-center rounded-full px-2 text-xs font-bold text-white"
                  :class="link.badge === 'orders' ? 'bg-warning' : 'bg-brand'">
                  <bdi>{{ badgeFor(link) }}</bdi><span class="sr-only">{{ link.badge === 'orders' ? ' طلبات جديدة' : ' غير مقروءة' }}</span>
                </span>
              </NuxtLink>
            </li>
          </ul>
        </div>
      </nav>
      <div class="space-y-1 border-t border-lavender p-3">
        <div v-if="account?.signedIn" class="flex items-center gap-3 rounded-xl bg-lavender/60 px-3 py-2">
          <span class="grid size-9 shrink-0 place-items-center rounded-full bg-gradient-brand text-white" aria-hidden="true"><AppIcon name="user" :size="18" /></span>
          <span class="min-w-0 flex-1">
            <span class="block text-xs text-ink-soft">مسجّل باسم</span>
            <bdi class="block truncate text-sm font-semibold text-ink" dir="ltr">{{ account.name }}</bdi>
          </span>
        </div>
        <NuxtLink to="/account" class="flex min-h-11 items-center gap-3 rounded-xl px-3 py-2 font-semibold text-ink-soft hover:bg-lavender focus-visible:bg-lavender">
          <AppIcon name="user" :size="20" />
          الحساب
        </NuxtLink>
        <NuxtLink to="/" class="flex min-h-11 items-center gap-3 rounded-xl px-3 py-2 font-semibold text-brand hover:bg-lavender focus-visible:bg-lavender">
          <AppIcon name="sparkle" :size="20" />
          عرض المتجر
        </NuxtLink>
        <button v-if="account?.signedIn" type="button" :disabled="loggingOut"
          class="flex min-h-11 w-full items-center gap-3 rounded-xl px-3 py-2 font-semibold text-danger hover:bg-danger/10 disabled:opacity-60"
          @click="logout">
          <AppIcon name="login" :size="20" class="rotate-180" />
          {{ loggingOut ? 'جاري تسجيل الخروج…' : 'تسجيل الخروج' }}
        </button>
      </div>
    </aside>

    <main id="admin-content" class="px-4 pt-6 pb-28 lg:ms-[272px] lg:px-8 lg:pt-8 lg:pb-10">
      <!-- Desktop top bar: where you are, and the most common action -->
      <div class="mx-auto mb-6 hidden max-w-6xl items-center justify-between gap-3 lg:flex">
        <nav aria-label="مكانك في الإدارة" class="flex items-center gap-2 text-sm text-ink-soft">
          <NuxtLink to="/admin" class="hover:text-brand hover:underline">الإدارة</NuxtLink>
          <template v-if="activeTo && activeTo !== '/admin'">
            <AppIcon name="chevron-left" :size="14" />
            <NuxtLink :to="activeTo" class="font-semibold text-ink hover:underline" aria-current="location">{{ pageTitle }}</NuxtLink>
          </template>
        </nav>
        <div class="flex items-center gap-2">
          <NuxtLink v-if="counts.orders" to="/admin/orders?status=pending" class="chip gap-1.5 bg-status-pending text-warning">
            <AppIcon name="cart" :size="16" /><bdi>{{ counts.orders }}</bdi> طلبات جديدة
          </NuxtLink>
          <NuxtLink to="/admin/orders/manual" class="btn-primary min-h-11 gap-1.5 text-sm">
            <AppIcon name="facebook" :size="16" />طلب جديد من فيسبوك
          </NuxtLink>
        </div>
      </div>
      <div class="mx-auto max-w-6xl"><slot /></div>
    </main>

    <!-- Phone bottom bar -->
    <nav aria-label="تنقل سريع" class="fixed inset-x-0 bottom-0 z-30 border-t border-lavender bg-white/95 backdrop-blur lg:hidden"
      style="padding-bottom: env(safe-area-inset-bottom)">
      <ul class="grid grid-cols-5">
        <li v-for="link in quick" :key="link.to">
          <NuxtLink :to="link.to" :aria-current="isActive(link.to) ? 'page' : undefined"
            class="relative flex min-h-14 flex-col items-center justify-center gap-0.5 text-[11px] font-semibold"
            :class="isActive(link.to) ? 'text-brand' : 'text-ink-soft'">
            <AppIcon :name="link.icon" :size="22" />
            {{ link.label }}
            <span v-if="badgeFor(link)" class="absolute top-1.5 start-1/2 ms-2 grid min-w-5 place-items-center rounded-full bg-warning px-1 text-[10px] font-bold text-white">
              <bdi>{{ badgeFor(link) }}</bdi><span class="sr-only"> طلبات جديدة</span>
            </span>
          </NuxtLink>
        </li>
        <li>
          <button type="button" class="flex min-h-14 w-full flex-col items-center justify-center gap-0.5 text-[11px] font-semibold text-ink-soft"
            aria-controls="admin-sidebar" :aria-expanded="open" @click="setOpen(true)">
            <AppIcon name="menu" :size="22" />
            المزيد
            <span v-if="counts.messages" class="sr-only"> ({{ counts.messages }} رسائل غير مقروءة)</span>
          </button>
        </li>
      </ul>
    </nav>
  </div>
</template>

<style scoped>
/* Active sidebar link: a slim brand bar on the inline-start edge. */
.nav-active::before {
  content: ""; position: absolute; inset-block: 0.5rem; inset-inline-start: 0;
  width: 0.25rem; border-radius: 9999px; background: var(--color-brand);
}
</style>
