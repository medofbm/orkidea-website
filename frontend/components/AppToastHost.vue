<script setup lang="ts">
import type { ToastType } from '~/composables/useToast'

const { items, dismiss } = useToast()

interface Timer { handle: ReturnType<typeof setTimeout> | null; remaining: number; startedAt: number }
const timers = new Map<number, Timer>()
const paused = reactive<Record<number, boolean>>({})

function start(id: number) {
  const t = timers.get(id)
  if (!t) return
  t.startedAt = Date.now()
  t.handle = setTimeout(() => { clear(id); dismiss(id) }, t.remaining)
}
function clear(id: number) {
  const t = timers.get(id)
  if (t?.handle) clearTimeout(t.handle)
  timers.delete(id)
  delete paused[id]
}
function pause(id: number) {
  const t = timers.get(id)
  if (!t || !t.handle) return
  clearTimeout(t.handle)
  t.handle = null
  t.remaining = Math.max(200, t.remaining - (Date.now() - t.startedAt))
  paused[id] = true
}
function resume(id: number) {
  const t = timers.get(id)
  if (!t || t.handle) return
  paused[id] = false
  start(id)
}

// Timers run on the client only.
if (import.meta.client) {
  watch(items, (list) => {
    const live = new Set(list.map(i => i.id))
    for (const item of list) {
      if (!timers.has(item.id)) {
        timers.set(item.id, { handle: null, remaining: item.duration, startedAt: 0 })
        start(item.id)
      }
    }
    for (const id of [...timers.keys()]) if (!live.has(id)) clear(id)
  }, { deep: true, immediate: true })
}
onBeforeUnmount(() => { for (const id of [...timers.keys()]) clear(id) })

const tone: Record<ToastType, string> = {
  success: 'bg-success/12 text-success',
  error: 'bg-danger/12 text-danger',
  info: 'bg-info/12 text-info',
  warning: 'bg-warning/12 text-warning',
}
const bar: Record<ToastType, string> = {
  success: 'bg-success',
  error: 'bg-danger',
  info: 'bg-info',
  warning: 'bg-warning',
}
</script>

<template>
  <div
    role="status"
    aria-live="polite"
    class="toast-region pointer-events-none fixed inset-x-4 z-[60] sm:inset-x-auto sm:start-4 sm:w-96"
  >
    <TransitionGroup name="toast" tag="div" class="flex flex-col gap-3">
      <div
        v-for="t in items"
        :key="t.id"
        :role="t.type === 'error' ? 'alert' : undefined"
        class="card-glass pointer-events-auto relative overflow-hidden"
        @mouseenter="pause(t.id)"
        @mouseleave="resume(t.id)"
        @focusin="pause(t.id)"
        @focusout="resume(t.id)"
      >
        <div class="flex items-center gap-3 py-2 ps-4 pe-1">
          <span class="inline-flex size-9 shrink-0 items-center justify-center rounded-full" :class="tone[t.type]" aria-hidden="true">
            <svg v-if="t.type === 'success'" viewBox="0 0 24 24" class="size-5" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12.5l4.5 4.5L19 7.5" /></svg>
            <svg v-else-if="t.type === 'error'" viewBox="0 0 24 24" class="size-5" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M6 6l12 12M18 6L6 18" /></svg>
            <svg v-else-if="t.type === 'warning'" viewBox="0 0 24 24" class="size-5" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M12 8v5M12 16.5v.01" /><path d="M10.3 4.2L2.9 17.5A2 2 0 0 0 4.6 20.5h14.8a2 2 0 0 0 1.7-3L13.7 4.2a2 2 0 0 0-3.4 0z" /></svg>
            <svg v-else viewBox="0 0 24 24" class="size-5" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9" /><path d="M12 11v5M12 7.5v.01" /></svg>
          </span>
          <p class="min-w-0 flex-1 py-2 text-sm font-semibold text-ink">{{ t.message }}</p>
          <NuxtLink
            v-if="t.action"
            :to="t.action.to"
            class="inline-flex min-h-11 shrink-0 items-center rounded-full px-3 text-sm font-bold text-brand hover:bg-lavender"
            @click="dismiss(t.id)"
          >{{ t.action.label }}</NuxtLink>
          <button
            type="button"
            aria-label="إغلاق"
            class="inline-flex size-11 shrink-0 items-center justify-center rounded-full text-ink-soft hover:bg-lavender"
            @click="dismiss(t.id)"
          >
            <svg viewBox="0 0 24 24" class="size-4" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" aria-hidden="true"><path d="M6 6l12 12M18 6L6 18" /></svg>
          </button>
        </div>
        <div class="toast-bar h-1 w-full" aria-hidden="true">
          <div
            class="toast-bar-fill h-full w-full"
            :class="bar[t.type]"
            :style="{ animationDuration: `${t.duration}ms`, animationPlayState: paused[t.id] ? 'paused' : 'running' }"
          />
        </div>
      </div>
    </TransitionGroup>
  </div>
</template>

<style scoped>
.toast-region { top: calc(env(safe-area-inset-top, 0px) + 4.75rem); }
.toast-bar-fill { transform-origin: inline-start; animation: toast-shrink linear forwards; }
[dir='rtl'] .toast-bar-fill { transform-origin: right; }
@keyframes toast-shrink { from { transform: scaleX(1); } to { transform: scaleX(0); } }

@media (prefers-reduced-motion: no-preference) {
  .toast-enter-active, .toast-leave-active { transition: opacity 220ms ease-out, transform 220ms ease-out; }
  .toast-leave-active { position: absolute; inset-inline: 0; }
  .toast-move { transition: transform 220ms ease-out; }
  .toast-enter-from, .toast-leave-to { opacity: 0; transform: translateY(-12px); }
}
@media (prefers-reduced-motion: reduce) {
  .toast-bar-fill { animation: none; }
}
</style>
