<script setup lang="ts" generic="T">
// Lightweight CSS scroll-snap carousel (no dependencies). RTL-safe: positions are computed from
// bounding rects, never from raw scrollLeft sign, so it works in dir=rtl (negative scrollLeft) and ltr.
interface PerView { base: number; sm?: number; md?: number; lg?: number; xl?: number }

const props = withDefaults(defineProps<{
  items: T[]
  label: string
  perView?: PerView
  gap?: number
  autoplay?: number
  loop?: boolean
  dots?: boolean
  arrows?: boolean
  dotsTone?: 'light' | 'dark'
}>(), {
  perView: () => ({ base: 1, sm: 2, lg: 3, xl: 4 }),
  gap: 16,
  autoplay: 0,
  loop: false,
  dots: false,
  arrows: false,
  dotsTone: 'dark'
})

defineSlots<{ item(props: { item: T; index: number }): unknown }>()

const track = ref<HTMLElement | null>(null)
const active = ref(0)
const perViewNow = ref(props.perView.base)
const paused = ref(false)
let timer: ReturnType<typeof setInterval> | undefined
let reduced = false
let raf = 0

// Cascade base -> sm -> md -> lg -> xl; each breakpoint inherits the previous one when unset.
const cssVars = computed(() => {
  const p = props.perView
  const sm = p.sm ?? p.base
  const md = p.md ?? sm
  const lg = p.lg ?? md
  const xl = p.xl ?? lg
  return {
    '--gap': `${props.gap}px`,
    '--pv-base': p.base, '--pv-sm': sm, '--pv-md': md, '--pv-lg': lg, '--pv-xl': xl
  }
})

const pageCount = computed(() => Math.max(1, props.items.length - perViewNow.value + 1))
const slides = () => (track.value ? Array.from(track.value.children) as HTMLElement[] : [])

function readPerView() {
  if (!track.value) return
  const v = parseInt(getComputedStyle(track.value).getPropertyValue('--pv'), 10)
  if (v > 0) perViewNow.value = v
}

function goTo(index: number, smooth = true) {
  const el = track.value
  const slide = slides()[index]
  if (!el || !slide) return
  const t = el.getBoundingClientRect()
  const s = slide.getBoundingClientRect()
  const rtl = getComputedStyle(el).direction === 'rtl'
  const delta = rtl ? s.right - t.right : s.left - t.left
  el.scrollTo({ left: el.scrollLeft + delta, behavior: smooth && !reduced ? 'smooth' : 'auto' })
  active.value = index
}

function next() {
  const last = pageCount.value - 1
  if (active.value >= last) { if (props.loop) goTo(0) } else goTo(active.value + 1)
}
function prev() {
  if (active.value <= 0) { if (props.loop) goTo(pageCount.value - 1) } else goTo(active.value - 1)
}

function onScroll() {
  cancelAnimationFrame(raf)
  raf = requestAnimationFrame(() => {
    const el = track.value
    if (!el) return
    const t = el.getBoundingClientRect()
    const rtl = getComputedStyle(el).direction === 'rtl'
    let best = 0
    let bestDist = Infinity
    slides().forEach((s, i) => {
      const r = s.getBoundingClientRect()
      const d = Math.abs(rtl ? r.right - t.right : r.left - t.left)
      if (d < bestDist) { bestDist = d; best = i }
    })
    active.value = Math.min(best, pageCount.value - 1)
  })
}

function onKey(e: KeyboardEvent) {
  // Visual direction: in RTL the ArrowLeft key moves forward.
  const rtl = track.value ? getComputedStyle(track.value).direction === 'rtl' : true
  if (e.key === 'ArrowLeft') { e.preventDefault(); if (rtl) next(); else prev() }
  else if (e.key === 'ArrowRight') { e.preventDefault(); if (rtl) prev(); else next() }
}

function stopTimer() { if (timer) { clearInterval(timer); timer = undefined } }
function startTimer() {
  stopTimer()
  if (props.autoplay > 0 && !reduced && pageCount.value > 1) {
    timer = setInterval(() => { if (!paused.value && !document.hidden) next() }, props.autoplay)
  }
}
function onVisibility() { paused.value = document.hidden }

let mq: MediaQueryList | undefined
function onMotionChange() { reduced = !!mq?.matches; startTimer() }

onMounted(() => {
  mq = window.matchMedia('(prefers-reduced-motion: reduce)')
  reduced = mq.matches
  mq.addEventListener('change', onMotionChange)
  document.addEventListener('visibilitychange', onVisibility)
  window.addEventListener('resize', readPerView)
  readPerView()
  startTimer()
})
onBeforeUnmount(() => {
  stopTimer()
  cancelAnimationFrame(raf)
  mq?.removeEventListener('change', onMotionChange)
  document.removeEventListener('visibilitychange', onVisibility)
  window.removeEventListener('resize', readPerView)
})
watch(() => [props.autoplay, props.items.length], startTimer)
</script>

<template>
  <div
    class="app-carousel relative"
    role="region"
    aria-roledescription="carousel"
    :aria-label="label"
    :style="cssVars"
    @mouseenter="paused = true"
    @mouseleave="paused = false"
    @focusin="paused = true"
    @focusout="paused = false"
    @touchstart.passive="paused = true"
    @touchend.passive="paused = false"
    @keydown="onKey"
  >
    <div ref="track" class="app-carousel__track" @scroll.passive="onScroll">
      <div
        v-for="(item, i) in items"
        :key="i"
        class="app-carousel__slide"
        role="group"
        aria-roledescription="slide"
        :aria-label="`${i + 1} من ${items.length}`"
      >
        <slot name="item" :item="item" :index="i" />
      </div>
    </div>

    <template v-if="arrows && items.length > 1">
      <!-- Previous on the start (right in RTL) edge, next on the end (left in RTL) edge. -->
      <button
        type="button" aria-label="السابق"
        class="app-carousel__arrow start-2"
        :disabled="!loop && active <= 0"
        @click="prev"
      >
        <svg class="rtl:rotate-180" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 6l-6 6 6 6" /></svg>
      </button>
      <button
        type="button" aria-label="التالي"
        class="app-carousel__arrow end-2"
        :disabled="!loop && active >= pageCount - 1"
        @click="next"
      >
        <svg class="rtl:rotate-180" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M9 6l6 6-6 6" /></svg>
      </button>
    </template>

    <div v-if="dots && pageCount > 1" class="mt-4 flex flex-wrap items-center justify-center">
      <button
        v-for="n in pageCount"
        :key="n"
        type="button"
        class="app-carousel__dot"
        :class="dotsTone === 'light' ? 'is-light' : 'is-dark'"
        :aria-label="`الانتقال إلى الشريحة ${n}`"
        :aria-current="active === n - 1 ? 'true' : undefined"
        @click="goTo(n - 1)"
      ><span /></button>
    </div>
  </div>
</template>

<style scoped>
.app-carousel__track {
  --pv: var(--pv-base);
  display: flex; gap: var(--gap);
  overflow-x: auto; scroll-snap-type: x mandatory;
  overscroll-behavior-x: contain;
  scrollbar-width: none; -ms-overflow-style: none;
  padding-block: 8px; margin-block: -8px; /* room for card shadows without layout shift */
}
.app-carousel__track::-webkit-scrollbar { display: none; }
.app-carousel__slide {
  flex: 0 0 calc((100% - (var(--pv) - 1) * var(--gap)) / var(--pv));
  min-width: 0; scroll-snap-align: start;
}
@media (min-width: 640px) { .app-carousel__track { --pv: var(--pv-sm); } }
@media (min-width: 768px) { .app-carousel__track { --pv: var(--pv-md); } }
@media (min-width: 992px) { .app-carousel__track { --pv: var(--pv-lg); } }
@media (min-width: 1200px) { .app-carousel__track { --pv: var(--pv-xl); } }

.app-carousel__arrow {
  position: absolute; top: 50%; translate: 0 -50%; z-index: 2;
  width: 44px; height: 44px; border-radius: 999px; border: 0; cursor: pointer;
  display: inline-flex; align-items: center; justify-content: center;
  background: #fff; color: var(--color-brand); box-shadow: var(--shadow-soft);
}
.app-carousel__arrow:hover:not(:disabled) { background: var(--color-lavender); }
.app-carousel__arrow:disabled { opacity: 0.4; cursor: not-allowed; }

/* 44px hit area, small visible pill. Active dot stretches into a pill. */
.app-carousel__dot { width: 44px; height: 44px; display: inline-flex; align-items: center; justify-content: center; background: none; border: 0; cursor: pointer; }
.app-carousel__dot span { display: block; width: 10px; height: 10px; border-radius: 999px; background: var(--color-lavender-deep); }
.app-carousel__dot.is-light span { background: rgb(255 255 255 / 0.45); }
.app-carousel__dot[aria-current='true'] span { width: 26px; background: var(--color-brand); }
.app-carousel__dot.is-light[aria-current='true'] span { background: #fff; }
@media (prefers-reduced-motion: no-preference) {
  .app-carousel__dot span { transition: width 200ms ease-out, background-color 200ms ease-out; }
}
</style>
