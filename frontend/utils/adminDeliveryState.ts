const deliveryStates: Record<string, string> = {
  HandedOver: 'مع جهة التوصيل',
  Delivered: 'استلمه العميل',
  Returning: 'بانتظار الراجع',
  Returned: 'تم استلام الراجع'
}

export function adminDeliveryState(value: string) {
  return deliveryStates[value] ?? 'حالة توصيل غير معروفة'
}
