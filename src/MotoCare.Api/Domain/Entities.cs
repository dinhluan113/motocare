using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoCare.Api.Domain;

public abstract class BaseDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public int SchemaVersion { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
}

public sealed class AppUser : BaseDocument
{
    public string Username { get; set; } = string.Empty;
    public string NormalizedUsername { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeId { get; set; }
    public List<string> Roles { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
}

public sealed class Customer : BaseDocument
{
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string NormalizedPhone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public AddressDetails? AddressDetails { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? TaxCode { get; set; }
    public string? Notes { get; set; }
    public string? LoyaltyAccountId { get; set; }
    public string? LoyaltyTierCode { get; set; }
    public long LoyaltyPointBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum EmployeeStatus
{
    Active,
    OnLeave,
    Inactive
}

public sealed class Employee : BaseDocument
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public AddressDetails? AddressDetails { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime HireDate { get; set; } = DateTime.UtcNow;
    public string Position { get; set; } = string.Empty;
    public string? SkillLevel { get; set; }
    public List<string> Specialties { get; set; } = [];

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal BaseSalary { get; set; }

    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public string? UserId { get; set; }
    public string? Notes { get; set; }
}

public sealed class AddressDetails
{
    public string? AddressLine { get; set; }
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public string? RegionCode { get; set; }
    public string? RegionName { get; set; }
    public string? AreaCode { get; set; }
    public string? AreaName { get; set; }
}

public sealed class VehicleBrand : BaseDocument
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class VehicleModel : BaseDocument
{
    public string BrandId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? VehicleType { get; set; }
    public int? EngineCapacityCc { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Vehicle : BaseDocument
{
    public string CustomerId { get; set; } = string.Empty;
    public string VehicleModelId { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string NormalizedLicensePlate { get; set; } = string.Empty;
    public string? FrameNumber { get; set; }
    public string? EngineNumber { get; set; }
    public int? ManufactureYear { get; set; }
    public string? Color { get; set; }
    public int? Odometer { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PartBrand : BaseDocument
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? ContactInfo { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Supplier : BaseDocument
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public AddressDetails? AddressDetails { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PartCategory : BaseDocument
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<PartSpecificationDefinition> SpecificationDefinitions { get; set; } = [];
    public bool IsActive { get; set; } = true;
}

public sealed class ServiceCategory : BaseDocument
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal DefaultPrice { get; set; }

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PartSpecificationDefinition
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    [BsonRepresentation(BsonType.String)]
    public PartSpecificationDataType DataType { get; set; } = PartSpecificationDataType.Text;
    public List<string> Options { get; set; } = [];
    public string? Unit { get; set; }
    public bool IsRequired { get; set; }
}

public enum PartSpecificationDataType
{
    Text,
    Number,
    Boolean,
    Selection
}

public sealed class PartSpecificationValue
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string Value { get; set; } = string.Empty;
}

public sealed class SupplierPartStock : BaseDocument
{
    public string SupplierId { get; set; } = string.Empty;
    public string PartId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal QuantityOnHand { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal LastUnitCost { get; set; }

    public DateTime? LastReceiptAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class Part : BaseDocument
{
    public string Code { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PartBrandId { get; set; } = string.Empty;
    public string PartCategoryId { get; set; } = string.Empty;
    public List<PartSpecificationValue> Specifications { get; set; } = [];
    public List<string> SupplierIds { get; set; } = [];
    public string Unit { get; set; } = "Cái";

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal ImportPrice { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal StockPrice { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal SalePrice { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal QuantityOnHand { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal MinQuantity { get; set; }

    public int? ReplacementIntervalKm { get; set; }
    public int? ReplacementIntervalMonths { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum InventoryTransactionType
{
    Receipt,
    RepairIssue,
    RepairReturn,
    AdjustmentIncrease,
    AdjustmentDecrease
}

public sealed class InventoryTransaction : BaseDocument
{
    public string Code { get; set; } = string.Empty;
    public string PartId { get; set; } = string.Empty;
    public InventoryTransactionType Type { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Quantity { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal UnitCost { get; set; }

    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public string? SupplierId { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string PerformedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public enum RepairPriority
{
    Low,
    Normal,
    High,
    Urgent
}

public enum RepairOrderStatus
{
    Received,
    Inspecting,
    AwaitingApproval,
    Repairing,
    AwaitingParts,
    Completed,
    Delivered,
    Cancelled
}

public enum RepairItemType
{
    Service,
    Part
}

public enum DiscountType
{
    Amount,
    Percentage
}

public enum WorkStatus
{
    Pending,
    InProgress,
    Completed,
    Cancelled
}

public sealed class RepairOrderItem
{
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public RepairItemType ItemType { get; set; }
    public string? ServiceId { get; set; }
    public string? PartId { get; set; }
    public string Description { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Quantity { get; set; } = 1;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal UnitPrice { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal DiscountAmount { get; set; }

    public DiscountType DiscountType { get; set; } = DiscountType.Amount;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal DiscountValue { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal LineTotal { get; set; }

    public string? AssignedEmployeeId { get; set; }
    public WorkStatus WorkStatus { get; set; } = WorkStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? TechnicianNotes { get; set; }
    public bool InventoryIssued { get; set; }
}

public sealed class RepairStatusHistory
{
    public RepairOrderStatus? FromStatus { get; set; }
    public RepairOrderStatus ToStatus { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}

public sealed class RepairOrder : BaseDocument
{
    public string Code { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string VehicleId { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpectedDeliveryAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public int? OdometerIn { get; set; }
    public string? FuelLevel { get; set; }
    public string VehicleCondition { get; set; } = string.Empty;
    public List<string> VehicleConditionImages { get; set; } = [];
    public string CustomerRequest { get; set; } = string.Empty;
    public string? Diagnosis { get; set; }
    public string? InternalNotes { get; set; }
    public string? WarrantyNotes { get; set; }
    public RepairPriority Priority { get; set; } = RepairPriority.Normal;
    public RepairOrderStatus Status { get; set; } = RepairOrderStatus.Received;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal EstimatedTotal { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal DiscountAmount { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal FinalTotal { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public string? ServiceAdvisorId { get; set; }
    public List<RepairOrderItem> Items { get; set; } = [];
    public List<RepairStatusHistory> StatusHistory { get; set; } = [];
}

public enum InvoicePaymentStatus
{
    Unpaid,
    PartiallyPaid,
    Paid,
    Refunded,
    Cancelled
}

public sealed class InvoiceItem
{
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public RepairItemType ItemType { get; set; }
    public string? ReferenceId { get; set; }
    public string Description { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Quantity { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal UnitPrice { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal DiscountAmount { get; set; }

    public DiscountType DiscountType { get; set; } = DiscountType.Amount;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal DiscountValue { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TaxRate { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal LineTotal { get; set; }
}

public sealed class Payment
{
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string IdempotencyKey { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Amount { get; set; }

    public string Method { get; set; } = "Cash";
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public string? ReferenceCode { get; set; }
    public string ReceivedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class Invoice : BaseDocument
{
    public string Code { get; set; } = string.Empty;
    public string RepairOrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Subtotal { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal DiscountAmount { get; set; }

    public DiscountType DiscountType { get; set; } = DiscountType.Amount;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal DiscountValue { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal ItemDiscountAmount { get; set; }

    public string? CouponId { get; set; }
    public string? CouponCode { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal CouponDiscountAmount { get; set; }

    public bool CouponUsageReturned { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TaxRate { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TaxAmount { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TotalAmount { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal PaidAmount { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal RemainingAmount { get; set; }

    public long LoyaltyPointsRedeemed { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal LoyaltyDiscountAmount { get; set; }

    public InvoicePaymentStatus PaymentStatus { get; set; } = InvoicePaymentStatus.Unpaid;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerAddress { get; set; }
    public string? CustomerTaxCode { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<InvoiceItem> Items { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    public bool LoyaltyEarned { get; set; }
}

public enum CouponAudience
{
    All,
    MinimumOrder,
    SpecificCustomers
}

public sealed class Coupon : BaseDocument
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CouponAudience Audience { get; set; } = CouponAudience.All;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal MinimumOrderAmount { get; set; }

    public List<string> CustomerIds { get; set; } = [];
    public DiscountType DiscountType { get; set; } = DiscountType.Amount;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal DiscountValue { get; set; }

    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum CashTransactionType
{
    Income,
    Expense
}

public enum CashCategoryScope
{
    Income,
    Expense,
    Both
}

public sealed class CashCategory : BaseDocument
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CashCategoryScope Scope { get; set; } = CashCategoryScope.Both;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CashTransaction : BaseDocument
{
    public string Code { get; set; } = string.Empty;
    public CashTransactionType Type { get; set; }
    public string? CashCategoryId { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = "Cash";
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string Status { get; set; } = "Approved";
    public string Purpose { get; set; } = "Other";
    public string? SupplierId { get; set; }
    public List<PurchaseExpenseItem> PurchaseItems { get; set; } = [];
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedBy { get; set; }
}

public sealed class PurchaseExpenseItem
{
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string PartId { get; set; } = string.Empty;
    public string PartCode { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Quantity { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal UnitCost { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal LineTotal { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal SalePriceSnapshot { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal ProfitRate { get; set; }

    public bool IsLowProfit { get; set; }
}

public sealed class LoyaltyTier : BaseDocument
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Rank { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal MinEligibleSpend { get; set; }

    public long MinEarnedPoints { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal EarnRate { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal RedemptionValue { get; set; }

    public List<string> Benefits { get; set; } = [];
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class LoyaltyRule : BaseDocument
{
    public string Name { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal SpendPerPoint { get; set; } = 10_000;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal RedemptionValue { get; set; } = 1_000;

    public long MinimumRedemptionPoints { get; set; } = 10;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal MaximumRedemptionRate { get; set; } = 0.5m;

    public int? PointExpiryDays { get; set; } = 365;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum LoyaltyTransactionType
{
    Earn,
    Redeem,
    Expire,
    Adjust,
    Reverse
}

public sealed class LoyaltyAccount : BaseDocument
{
    public string CustomerId { get; set; } = string.Empty;
    public string MemberCode { get; set; } = string.Empty;
    public string CurrentTierCode { get; set; } = "MEMBER";
    public long AvailablePoints { get; set; }
    public long PendingPoints { get; set; }
    public long LifetimeEarnedPoints { get; set; }
    public long LifetimeRedeemedPoints { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal EligibleSpend { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TierUpdatedAt { get; set; }
    public string Status { get; set; } = "Active";
}

public sealed class LoyaltyTransaction : BaseDocument
{
    public string TransactionCode { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string LoyaltyAccountId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? InvoiceId { get; set; }
    public string? PaymentId { get; set; }
    public LoyaltyTransactionType Type { get; set; }
    public long Points { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal MonetaryValue { get; set; }

    public long BalanceBefore { get; set; }
    public long BalanceAfter { get; set; }
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
    public DateTime EffectiveAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? ReferenceTransactionId { get; set; }
}

public sealed class Notification : BaseDocument
{
    public string? UserId { get; set; }
    public string? Role { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public List<string> ReadByUserIds { get; set; } = [];
}

public sealed class AuditLog : BaseDocument
{
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? UserDisplayName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? BeforeData { get; set; }
    public string? AfterData { get; set; }
    public string? IpAddress { get; set; }
}

public sealed class Sequence : BaseDocument
{
    public string Name { get; set; } = string.Empty;
    public long Value { get; set; }
}
