// Display-only price format for customers: «30» instead of «30.00», «29.95» stays as is.
// The value always comes from the server; this never rounds or recalculates money.
export function formatPrice(value: string | number | null | undefined) {
  if (value === null || value === undefined || value === '') return ''
  const number = Number(value)
  if (!Number.isFinite(number)) return String(value)
  return Number.isInteger(number) ? String(number) : number.toFixed(2)
}
