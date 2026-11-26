// «قبل 3 أيام», «أمس», «الآن». Null or invalid dates show «غير معروف» (accounts older than activity tracking).
export function adminRelativeDate(iso: string | null | undefined, now: number = Date.now()) {
  if (!iso) return 'غير معروف'
  const time = new Date(iso).getTime()
  if (Number.isNaN(time)) return 'غير معروف'
  const seconds = Math.round((time - now) / 1000)
  const format = new Intl.RelativeTimeFormat('ar', { numeric: 'auto' })
  const steps: [Intl.RelativeTimeFormatUnit, number][] = [
    ['second', 60], ['minute', 60], ['hour', 24], ['day', 30], ['month', 12]
  ]
  let value = seconds
  for (const [unit, size] of steps) {
    if (Math.abs(value) < size) return unit === 'second' ? 'الآن' : format.format(value, unit)
    value = Math.round(value / size)
  }
  return format.format(value, 'year')
}
