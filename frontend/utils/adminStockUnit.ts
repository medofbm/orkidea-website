export function adminStockUnit(unit: 'ml' | 'piece') {
  return unit === 'ml' ? 'مل' : 'قطعة'
}
