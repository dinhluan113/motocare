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

export interface Part extends BaseDocument {
  code: string
  barcode?: string
  name: string
  partBrandId: string
  unit: string
  importPrice: number
  stockPrice: number
  salePrice: number
  quantityOnHand: number
  minQuantity: number
  location?: string
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
  lineTotal: number
  assignedEmployeeId?: string
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
  customerRequest: string
  vehicleCondition: string
  diagnosis?: string
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
