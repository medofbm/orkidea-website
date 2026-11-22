<script setup lang="ts">
import type { AppIconName } from '~/components/AppIcon.vue'

useSeoMeta({ robots: 'noindex, nofollow, noarchive' })

const route = useRoute()
const links: { to: string, label: string, icon: AppIconName }[] = [
  { to: '/admin', label: 'لوحة الإدارة', icon: 'home' },
  { to: '/admin/orders', label: 'الطلبات', icon: 'cart' },
  { to: '/admin/delivery', label: 'التوصيل والرواجع', icon: 'truck' },
  { to: '/admin/inventory', label: 'المخزون', icon: 'droplet' },
  { to: '/admin/products', label: 'المنتجات', icon: 'bag' },
  { to: '/admin/categories', label: 'الفئات', icon: 'flower' },
  { to: '/admin/reviews', label: 'التقييمات', icon: 'star' },
  { to: '/admin/messages', label: 'الرسائل', icon: 'mail' },
  { to: '/account', label: 'الحساب', icon: 'user' }
]
// Light unread badge on the messages link (best effort; never blocks the layout).
const unread = ref(0)
async function loadUnread() {
  try {
    const result = await $fetch<{ items: { isRead?: boolean }[] }>('/api/admin/contact-messages', { retry: 0 })
    unread.value = result.items.filter(item => item.isRead === false).length
  } catch { unread.value = 0 }
}
onMounted(loadUnread)
watch(() => route.fullPath, loadUnread)
const isActive = (to: string) => to === '/admin' ? route.path === '/admin' : route.path === to || route.path.startsWith(to + '/')

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
</script>

<template>
  <div class="min-h-screen" @keydown="onKeydown">
    <a href="#admin-content" class="sr-only focus:not-sr-only">الانتقال إلى المحتوى</a>

    <header class="sticky top-0 z-30 flex items-center gap-3 border-b border-lavender bg-white px-4 py-3 lg:hidden">
      <button ref="menuButton" type="button" class="btn-ghost inline-flex min-h-11 min-w-11 items-center justify-center"
        aria-controls="admin-sidebar" :aria-expanded="open" aria-label="فتح قائمة الإدارة" @click="setOpen(!open)">
        <AppIcon name="menu" :size="24" />
      </button>
      <span class="text-lg font-bold text-brand">إدارة أوركيديا</span>
    </header>

    <div v-if="open" class="fixed inset-0 z-40 bg-ink/50 lg:hidden" aria-hidden="true" @click="setOpen(false)" />

    <aside id="admin-sidebar" ref="sidebar"
      class="fixed inset-y-0 start-0 z-50 flex w-[280px] max-w-[85vw] flex-col bg-white shadow-lift motion-safe:transition-transform motion-safe:duration-200 lg:z-20 lg:max-w-none lg:translate-x-0 lg:border-e lg:border-lavender lg:shadow-none"
      :class="open ? 'translate-x-0' : 'max-lg:invisible max-lg:ltr:-translate-x-full max-lg:rtl:translate-x-full'"
      :role="open ? 'dialog' : undefined" :aria-modal="open ? 'true' : undefined" :aria-label="open ? 'قائمة الإدارة' : undefined">
      <div class="bg-gradient-brand flex items-center gap-3 px-5 py-6 text-white">
        <img src="/brand/flower-white.png" alt="" width="36" height="36" class="h-9 w-9" />
        <NuxtLink to="/admin" class="text-xl font-extrabold text-white">إدارة أوركيديا</NuxtLink>
        <button v-if="open" type="button" class="ms-auto inline-flex min-h-11 min-w-11 items-center justify-center rounded-lg text-white lg:hidden"
          aria-label="إغلاق القائمة" @click="setOpen(false)">
          <AppIcon name="x" :size="22" />
        </button>
      </div>
      <nav class="flex-1 overflow-y-auto p-3" aria-label="قائمة الإدارة">
        <ul class="flex flex-col gap-1">
          <li v-for="link in links" :key="link.to">
            <NuxtLink :to="link.to" :aria-current="isActive(link.to) ? 'page' : undefined"
              class="flex min-h-11 items-center gap-3 rounded-lg px-3 py-2 font-semibold text-ink-soft hover:bg-lavender hover:text-brand-deep focus-visible:bg-lavender"
              :class="isActive(link.to) ? 'bg-lavender text-brand-deep' : ''">
              <AppIcon :name="link.icon" :size="20" />
              {{ link.label }}
              <span v-if="link.to === '/admin/messages' && unread" class="ms-auto grid min-w-6 place-items-center rounded-full bg-brand px-2 text-xs font-bold text-white">
                <bdi>{{ unread }}</bdi><span class="sr-only"> غير مقروءة</span>
              </span>
            </NuxtLink>
          </li>
        </ul>
      </nav>
      <div class="border-t border-lavender p-3">
        <NuxtLink to="/" class="flex min-h-11 items-center gap-3 rounded-lg px-3 py-2 font-semibold text-brand hover:bg-lavender focus-visible:bg-lavender">
          <AppIcon name="sparkle" :size="20" />
          عرض المتجر
        </NuxtLink>
      </div>
    </aside>

    <main id="admin-content" class="px-4 py-8 lg:ms-[280px] lg:px-8">
      <div class="mx-auto max-w-6xl"><slot /></div>
    </main>
  </div>
</template>
