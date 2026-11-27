<script setup lang="ts">
import type { AppIconName } from '~/components/AppIcon.vue'

useSeoMeta({ robots: 'noindex, nofollow, noarchive' })

const route = useRoute()
type NavLink = { to: string, label: string, icon: AppIconName, badge?: 'orders' | 'messages' | 'reviews' }
// Grouped by daily use: look at the day, handle orders, keep the catalogue, answer customers, settings.
const groups: { title: string, links: NavLink[] }[] = [
  { title: 'الرئيسية', links: [{ to: '/admin', label: 'لوحة الإدارة', icon: 'home' }] },
  {
    title: 'الطلبات',
    links: [
      { to: '/admin/orders', label: 'الطلبات', icon: 'cart', badge: 'orders' },
      { to: '/admin/orders/manual', label: 'طلب من فيسبوك', icon: 'facebook' },
      { to: '/admin/delivery', label: 'التوصيل والرواجع', icon: 'truck' }
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
      { to: '/admin/reviews', label: 'التقييمات', icon: 'star', badge: 'reviews' }
    ]
  },
  {
    title: 'الحسابات',
    links: [
      { to: '/admin/users', label: 'الزبائن', icon: 'user' },
      { to: '/admin/admins', label: 'المديرين', icon: 'shield' }
    ]
  },
  {
    title: 'الإعدادات',
    links: [
      { to: '/admin/shipping-rates', label: 'أسعار التوصيل', icon: 'cash' },
      { to: '/admin/promotions', label: 'العروض', icon: 'gift' }
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
const counts = reactive({ orders: 0, messages: 0, reviews: 0 })
async function loadCounts() {
  const [orders, messages, reviews] = await Promise.allSettled([
    $fetch<{ total: number }>('/api/admin/orders', { query: { status: 'pending' }, retry: 0 }),
    $fetch<{ items: { isRead?: boolean }[] }>('/api/admin/contact-messages', { retry: 0 }),
    $fetch<{ items: { isVisible?: boolean }[] }>('/api/admin/reviews', { retry: 0 })
  ])
  counts.orders = orders.status === 'fulfilled' ? orders.value.total : 0
  counts.messages = messages.status === 'fulfilled' ? messages.value.items.filter(item => item.isRead === false).length : 0
  counts.reviews = reviews.status === 'fulfilled' ? reviews.value.items.filter(item => item.isVisible === false).length : 0
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
const badgeText: Record<NonNullable<NavLink['badge']>, string> = { orders: ' طلبات جديدة', messages: ' رسائل غير مقروءة', reviews: ' تقييمات بانتظار المراجعة' }
const badgeTone: Record<NonNullable<NavLink['badge']>, string> = { orders: 'bg-warning', messages: 'bg-brand', reviews: 'bg-info' }
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
    </header>

    <div v-if="open" class="fixed inset-0 z-40 bg-ink/50 lg:hidden" aria-hidden="true" @click="setOpen(false)" />

    <aside id="admin-sidebar" ref="sidebar"
      class="fixed inset-y-0 start-0 z-50 flex w-[272px] max-w-[85vw] flex-col bg-white shadow-lift motion-safe:transition-transform motion-safe:duration-200 lg:z-20 lg:max-w-none lg:translate-x-0 lg:border-e lg:border-lavender lg:shadow-none"
      :class="open ? 'translate-x-0' : 'max-lg:invisible max-lg:ltr:-translate-x-full max-lg:rtl:translate-x-full'"
      :role="open ? 'dialog' : undefined" :aria-modal="open ? 'true' : undefined" :aria-label="open ? 'قائمة الإدارة' : undefined">
      <div class="bg-gradient-brand relative flex items-center gap-2.5 overflow-hidden px-4 py-3 text-white">
        <span class="grid size-9 shrink-0 place-items-center rounded-xl bg-white/15 ring-1 ring-white/25" aria-hidden="true">
          <img src="/brand/flower-white.png" alt="" width="24" height="24" class="h-6 w-6" />
        </span>
        <NuxtLink to="/admin" class="min-w-0 rounded-lg leading-tight text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white">
          <span class="block text-base font-extrabold">أوركيديا</span>
          <span class="block text-[11px] font-medium text-white/80">لوحة الإدارة</span>
        </NuxtLink>
        <button v-if="open" type="button" class="ms-auto inline-flex min-h-11 min-w-11 items-center justify-center rounded-xl text-white hover:bg-white/15 focus-visible:outline-2 focus-visible:outline-white lg:hidden"
          aria-label="إغلاق القائمة" @click="setOpen(false)">
          <AppIcon name="x" :size="20" />
        </button>
      </div>
      <nav class="admin-scroll flex-1 overflow-y-auto px-2 py-2" aria-label="قائمة الإدارة">
        <div v-for="group in groups" :key="group.title" class="mb-2 last:mb-0" role="group" :aria-labelledby="'nav-' + group.title">
          <p :id="'nav-' + group.title" class="mb-0.5 px-2 text-[11px] font-bold tracking-wide text-ink-soft">{{ group.title }}</p>
          <ul class="flex flex-col gap-0.5">
            <li v-for="link in group.links" :key="link.to">
              <NuxtLink :to="link.to" :aria-current="isActive(link.to) ? 'page' : undefined"
                class="group flex min-h-10 items-center gap-2.5 rounded-lg px-2 py-1 text-sm font-semibold motion-safe:transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
                :class="isActive(link.to) ? 'bg-brand text-white shadow-soft' : 'text-ink hover:bg-lavender hover:text-brand-deep'">
                <span class="grid size-7 shrink-0 place-items-center rounded-md motion-safe:transition-colors"
                  :class="isActive(link.to) ? 'bg-white/20' : 'bg-lavender text-brand group-hover:bg-white'" aria-hidden="true">
                  <AppIcon :name="link.icon" :size="16" />
                </span>
                <span class="min-w-0 flex-1 truncate">{{ link.label }}</span>
                <span v-if="link.badge && badgeFor(link)" class="grid h-5 min-w-5 place-items-center rounded-full px-1.5 text-[11px] font-bold"
                  :class="isActive(link.to) ? 'bg-white text-brand-deep' : [badgeTone[link.badge], 'text-white']">
                  <bdi>{{ badgeFor(link) }}</bdi><span class="sr-only">{{ badgeText[link.badge] }}</span>
                </span>
              </NuxtLink>
            </li>
          </ul>
        </div>
      </nav>
      <div class="flex items-center gap-1 border-t border-lavender bg-blush/50 px-2 py-2">
        <span class="grid size-8 shrink-0 place-items-center rounded-full bg-gradient-brand text-white" aria-hidden="true"><AppIcon name="user" :size="16" /></span>
        <bdi v-if="account?.signedIn" class="min-w-0 flex-1 truncate px-1 text-xs font-semibold text-ink" dir="ltr" :title="account.name ?? undefined">{{ account.name }}</bdi>
        <span v-else class="min-w-0 flex-1" />
        <NuxtLink to="/account" aria-label="الحساب" title="الحساب"
          class="grid size-10 shrink-0 place-items-center rounded-lg text-ink-soft hover:bg-lavender hover:text-brand-deep focus-visible:outline-2 focus-visible:outline-brand">
          <AppIcon name="user" :size="18" />
        </NuxtLink>
        <NuxtLink to="/" aria-label="المتجر" title="المتجر"
          class="grid size-10 shrink-0 place-items-center rounded-lg text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-brand">
          <AppIcon name="sparkle" :size="18" />
        </NuxtLink>
        <button v-if="account?.signedIn" type="button" :disabled="loggingOut" aria-label="تسجيل الخروج" :title="loggingOut ? 'جاري الخروج…' : 'تسجيل الخروج'"
          class="grid size-10 shrink-0 place-items-center rounded-lg text-danger hover:bg-danger/10 focus-visible:outline-2 focus-visible:outline-danger disabled:opacity-60"
          @click="logout">
          <AppIcon name="login" :size="18" class="rotate-180" />
        </button>
      </div>
    </aside>

    <main id="admin-content" class="px-4 pt-6 pb-28 lg:ms-[272px] lg:px-8 lg:pt-8 lg:pb-10">
      <!-- Desktop top bar: where you are -->
      <div class="mx-auto mb-6 hidden max-w-6xl items-center gap-3 lg:flex">
        <nav aria-label="مكانك في الإدارة" class="flex items-center gap-2 text-sm text-ink-soft">
          <NuxtLink to="/admin" class="inline-flex min-h-9 items-center rounded-full px-3 text-sm font-semibold text-ink-soft hover:bg-lavender hover:text-brand-deep focus-visible:outline-2 focus-visible:outline-brand">الإدارة</NuxtLink>
          <template v-if="activeTo && activeTo !== '/admin'">
            <AppIcon name="chevron-left" :size="14" />
            <NuxtLink :to="activeTo" class="inline-flex min-h-9 items-center rounded-full bg-lavender px-3 text-sm font-bold text-brand-deep focus-visible:outline-2 focus-visible:outline-brand" aria-current="location">{{ pageTitle }}</NuxtLink>
          </template>
        </nav>
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
/* Thin, quiet scrollbar for the sidebar menu. */
.admin-scroll { scrollbar-width: thin; scrollbar-color: var(--color-lavender-deep) transparent; }
.admin-scroll::-webkit-scrollbar { width: 6px; }
.admin-scroll::-webkit-scrollbar-thumb { background: var(--color-lavender-deep); border-radius: 9999px; }
</style>
