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
