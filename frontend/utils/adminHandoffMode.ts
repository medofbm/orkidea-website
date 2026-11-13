export function adminHandoffMode(value: string | null | undefined) {
  return value === 'OfficeDropoff' ? 'تسليم إلى مكتب الشركة' :
    value === 'CarrierCollection' ? 'استلام مندوب الشركة' : 'طريقة التسليم للشركة غير مسجلة'
}
