using System.Globalization;
using MongoDB.Driver;
using MotoCare.Api.Contracts;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Services;

public sealed class ReportsService(MongoDbContext context)
{
    public async Task<IReadOnlyList<RevenueReportRow>> RevenueAsync(
        DateTime from,
        DateTime to,
        string groupBy,
        CancellationToken cancellationToken = default)
    {
        var invoices = await ValidInvoices(from, to)
            .ToListAsync(cancellationToken);
        return invoices
            .GroupBy(x => PeriodKey(x.IssueDate, groupBy))
            .OrderBy(x => x.Key)
            .Select(group => new RevenueReportRow(
                group.Key,
                group.Sum(x => x.TotalAmount),
                group.Sum(x => x.PaidAmount),
                group.Sum(x => x.RemainingAmount),
                group.Sum(x => x.DiscountAmount + x.LoyaltyDiscountAmount),
                group.Count()))
            .ToList();
    }

    public async Task<IReadOnlyList<TopPartReportRow>> TopPartsAsync(
        DateTime from,
        DateTime to,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var invoices = await ValidInvoices(from, to)
            .ToListAsync(cancellationToken);
        return invoices
            .SelectMany(invoice => invoice.Items
                .Where(item => item.ItemType == RepairItemType.Part)
                .Select(item => new { invoice.Id, Item = item }))
            .GroupBy(x => new { x.Item.ReferenceId, x.Item.Description })
            .Select(group => new TopPartReportRow(
                group.Key.ReferenceId,
                group.Key.Description,
                group.Sum(x => x.Item.Quantity),
                group.Sum(x => x.Item.LineTotal),
                group.Select(x => x.Id).Distinct().Count()))
            .OrderByDescending(x => x.Quantity)
            .ThenByDescending(x => x.Revenue)
            .Take(Math.Clamp(limit, 1, 500))
            .ToList();
    }

    public async Task<IReadOnlyList<TopVehicleReportRow>> TopVehiclesAsync(
        DateTime from,
        DateTime to,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var orders = await context.Collection<RepairOrder>()
            .Find(x =>
                x.ReceivedAt >= from
                && x.ReceivedAt <= to
                && x.Status != RepairOrderStatus.Cancelled
                && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var grouped = orders.GroupBy(x => x.VehicleId)
            .Select(x => new
            {
                VehicleId = x.Key,
                Count = x.Count(),
                Value = x.Sum(v => v.FinalTotal),
                Last = x.Max(v => v.ReceivedAt)
            })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.Value)
            .Take(Math.Clamp(limit, 1, 500))
            .ToList();
        var ids = grouped.Select(x => x.VehicleId).ToArray();
        var vehicles = await context.Collection<Vehicle>()
            .Find(Builders<Vehicle>.Filter.In(x => x.Id, ids))
            .ToListAsync(cancellationToken);
        var plates = vehicles.ToDictionary(x => x.Id, x => x.LicensePlate);
        return grouped.Select(x => new TopVehicleReportRow(
            x.VehicleId,
            plates.GetValueOrDefault(x.VehicleId, "(không xác định)"),
            x.Count,
            x.Value,
            x.Last)).ToList();
    }

    public async Task<IReadOnlyList<LoyalCustomerReportRow>> LoyalCustomersAsync(
        string? tier,
        decimal? minimumSpend,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<LoyaltyAccount>>
        {
            Builders<LoyaltyAccount>.Filter.Eq(x => x.IsDeleted, false)
        };
        if (!string.IsNullOrWhiteSpace(tier))
        {
            filters.Add(Builders<LoyaltyAccount>.Filter.Eq(x => x.CurrentTierCode, tier.ToUpperInvariant()));
        }

        if (minimumSpend.HasValue)
        {
            filters.Add(Builders<LoyaltyAccount>.Filter.Gte(x => x.EligibleSpend, minimumSpend.Value));
        }

        var accounts = await context.Collection<LoyaltyAccount>()
            .Find(Builders<LoyaltyAccount>.Filter.And(filters))
            .SortByDescending(x => x.EligibleSpend)
            .Limit(Math.Clamp(limit, 1, 1_000))
            .ToListAsync(cancellationToken);
        var customerIds = accounts.Select(x => x.CustomerId).ToArray();
        var customers = await context.Collection<Customer>()
            .Find(Builders<Customer>.Filter.In(x => x.Id, customerIds))
            .ToListAsync(cancellationToken);
        var customerMap = customers.ToDictionary(x => x.Id);
        return accounts
            .Where(x => customerMap.ContainsKey(x.CustomerId))
            .Select(x =>
            {
                var customer = customerMap[x.CustomerId];
                return new LoyalCustomerReportRow(
                    customer.Id,
                    customer.Code,
                    customer.FullName,
                    customer.Phone,
                    x.CurrentTierCode,
                    x.EligibleSpend,
                    x.AvailablePoints,
                    x.LifetimeEarnedPoints,
                    x.LifetimeRedeemedPoints);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<LoyaltyTransactionReportRow>> LoyaltyTransactionsAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var transactions = await context.Collection<LoyaltyTransaction>()
            .Find(x =>
                x.EffectiveAt >= from
                && x.EffectiveAt <= to
                && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        return transactions
            .GroupBy(x => x.Type.ToString())
            .Select(x => new LoyaltyTransactionReportRow(
                x.Key,
                x.LongCount(),
                x.Sum(v => v.Points),
                x.Sum(v => v.MonetaryValue)))
            .OrderBy(x => x.Type)
            .ToList();
    }

    private IFindFluent<Invoice, Invoice> ValidInvoices(DateTime from, DateTime to) =>
        context.Collection<Invoice>().Find(x =>
            x.IssueDate >= from
            && x.IssueDate <= to
            && x.PaymentStatus != InvoicePaymentStatus.Cancelled
            && x.PaymentStatus != InvoicePaymentStatus.Refunded
            && !x.IsDeleted);

    private static string PeriodKey(DateTime value, string groupBy) =>
        groupBy.ToLowerInvariant() switch
        {
            "week" => $"{ISOWeek.GetYear(value):0000}-W{ISOWeek.GetWeekOfYear(value):00}",
            "quarter" => $"{value.Year:0000}-Q{((value.Month - 1) / 3) + 1}",
            _ => $"{value.Year:0000}-{value.Month:00}"
        };
}
