export interface CatalogueProduct {
  id: number
  name: string
  description: string | null
  category: string | null
  images: string[]
  variants: { id: number; size: string; price: string; currency: string }[]
}
export interface CataloguePage {
  items: CatalogueProduct[]
  page: number
  pageSize: number
  total: number
}
