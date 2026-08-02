const entityRoutes: Record<string, (id: string) => string> = {
  customer: id => `/customers/${id}`,
  customers: id => `/customers/${id}`,
  vehicle: id => `/vehicles/${id}`,
  vehicles: id => `/vehicles/${id}`,
  employee: id => `/employees/${id}`,
  employees: id => `/employees/${id}`,
  supplier: id => `/suppliers/${id}`,
  suppliers: id => `/suppliers/${id}`,
  part: id => `/inventory/${id}`,
  parts: id => `/inventory/${id}`,
  warehouselocation: id => `/warehouse-locations/${id}`,
  warehouselocations: id => `/warehouse-locations/${id}`,
  warehousestocktake: id => `/warehouse-locations/${id}`,
  repairorder: id => `/repair-orders/${id}`,
  repairorders: id => `/repair-orders/${id}`,
  invoice: id => `/invoices/${id}`,
  invoices: id => `/invoices/${id}`,
  cashtransaction: id => `/finance/${id}`,
  cashtransactions: id => `/finance/${id}`,
  cashcategory: id => `/finance/categories/${id}`,
  cashcategories: id => `/finance/categories/${id}`,
  coupon: id => `/coupons/${id}`,
  coupons: id => `/coupons/${id}`,
  user: id => `/users/${id}`,
  users: id => `/users/${id}`,
  vehiclebrand: id => `/catalogs/vehicle-brands/${id}`,
  vehiclebrands: id => `/catalogs/vehicle-brands/${id}`,
  vehiclemodel: id => `/catalogs/vehicle-models/${id}`,
  vehiclemodels: id => `/catalogs/vehicle-models/${id}`,
  partbrand: id => `/catalogs/part-brands/${id}`,
  partbrands: id => `/catalogs/part-brands/${id}`,
  partcategory: id => `/catalogs/part-categories/${id}`,
  partcategories: id => `/catalogs/part-categories/${id}`,
  servicecategory: id => `/catalogs/service-categories/${id}`,
  servicecategories: id => `/catalogs/service-categories/${id}`,
  loyaltytier: id => `/loyalty/tiers/${id}`,
  loyaltytiers: id => `/loyalty/tiers/${id}`,
  loyaltyrule: id => `/loyalty/rules/${id}`,
  loyaltyrules: id => `/loyalty/rules/${id}`
}

const normalizeEntityType = (type?: string) =>
  (type || '').toLowerCase().replace(/[^a-z]/g, '')

export const entityDetailRoute = (type?: string, id?: string) => {
  if (!id) return undefined
  return entityRoutes[normalizeEntityType(type)]?.(id)
}
