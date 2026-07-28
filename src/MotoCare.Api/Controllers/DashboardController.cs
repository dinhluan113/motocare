using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public sealed class DashboardController(MongoDbContext context) : ControllerBase
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
        var overdueTask = orders.CountDocumentsAsync(
            x => x.ExpectedDeliveryAt < DateTime.UtcNow
                && x.Status != RepairOrderStatus.Delivered
                && x.Status != RepairOrderStatus.Cancelled
                && !x.IsDeleted,
            cancellationToken: cancellationToken);
        var todayInvoicesTask = invoices.Find(x =>
                x.IssueDate >= today
                && x.IssueDate < tomorrow
                && x.PaymentStatus != InvoicePaymentStatus.Cancelled
                && x.PaymentStatus != InvoicePaymentStatus.Refunded
                && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        await Task.WhenAll(
            repairingTask,
            awaitingPartsTask,
            completedTask,
            overdueTask,
            todayInvoicesTask);

        var todayInvoices = await todayInvoicesTask;
        return Ok(ApiEnvelope.Ok(new
        {
            repairOrders = new
            {
                repairing = await repairingTask,
                awaitingParts = await awaitingPartsTask,
                waitingDelivery = await completedTask,
                overdue = await overdueTask
            },
            finance = new
            {
                revenueToday = todayInvoices.Sum(x => x.TotalAmount),
                collectedToday = todayInvoices.Sum(x => x.PaidAmount),
                outstandingToday = todayInvoices.Sum(x => x.RemainingAmount)
            }
        }));
    }
}
