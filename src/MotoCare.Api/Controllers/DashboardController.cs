using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public sealed class DashboardController(
    MongoDbContext context,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var orders = context.Collection<RepairOrder>();
        var invoices = context.Collection<Invoice>();
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var repairingTask = orders.CountDocumentsAsync(
            x => x.Status == RepairOrderStatus.Repairing && !x.IsDeleted,
            cancellationToken: cancellationToken);
        var awaitingPartsTask = orders.CountDocumentsAsync(
            x => x.Status == RepairOrderStatus.AwaitingParts && !x.IsDeleted,
            cancellationToken: cancellationToken);
        var completedTask = orders.CountDocumentsAsync(
            x => x.Status == RepairOrderStatus.Completed && !x.IsDeleted,
            cancellationToken: cancellationToken);
        var overdueOrdersTask = OverdueOrders(cancellationToken);
        var todayInvoicesTask = invoices.Find(x =>
                x.IssueDate >= today
                && x.IssueDate < tomorrow
                && x.PaymentStatus != InvoicePaymentStatus.Cancelled
                && x.PaymentStatus != InvoicePaymentStatus.Refunded
                && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var oilChangeAlertsTask = OilChangeAlerts(cancellationToken);
        await Task.WhenAll(
            repairingTask,
            awaitingPartsTask,
            completedTask,
            overdueOrdersTask,
            todayInvoicesTask,
            oilChangeAlertsTask);

        var todayInvoices = await todayInvoicesTask;
        var overdueOrders = await overdueOrdersTask;
        return Ok(ApiEnvelope.Ok(new
        {
            repairOrders = new
            {
                repairing = await repairingTask,
                awaitingParts = await awaitingPartsTask,
                waitingDelivery = await completedTask,
                overdue = overdueOrders.Count,
                overdueItems = overdueOrders
            },
            finance = new
            {
                revenueToday = todayInvoices.Sum(x => x.TotalAmount),
                collectedToday = todayInvoices.Sum(x => x.PaidAmount),
                outstandingToday = todayInvoices.Sum(x => x.RemainingAmount)
            },
            maintenance = new
            {
                oilChange = new
                {
                    intervalKm = configuration.GetValue("Maintenance:OilChangeIntervalKm", 2_000),
                    warningBeforeKm = configuration.GetValue("Maintenance:OilChangeWarningBeforeKm", 300),
                    vehicles = await oilChangeAlertsTask
                }
            }
        }));
    }

    private async Task<IReadOnlyList<object>> OverdueOrders(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var overdueOrders = await context.Collection<RepairOrder>()
            .Find(x => x.ExpectedDeliveryAt != null
                && x.ExpectedDeliveryAt < now
                && x.Status != RepairOrderStatus.Delivered
                && x.Status != RepairOrderStatus.Cancelled
                && !x.IsDeleted)
            .SortBy(x => x.ExpectedDeliveryAt)
            .ToListAsync(cancellationToken);
        if (overdueOrders.Count == 0) return [];

        var customerIds = overdueOrders.Select(x => x.CustomerId).Distinct().ToArray();
        var vehicleIds = overdueOrders.Select(x => x.VehicleId).Distinct().ToArray();
        var customersTask = context.Collection<Customer>()
            .Find(x => customerIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var vehiclesTask = context.Collection<Vehicle>()
            .Find(x => vehicleIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        await Task.WhenAll(customersTask, vehiclesTask);

        var customerMap = (await customersTask).ToDictionary(x => x.Id);
        var vehicleMap = (await vehiclesTask).ToDictionary(x => x.Id);
        return overdueOrders.Select(order =>
        {
            customerMap.TryGetValue(order.CustomerId, out var customer);
            vehicleMap.TryGetValue(order.VehicleId, out var vehicle);
            var daysOverdue = Math.Max(
                1,
                (int)Math.Ceiling((now - order.ExpectedDeliveryAt!.Value).TotalDays));
            return (object)new
            {
                order.Id,
                order.Code,
                order.CustomerId,
                customerName = customer?.FullName ?? "Khách hàng không còn tồn tại",
                order.VehicleId,
                licensePlate = vehicle?.LicensePlate ?? "Không xác định",
                order.ExpectedDeliveryAt,
                order.Status,
                order.Priority,
                daysOverdue
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<object>> OilChangeAlerts(CancellationToken cancellationToken)
    {
        var intervalKm = Math.Max(1, configuration.GetValue("Maintenance:OilChangeIntervalKm", 2_000));
        var warningBeforeKm = Math.Clamp(
            configuration.GetValue("Maintenance:OilChangeWarningBeforeKm", 300),
            0,
            intervalKm);
        var services = await context.Collection<ServiceCategory>()
            .Find(Builders<ServiceCategory>.Filter.Empty)
            .ToListAsync(cancellationToken);
        var oilServiceIds = services
            .Where(x => IsOilChangeText(x.Name) || IsOilChangeText(x.Code))
            .Select(x => x.Id)
            .ToHashSet();

        var orderFilter = Builders<RepairOrder>.Filter.And(
            Builders<RepairOrder>.Filter.Eq(x => x.IsDeleted, false),
            Builders<RepairOrder>.Filter.Ne(x => x.Status, RepairOrderStatus.Cancelled),
            Builders<RepairOrder>.Filter.Eq("items.itemType", RepairItemType.Service));
        var serviceOrders = await context.Collection<RepairOrder>()
            .Find(orderFilter)
            .ToListAsync(cancellationToken);
        var latestByVehicle = serviceOrders
            .Where(x => x.OdometerIn.HasValue && x.Items.Any(item =>
                item.ItemType == RepairItemType.Service
                && item.WorkStatus == WorkStatus.Completed
                && (item.ServiceId is not null && oilServiceIds.Contains(item.ServiceId)
                    || IsOilChangeText(item.Description))))
            .GroupBy(x => x.VehicleId)
            .Select(x => x.OrderByDescending(order => order.ReceivedAt).First())
            .ToList();
        if (latestByVehicle.Count == 0) return [];

        var vehicleIds = latestByVehicle.Select(x => x.VehicleId).ToArray();
        var vehicles = await context.Collection<Vehicle>()
            .Find(x => vehicleIds.Contains(x.Id) && !x.IsDeleted && x.IsActive)
            .ToListAsync(cancellationToken);
        var customerIds = vehicles.Select(x => x.CustomerId).Distinct().ToArray();
        var customers = await context.Collection<Customer>()
            .Find(x => customerIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var customerMap = customers.ToDictionary(x => x.Id);
        var lastMap = latestByVehicle.ToDictionary(x => x.VehicleId);

        return vehicles
            .Where(vehicle => vehicle.Odometer.HasValue && lastMap.ContainsKey(vehicle.Id))
            .Select(vehicle =>
            {
                var last = lastMap[vehicle.Id];
                var lastOdometer = last.OdometerIn!.Value;
                var dueOdometer = lastOdometer + intervalKm;
                var remainingKm = dueOdometer - vehicle.Odometer!.Value;
                customerMap.TryGetValue(vehicle.CustomerId, out var customer);
                return new
                {
                    vehicleId = vehicle.Id,
                    vehicle.LicensePlate,
                    vehicle.CustomerId,
                    customerName = customer is null
                        ? "Khách hàng không còn tồn tại"
                        : $"{customer.FullName}{(customer.IsDeleted ? " (đã xóa)" : string.Empty)}",
                    customerPhone = customer?.Phone,
                    currentOdometer = vehicle.Odometer.Value,
                    lastOdometer,
                    dueOdometer,
                    remainingKm,
                    overdue = remainingKm <= 0,
                    lastChangedAt = last.ReceivedAt,
                    lastRepairOrderId = last.Id
                };
            })
            .Where(x => x.remainingKm <= warningBeforeKm)
            .OrderBy(x => x.remainingKm)
            .Take(20)
            .Cast<object>()
            .ToList();
    }

    private static bool IsOilChangeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        }
        var text = builder.ToString().Normalize(NormalizationForm.FormC);
        return text.Contains("thay nhot")
            || text.Contains("thay dau")
            || text.Contains("dau may")
            || text.Contains("engine oil")
            || text.Contains("oil change");
    }
}
