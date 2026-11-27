// Display text for the server's free-delivery promotion. The server decides who gets free delivery;
// this only words it (Libyan feminine voice). Dates are shown in Tripoli time.
export interface PromotionText {
  title: string
  scope: 'Tripoli' | 'AllLibya' | string
  endsAtUtc: string
  minSubtotal?: string | null
}

export function promotionEnds(endsAtUtc: string) {
  const date = new Date(endsAtUtc)
  if (Number.isNaN(date.getTime())) return ''
  return new Intl.DateTimeFormat('ar-LY-u-nu-latn', {
    timeZone: 'Africa/Tripoli', weekday: 'long', day: 'numeric', month: 'long', hour: 'numeric', minute: '2-digit'
  }).format(date)
}

export function promotionText(promotion: PromotionText) {
  const where = promotion.scope === 'AllLibya' ? 'لكل ليبيا' : 'داخل طرابلس'
  const min = promotion.minSubtotal && Number(promotion.minSubtotal) > 0
    ? ` للطلبات من ${formatPrice(promotion.minSubtotal)} د.ل وفوق` : ''
  const ends = promotionEnds(promotion.endsAtUtc)
  return `${promotion.title}: توصيل مجاني ${where}${min}${ends ? ` — لين ${ends}` : ''}`
}
