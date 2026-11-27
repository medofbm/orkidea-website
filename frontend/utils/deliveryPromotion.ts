// Display text for the server's free-delivery promotion. The server decides who gets free delivery;
// this only words it (Libyan feminine voice).
export interface PromotionText {
  title: string
  scope: 'Tripoli' | 'AllLibya' | string
  endsAtUtc: string
  minSubtotal?: string | null
}

export function promotionText(promotion: PromotionText) {
  const where = promotion.scope === 'AllLibya' ? 'لكل ليبيا' : 'داخل طرابلس'
  const min = promotion.minSubtotal && Number(promotion.minSubtotal) > 0
    ? ` للطلبات من ${formatPrice(promotion.minSubtotal)} د.ل وفوق` : ''
  return `${promotion.title}: توصيل مجاني ${where}${min}`
}
