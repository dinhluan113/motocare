namespace MotoCare.Api.Contracts;

public sealed record RevenueReportRow(
    string Period,
    decimal Revenue,
    decimal Collected,
    decimal Outstanding,
    decimal Discount,
    int InvoiceCount);

public sealed record TopPartReportRow(
    string? PartId,
    string Description,
    decimal Quantity,
    decimal Revenue,
    int InvoiceCount);

public sealed record TopVehicleReportRow(
    string VehicleId,
    string LicensePlate,
    int RepairCount,
    decimal TotalValue,
    DateTime LastRepairAt);

public sealed record LoyalCustomerReportRow(
    string CustomerId,
    string CustomerCode,
    string FullName,
    string Phone,
    string TierCode,
    decimal EligibleSpend,
    long AvailablePoints,
    long LifetimeEarnedPoints,
    long LifetimeRedeemedPoints);

public sealed record LoyaltyTransactionReportRow(
    string Type,
    long TransactionCount,
    long Points,
    decimal MonetaryValue);
