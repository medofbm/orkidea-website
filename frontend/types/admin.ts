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

export interface AdminOrderDetails {
  order: AdminOrderSummary
  whatsAppNumber: string
  shippingAddress: string
  paymentMethod: string | null
  productsTotal: string
  items: { id: number; productId: number; quantity: number; variantSize: string | null;
    productName: string | null; unitPrice: string }[]
}
