<script setup lang="ts">
type Chart = { labels: string[], data: number[] }
const days = ref<7 | 30>(7)
const options = [{ value: 7, label: 'آخر 7 أيام' }, { value: 30, label: 'آخر 30 يومًا' }] as const
const query = computed(() => ({ days: days.value }))
const { data, status, error, refresh } = useFetch<Chart>('/api/admin/sales-chart', { query, server: false, cache: 'no-store' })

// Wide and short so the chart stays readable without filling the screen.
const W = 1000
const H = 240
const pad = { top: 26, bottom: 28, side: 8 }
const nf = new Intl.NumberFormat('ar-LY', { maximumFractionDigits: 2 })
const points = computed(() => {
  const labels = data.value?.labels ?? []
  const values = data.value?.data ?? []
  const max = Math.max(1, ...values)
  const slot = (W - pad.side * 2) / Math.max(1, values.length)
  const barW = Math.max(2, slot * 0.64)
  return values.map((value, i) => {
    const h = Math.max(value > 0 ? 2 : 0, (value / max) * (H - pad.top - pad.bottom))
    return { x: pad.side + i * slot + (slot - barW) / 2, y: H - pad.bottom - h, w: barW, h, value, label: labels[i] ?? '',
      cx: pad.side + i * slot + slot / 2 }
  })
})
const total = computed(() => (data.value?.data ?? []).reduce((sum, v) => sum + v, 0))
const labelEvery = computed(() => days.value === 30 ? 5 : 1)
const summary = computed(() => `إجمالي المبيعات خلال ${days.value === 7 ? '7 أيام' : '30 يومًا'}: ${nf.format(total.value)} د.ل`)
</script>

<template>
  <section class="card p-4" aria-labelledby="sales-chart-title">
    <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
      <h2 id="sales-chart-title" class="text-xl font-semibold">المبيعات</h2>
      <div role="group" aria-label="مدة الرسم" class="flex gap-2">
        <button v-for="option in options" :key="option.value" type="button" :aria-pressed="days === option.value"
          class="chip border border-transparent" :class="days === option.value ? '!bg-brand !text-white' : 'hover:border-lavender-deep'"
          @click="days = option.value">{{ option.label }}</button>
      </div>
    </div>

    <p v-if="status === 'idle' || status === 'pending'" role="status" class="py-10 text-center text-ink-soft">جاري تحميل الرسم…</p>
    <div v-else-if="error" role="alert" class="py-6 text-center">
      <p class="text-danger">تعذر تحميل رسم المبيعات.</p>
      <button type="button" class="btn-secondary mt-3" @click="refresh()">إعادة المحاولة</button>
    </div>
    <template v-else-if="data">
      <p class="mb-2 text-sm text-ink-soft" role="status">{{ summary }}</p>
      <svg :viewBox="`0 0 ${W} ${H}`" class="h-auto w-full" role="img" :aria-label="summary" preserveAspectRatio="xMidYMid meet">
        <defs>
          <linearGradient id="sales-bar" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stop-color="var(--color-brand-soft)" />
            <stop offset="100%" stop-color="var(--color-brand-deep)" />
          </linearGradient>
        </defs>
        <line :x1="pad.side" :x2="W - pad.side" :y1="H - pad.bottom" :y2="H - pad.bottom" stroke="var(--color-lavender-deep)" />
        <g v-for="(p, i) in points" :key="i">
          <rect :x="p.x" :y="p.y" :width="p.w" :height="p.h" rx="3" fill="url(#sales-bar)">
            <title>{{ p.label }}: {{ nf.format(p.value) }} د.ل</title>
          </rect>
          <text v-if="p.value > 0 && (days === 7 || p.h > 18)" :x="p.cx" :y="p.y - 6" text-anchor="middle" font-size="12" font-weight="700"
            fill="var(--color-brand-deep)" aria-hidden="true">{{ nf.format(p.value) }}</text>
          <text v-if="i % labelEvery === 0" :x="p.cx" :y="H - 8" text-anchor="middle" font-size="11" fill="var(--color-ink-soft)">{{ p.label }}</text>
        </g>
      </svg>
      <table class="sr-only">
        <caption>{{ summary }}</caption>
        <thead><tr><th scope="col">اليوم</th><th scope="col">المبيعات (د.ل)</th></tr></thead>
        <tbody>
          <tr v-for="(p, i) in points" :key="i"><th scope="row">{{ p.label }}</th><td>{{ nf.format(p.value) }}</td></tr>
        </tbody>
      </table>
    </template>
  </section>
</template>
