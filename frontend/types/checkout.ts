export interface CheckoutDestination {
  key: string
  city: string
  area: string
  service?: 'women' | 'men' | null
  isPickup?: boolean
  /** Server flag: the quoted fee for this destination is waived by the active promotion. */
  freeDelivery?: boolean
}

export interface DeliveryPromotionInfo {
  title: string
  scope: 'Tripoli' | 'AllLibya'
  endsAtUtc: string
  minSubtotal: string | null
  meetsMinimum?: boolean
}

export interface CheckoutState {
  checkoutToken: string
  quoteToken: string | null
  productsTotal: string
  shippingFee: string | null
  /** Normal fee when the server's free-delivery promotion made shippingFee 0. */
  originalShippingFee?: string | null
  promotion?: DeliveryPromotionInfo | null
  total: string | null
  cartRevision: string
  shippingRates: { id: number; city: string; price: string; originalPrice?: string | null; total: string; region?: string | null; area?: string | null }[]
  destinations: CheckoutDestination[] | null
  errors: string[]
}

export interface CheckoutReceiptLine {
  productName: string
  size: string | null
  quantity: number
  unitPrice: string
  subtotal: string
}

export interface CheckoutReceipt {
  orderId: number
  quantity: number
  productsTotal: string
  shippingFee: string
  originalShippingFee?: string | null
  total: string
  statusLabel: string
  paymentMethod: string
  /** Guest receipt whose order is not linked to an account yet. */
  canCreateAccount?: boolean
  // Optional so an older API without product lines still renders.
  lines?: CheckoutReceiptLine[]
}
