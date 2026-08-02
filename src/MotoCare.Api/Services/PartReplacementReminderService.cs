using MongoDB.Driver;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Services;

public sealed record PartReplacementReminder(
    string CustomerId,
    string CustomerName,
    string? CustomerPhone,
    string VehicleId,
    string LicensePlate,
    int? CurrentOdometer,
    string PartId,
    string PartCode,
    string PartName,
    DateTime InstalledAt,
    int? InstalledOdometer,
    DateTime? DueAt,
    int? DueOdometer,
    int? RemainingDays,
    int? RemainingKm,
    bool IsOverdue,
    bool IsDueSoon,
    string LastRepairOrderId);

public sealed class PartReplacementReminderService(
    MongoDbContext context,
    IConfiguration configuration)
{
    public async Task<IReadOnlyList<PartReplacementReminder>> GetAsync(
        string? customerId = null,
        bool alertsOnly = false,
        CancellationToken cancellationToken = default)
    {
        var warningBeforeKm = Math.Max(0,
            configuration.GetValue("Maintenance:PartReplacementWarningBeforeKm", 500));
        var warningBeforeDays = Math.Max(0,
            configuration.GetValue("Maintenance:PartReplacementWarningBeforeDays", 30));

        var parts = (await context.Collection<Part>()
                .Find(x => !x.IsDeleted && x.IsActive)
                .ToListAsync(cancellationToken))
            .Where(x => x.ReplacementIntervalKm is > 0 || x.ReplacementIntervalMonths is > 0)
            .ToDictionary(x => x.Id);
        if (parts.Count == 0) return [];

        var orderFilter = Builders<RepairOrder>.Filter.And(
            Builders<RepairOrder>.Filter.Eq(x => x.IsDeleted, false),
            Builders<RepairOrder>.Filter.Ne(x => x.Status, RepairOrderStatus.Cancelled),
            Builders<RepairOrder>.Filter.Eq("items.itemType", RepairItemType.Part));
        if (!string.IsNullOrWhiteSpace(customerId))
        {
            orderFilter &= Builders<RepairOrder>.Filter.Eq(x => x.CustomerId, customerId);
        }

        var orders = await context.Collection<RepairOrder>()
            .Find(orderFilter)
            .ToListAsync(cancellationToken);
        var installations = orders
            .SelectMany(order => order.Items
                .Where(item => item.ItemType == RepairItemType.Part
                    && item.PartId is not null
                    && parts.ContainsKey(item.PartId)
                    && item.WorkStatus == WorkStatus.Completed
                    && item.InventoryIssued)
                .Select(item => new
                {
                    Order = order,
                    PartId = item.PartId!,
                    InstalledAt = item.CompletedAt ?? order.DeliveredAt ?? order.ReceivedAt
                }))
            .GroupBy(x => (x.Order.VehicleId, x.PartId))
            .Select(group => group.OrderByDescending(x => x.InstalledAt).First())
            .ToList();
        if (installations.Count == 0) return [];

        var vehicleIds = installations.Select(x => x.Order.VehicleId).Distinct().ToArray();
        var vehicles = await context.Collection<Vehicle>()
            .Find(x => vehicleIds.Contains(x.Id) && !x.IsDeleted && x.IsActive)
            .ToListAsync(cancellationToken);
        var vehicleMap = vehicles.ToDictionary(x => x.Id);
        var customerIds = vehicles.Select(x => x.CustomerId).Distinct().ToArray();
        var customers = await context.Collection<Customer>()
            .Find(x => customerIds.Contains(x.Id) && !x.IsDeleted && x.IsActive)
            .ToListAsync(cancellationToken);
        var customerMap = customers.ToDictionary(x => x.Id);
        var today = DateTime.UtcNow.Date;

        var reminders = installations
            .Where(x => vehicleMap.ContainsKey(x.Order.VehicleId))
            .Select(x =>
            {
                var vehicle = vehicleMap[x.Order.VehicleId];
                customerMap.TryGetValue(vehicle.CustomerId, out var customer);
                var part = parts[x.PartId];
                var dueAt = part.ReplacementIntervalMonths is > 0
                    ? x.InstalledAt.AddMonths(part.ReplacementIntervalMonths.Value)
                    : (DateTime?)null;
                var dueOdometer = part.ReplacementIntervalKm is > 0 && x.Order.OdometerIn.HasValue
                    ? x.Order.OdometerIn.Value + part.ReplacementIntervalKm.Value
                    : (int?)null;
                var remainingDays = dueAt.HasValue
                    ? (int)Math.Ceiling((dueAt.Value.Date - today).TotalDays)
                    : (int?)null;
                var remainingKm = dueOdometer.HasValue && vehicle.Odometer.HasValue
                    ? dueOdometer.Value - vehicle.Odometer.Value
                    : (int?)null;
                var isOverdue = remainingDays is <= 0 || remainingKm is <= 0;
                var isDueSoon = isOverdue
                    || remainingDays is not null && remainingDays <= warningBeforeDays
                    || remainingKm is not null && remainingKm <= warningBeforeKm;

                return new PartReplacementReminder(
                    vehicle.CustomerId,
                    customer?.FullName ?? "Khách hàng không còn tồn tại",
                    customer?.Phone,
                    vehicle.Id,
                    vehicle.LicensePlate,
                    vehicle.Odometer,
                    part.Id,
                    part.Code,
                    part.Name,
                    x.InstalledAt,
                    x.Order.OdometerIn,
                    dueAt,
                    dueOdometer,
                    remainingDays,
                    remainingKm,
                    isOverdue,
                    isDueSoon,
                    x.Order.Id);
            })
            .Where(x => x.DueAt.HasValue || x.DueOdometer.HasValue)
            .Where(x => !alertsOnly || x.IsDueSoon)
            .OrderByDescending(x => x.IsOverdue)
            .ThenBy(x => x.RemainingDays ?? int.MaxValue)
            .ThenBy(x => x.RemainingKm ?? int.MaxValue)
            .ToList();

        return reminders;
    }
}
