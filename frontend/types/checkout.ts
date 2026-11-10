export interface CheckoutDestination {
  key: string
  city: string
  area: string
  service?: 'women' | 'men' | null
  isPickup?: boolean
}

export interface CheckoutState {
  checkoutToken: string
  quoteToken: string | null
  productsTotal: string
  shippingFee: string | null
  total: string | null
  cartRevision: string
  shippingRates: { id: number; city: string; price: string; total: string }[]
  destinations: CheckoutDestination[] | null
  errors: string[]
}

export interface CheckoutReceipt {
  orderId: number
  quantity: number
  productsTotal: string
  shippingFee: string
  total: string
  statusLabel: string
  paymentMethod: string
}
