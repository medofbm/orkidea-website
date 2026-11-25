export interface DeliveryArea {
  area: string
  price: string
}

export interface DeliveryGroup {
  city: string
  minPrice: string
  maxPrice: string
  areas: DeliveryArea[]
}

// GET /api/delivery/summary. `groups` is optional so an older API still works.
export interface DeliverySummary {
  mode: 'rates' | 'carrier'
  minPrice?: string
  maxPrice?: string
  cities?: number
  areas?: number
  rates?: { city: string, price: string }[]
  groups?: DeliveryGroup[]
}
