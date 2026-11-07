export interface AdminOrderSummary {
  id: number
  customerName: string
  city: string
  orderDate: string
  status: string
  source: string
  total: string
  shippingFee: string
}

export interface AdminDashboard {
  productsValueLast30Days: string
  pendingOrders: number
  productsWithoutVariants: number
  accounts: number
  recentOrders: AdminOrderSummary[]
}

export interface AdminOrdersPage {
  items: AdminOrderSummary[]
  page: number
  pages: number
  total: number
  pageSize: number
}

export interface AdminDelivery {
  state: string
  mode: 'Company' | 'Self'
  carrierName: string | null
  shipmentReference: string | null
}

export interface AdminDeliveriesPage {
  items: (AdminDelivery & { orderId: number; customerName: string; city: string; updatedAtUtc: string })[]
  page: number
  pages: number
  total: number
}

export interface AdminDeliveryEvent extends Omit<AdminDelivery, 'state'> {
  revision: number
  action: string
  newState: string
  reason: string | null
  actorName: string | null
  changedAtUtc: string
}

export interface AdminStockAccount {
  unit: 'ml' | 'piece'
  onHand: number
  reserved: number
  available: number
  revision: number
}

export interface AdminStockProduct {
  productId: number
  name: string
  account: AdminStockAccount | null
}

export interface AdminStockPage {
  items: AdminStockProduct[]
  total: number
  page: number
  pages: number
}

export interface AdminStockDetails extends AdminStockProduct {
  variants: { productVariantId: number; size: string; stockUnitsPerItem: number | null }[]
  movements: { stockMovementId: string; orderId: number | null; kind: string; onHandDelta: number;
    reservedDelta: number; reason: string; actorName: string | null; createdAtUtc: string }[]
  total: number
  page: number
  pages: number
}

export interface AdminOrderDetails {
  order: AdminOrderSummary
  statusRevision: number
  stockState: string
  hasDelivery: boolean
  delivery: AdminDelivery | null
  deliveryHistory: AdminDeliveryEvent[]
  history: { revision: number; previousStatus: string; newStatus: string;
    reason: string | null; changedAtUtc: string; actorName: string | null }[]
  whatsAppNumber: string
  shippingAddress: string
  paymentMethod: string | null
  productsTotal: string
  items: { id: number; productId: number; quantity: number; variantSize: string | null;
    productName: string | null; unitPrice: string }[]
}
