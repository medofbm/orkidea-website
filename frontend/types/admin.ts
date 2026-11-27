export interface AdminOrderSummary {
  id: number
  customerName: string
  city: string
  orderDate: string
  status: string
  source: string
  total: string
  shippingFee: string
  originalShippingFee?: string | null
}

export interface AdminDashboard {
  productsValueLast30Days: string
  pendingOrders: number
  productsWithoutVariants: number
  accounts: number
  recentOrders: AdminOrderSummary[]
  // Added for the detailed dashboard; optional so an older API still renders.
  ordersLast30Days?: number
  averageOrderValue30Days?: string | null
  ordersToday?: number
  valueToday?: string
  weeklyChangePercent?: number | null
  statusCounts?: Record<'Pending' | 'Processing' | 'Shipped' | 'Delivered' | 'Cancelled', number>
  topProducts?: { productId: number; name: string; quantity: number; value: string }[]
  lowestStock?: { productId: number; name: string; unit: string; available: string }[]
  unreadMessages?: number
  reviewsCount?: number
  reviewsAverage?: number | null
  unorderableSizes?: { productId: number; name: string; size: string }[]
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
  handoffMode: 'OfficeDropoff' | 'CarrierCollection' | null
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
  category: string | null
  variants: { productVariantId: number; size: string; stockUnitsPerItem: number | null }[]
  movements: { stockMovementId: string; orderId: number | null; kind: string; onHandDelta: number;
    reservedDelta: number; reason: string; actorName: string | null; createdAtUtc: string }[]
  total: number
  page: number
  pages: number
}

export interface AdminProductsPage {
  items: {
    productId: number
    name: string
    category: string | null
    imageUrl: string | null
    variants: number
    pricedVariants?: number
    hasDescription?: boolean
    isArchived?: boolean
  }[]
  page: number
  pages: number
  total: number
}

export interface AdminProductDetails {
  productId: number
  name: string
  description: string | null
  categoryId: number | null
  imageUrl: string | null
  gallery: string[]
  version: string
  isArchived?: boolean
  variants: { productVariantId: number; size: string; price: string; isArchived?: boolean }[]
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
  carrierChoice?: { area: string; serviceKind: 'women' | 'men' | null; isPickup: boolean } | null
  paymentMethod: string | null
  productsTotal: string
  items: { id: number; productId: number; quantity: number; variantSize: string | null;
    productName: string | null; unitPrice: string }[]
}

// Accounts (admin «الزبائن» and «المديرين»). Dates are UTC ISO strings; null means «غير معروف».
export interface AdminUserSummary {
  id: string
  email: string | null
  phoneNumber: string | null
  emailConfirmed: boolean
  createdAtUtc: string | null
  lastSignInAtUtc: string | null
  signInCount: number | null
  isAdmin: boolean
  isSuperAdmin: boolean
  orderCount: number
  totalSpent: string
}

export interface AdminUsersPage {
  items: AdminUserSummary[]
  page: number
  pages: number
  total: number
  pageSize: number
  isSuperAdmin: boolean
}

export interface AdminUserDetails {
  user: AdminUserSummary
  orders: { id: number, orderDate: string, status: string, total: string }[]
  isSuperAdmin: boolean
}

export interface AdminAdminsList {
  items: {
    id: string
    email: string | null
    emailConfirmed: boolean
    isSuperAdmin: boolean
    isYou: boolean
    hasPassword: boolean
    createdAtUtc: string | null
    lastSignInAtUtc: string | null
  }[]
  isSuperAdmin: boolean
  superAdminConfigured: boolean
}
