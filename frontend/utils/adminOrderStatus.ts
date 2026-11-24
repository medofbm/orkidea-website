export function adminOrderStatus(status: string) {
  const labels: Record<string, string> = {
    Pending: 'جديد',
    Processing: 'قيد التجهيز',
    Shipped: 'قيد التوصيل',
    Delivered: 'مسلّم',
    Cancelled: 'ملغي'
  }
  return labels[status] ?? 'تحتاج الحالة إلى مراجعة'
}

// Badge colour for an order status (same classes in the list and the detail page).
export function adminOrderStatusClass(status: string) {
  const classes: Record<string, string> = {
    Pending: 'badge-status-pending',
    Processing: 'badge-status-processing',
    Shipped: 'badge-status-shipped',
    Delivered: 'badge-status-delivered',
    Cancelled: 'badge-status-cancelled'
  }
  return classes[status] ?? ''
}

// Order dates in Libya time, e.g. «24 نوفمبر 2026، 12:00».
export function adminOrderDate(iso: string) {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return ''
  return new Intl.DateTimeFormat('ar-LY', {
    day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: 'Africa/Tripoli'
  }).format(date)
}

// Digits for tel: and wa.me links. A local Libyan number (09…) becomes 2189….
export function adminPhoneDigits(phone: string) {
  const digits = phone.replace(/\D/g, '')
  return /^09\d{8}$/.test(digits) ? '218' + digits.slice(1) : digits
}
