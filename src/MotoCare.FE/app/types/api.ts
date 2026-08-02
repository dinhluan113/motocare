export interface ApiEnvelope<T> {
  success: boolean
  data: T
  message?: string
  code?: string
  errors?: Record<string, string[]>
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface AuthUser {
  id: string
  username: string
  fullName: string
  employeeId?: string
  roles: string[]
  lastLoginAt?: string
}

export interface LoginResult extends AuthUser {
  accessToken: string
  expiresAt: string
  userId: string
}

export interface BaseDocument {
  id: string
  createdAt: string
  updatedAt: string
  isDeleted: boolean
}

export interface LocationOption {
  code: string
  name: string
}

export interface AddressDetails {
  addressLine: string
  countryCode: string
  countryName: string
  regionCode: string
  regionName: string
  areaCode: string
  areaName: string
}

export interface Customer extends BaseDocument {
  code: string
  fullName: string
  phone: string
  email?: string
  address?: string
  addressDetails?: AddressDetails
  dateOfBirth?: string
  gender?: string
  taxCode?: string
  notes?: string
  loyaltyAccountId?: string
  loyaltyTierCode?: string
  loyaltyPointBalance: number
  isActive: boolean
}

export interface Vehicle extends BaseDocument {
  customerId: string
  vehicleModelId: string
  licensePlate: string
  frameNumber?: string
  engineNumber?: string
  manufactureYear?: number
  color?: string
  odometer?: number
  purchaseDate?: string
  notes?: string
  isActive: boolean
}

export interface Employee extends BaseDocument {
  employeeCode: string
  fullName: string
  phone: string
  email?: string
  address?: string
  addressDetails?: AddressDetails
  position: string
  skillLevel?: string
  specialties: string[]
  status: 'Active' | 'OnLeave' | 'Inactive'
}

export interface VehicleBrand extends BaseDocument {
  code: string
  name: string
  country?: string
  isActive: boolean
}

export interface VehicleModel extends BaseDocument {
  brandId: string
  code: string
  name: string
  vehicleType?: string
  engineCapacityCc?: number
  isActive: boolean
}

export interface PartBrand extends BaseDocument {
  code: string
  name: string
  country?: string
  isActive: boolean
}

export interface Supplier extends BaseDocument {
  code: string
  name: string
  phone: string
  taxCode?: string
  address?: string
  addressDetails?: AddressDetails
  notes?: string
  isActive: boolean
}

export interface PartCategory extends BaseDocument {
  code: string
  name: string
  description?: string
  specificationDefinitions: Array<{
    code: string
    name: string
    dataType: 'Text' | 'Number' | 'Boolean' | 'Selection'
    options: string[]
    unit?: string
    isRequired: boolean
  }>
  isActive: boolean
}

export interface ServiceCategory extends BaseDocument {
  code: string
  name: string
  defaultPrice: number
  description?: string
  isActive: boolean
}

export interface Part extends BaseDocument {
  code: string
  barcode?: string
  name: string
  partBrandId: string
  partCategoryId: string
  supplierIds: string[]
  specifications: Array<{
    code: string
    name: string
    unit?: string
    value: string
  }>
  unit: string
  importPrice: number
  stockPrice: number
  salePrice: number
  quantityOnHand: number
  minQuantity: number
  replacementIntervalKm?: number
  replacementIntervalMonths?: number
  notes?: string
  isActive: boolean
}

export interface PartReplacementReminder {
  customerId: string
  customerName: string
  customerPhone?: string
  vehicleId: string
  licensePlate: string
  currentOdometer?: number
  partId: string
  partCode: string
  partName: string
  installedAt: string
  installedOdometer?: number
  dueAt?: string
  dueOdometer?: number
  remainingDays?: number
  remainingKm?: number
  isOverdue: boolean
  isDueSoon: boolean
  lastRepairOrderId: string
}

export interface InventoryTransaction extends BaseDocument {
  code: string
  partId: string
  type: 'Receipt' | 'RepairIssue' | 'RepairReturn' | 'AdjustmentIncrease' | 'AdjustmentDecrease'
  quantity: number
  unitCost: number
  referenceType?: string
  referenceId?: string
  supplierId?: string
  transactionDate: string
  notes?: string
}

export interface PurchaseExpenseItem {
  id?: string
  partId: string
  partCode?: string
  partName?: string
  quantity: number
  unitCost: number
  lineTotal?: number
  salePriceSnapshot?: number
  profitRate?: number
  isLowProfit?: boolean
}

export interface CashTransaction extends BaseDocument {
  code: string
  type: 'Income' | 'Expense'
  category: string
  cashCategoryId?: string
  purpose: 'Other' | 'PartsPurchase'
  supplierId?: string
  transactionDate: string
  amount: number
  paymentMethod: string
  description: string
  attachmentUrl?: string
  status: 'New' | 'Confirmed' | 'Cancelled' | 'Approved'
  purchaseItems: PurchaseExpenseItem[]
  confirmedAt?: string
  confirmedBy?: string
}

export interface CashCategory extends BaseDocument {
  code: string
  name: string
  scope: 'Income' | 'Expense' | 'Both'
  description?: string
  isActive: boolean
}

export type RepairOrderStatus =
  | 'Received'
  | 'Inspecting'
  | 'AwaitingApproval'
  | 'Repairing'
  | 'AwaitingParts'
  | 'Completed'
  | 'Delivered'
  | 'Cancelled'

export interface RepairOrderItem {
  id: string
  itemType: 'Service' | 'Part'
  serviceId?: string
  partId?: string
  description: string
  quantity: number
  unitPrice: number
  discountAmount: number
  discountType: 'Amount' | 'Percentage'
  discountValue: number
  lineTotal: number
  assignedEmployeeId?: string
  technicianNotes?: string
  workStatus: 'Pending' | 'InProgress' | 'Completed' | 'Cancelled'
  inventoryIssued: boolean
}

export interface RepairOrder extends BaseDocument {
  code: string
  customerId: string
  vehicleId: string
  receivedAt: string
  expectedDeliveryAt?: string
  deliveredAt?: string
  odometerIn?: number
  customerRequest: string
  vehicleCondition: string
  vehicleConditionImages: string[]
  diagnosis?: string
  serviceAdvisorId?: string
  priority: 'Low' | 'Normal' | 'High' | 'Urgent'
  status: RepairOrderStatus
  estimatedTotal: number
  discountAmount: number
  finalTotal: number
  items: RepairOrderItem[]
  statusHistory: Array<{
    fromStatus?: RepairOrderStatus
    toStatus: RepairOrderStatus
    changedBy: string
    changedAt: string
    note?: string
  }>
}

export type InvoicePaymentStatus =
  | 'Unpaid'
  | 'PartiallyPaid'
  | 'Paid'
  | 'Refunded'
  | 'Cancelled'

export interface Invoice extends BaseDocument {
  code: string
  repairOrderId: string
  customerId: string
  issueDate: string
  subtotal: number
  discountAmount: number
  discountType: 'Amount' | 'Percentage'
  discountValue: number
  itemDiscountAmount: number
  couponId?: string
  couponCode?: string
  couponDiscountAmount: number
  couponUsageReturned: boolean
  taxAmount: number
  totalAmount: number
  paidAmount: number
  remainingAmount: number
  loyaltyPointsRedeemed: number
  loyaltyDiscountAmount: number
  paymentStatus: InvoicePaymentStatus
  customerName: string
  customerPhone: string
  customerAddress?: string
  items: Array<{
    id: string
    itemType: 'Service' | 'Part'
    description: string
    quantity: number
    unitPrice: number
    discountAmount: number
    discountType: 'Amount' | 'Percentage'
    discountValue: number
    lineTotal: number
  }>
  payments: Array<{
    id: string
    amount: number
    method: string
    paidAt: string
    referenceCode?: string
  }>
}

export interface Coupon extends BaseDocument {
  code: string
  name: string
  audience: 'All' | 'MinimumOrder' | 'SpecificCustomers'
  minimumOrderAmount: number
  customerIds: string[]
  discountType: 'Amount' | 'Percentage'
  discountValue: number
  usageLimit?: number
  usedCount: number
  startAt?: string
  endAt?: string
  description?: string
  isActive: boolean
}

export interface LoyaltyAccount extends BaseDocument {
  customerId: string
  memberCode: string
  currentTierCode: string
  availablePoints: number
  lifetimeEarnedPoints: number
  lifetimeRedeemedPoints: number
  eligibleSpend: number
}

export interface LoyaltyTier extends BaseDocument {
  code: string
  name: string
  rank: number
  minEligibleSpend: number
  minEarnedPoints: number
  earnRate: number
  redemptionValue: number
  benefits: string[]
  isActive: boolean
}

export interface AppNotification extends BaseDocument {
  userId?: string
  role?: string
  type: string
  title: string
  message: string
  referenceType?: string
  referenceId?: string
  isRead: boolean
  readByUserIds: string[]
}

export interface UserAccount {
  id: string
  username: string
  fullName: string
  employeeId?: string
  roles: Array<'Admin' | 'Administrator' | 'Manager' | 'Employee'>
  isActive: boolean
  lastLoginAt?: string
}

export interface AuditLog extends BaseDocument {
  userId?: string
  username?: string
  userDisplayName?: string
  action: 'CREATE' | 'UPDATE' | 'DELETE' | 'CONFIRM' | string
  entityType: string
  entityId: string
  requestPath: string
  statusCode: number
  beforeData?: string
  afterData?: string
  ipAddress?: string
}

export interface DemoDataStatus {
  enabled: boolean
  confirmationPhrase?: string
  preservesCurrentAdmin: boolean
  scope: string[]
}

export interface DemoAccountResult {
  username: string
  fullName: string
  role: string
  password: string
}

export interface DemoDataResetResult {
  completedAt: string
  counts: Record<string, number>
  demoAccounts: DemoAccountResult[]
}
