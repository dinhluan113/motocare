using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MotoCare.Api.Contracts;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;
using MotoCare.Api.Services;

namespace MotoCare.Api.Controllers;

[ApiController]
[Route("api/v1/invoices")]
[Authorize]
public sealed class InvoicesController(
    MongoDbContext context,
    InvoiceService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPage(
        [FromQuery] InvoicePaymentStatus? status,
        [FromQuery] string? customerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<Invoice>>
        {
            Builders<Invoice>.Filter.Eq(x => x.IsDeleted, false)
        };
        if (status.HasValue)
        {
            filters.Add(Builders<Invoice>.Filter.Eq(x => x.PaymentStatus, status.Value));
        }

        if (!string.IsNullOrWhiteSpace(customerId))
        {
            filters.Add(Builders<Invoice>.Filter.Eq(x => x.CustomerId, customerId));
        }

        if (from.HasValue)
        {
            filters.Add(Builders<Invoice>.Filter.Gte(x => x.IssueDate, from.Value.ToUniversalTime()));
        }

        if (to.HasValue)
        {
            filters.Add(Builders<Invoice>.Filter.Lte(x => x.IssueDate, to.Value.ToUniversalTime()));
        }

        var filter = Builders<Invoice>.Filter.And(filters);
        var collection = context.Collection<Invoice>();
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var total = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await collection.Find(filter)
            .SortByDescending(x => x.IssueDate)
            .Skip((safePage - 1) * safePageSize)
            .Limit(safePageSize)
            .ToListAsync(cancellationToken);
        return Ok(ApiEnvelope.Ok(new PagedResult<Invoice>(
            items,
            total,
            safePage,
            safePageSize)));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var invoice = await context.Collection<Invoice>()
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        return invoice is null
            ? NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy hóa đơn."))
            : Ok(ApiEnvelope.Ok(invoice));
    }

    [HttpPost("from-repair-order")]
    [Authorize(Roles = SecurityRoles.Finance)]
    public async Task<IActionResult> Create(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var invoice = await service.CreateAsync(request, UserId(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, ApiEnvelope.Ok(invoice));
    }

    [HttpPost("{id}/payments")]
    [Authorize(Roles = SecurityRoles.Finance)]
    public async Task<IActionResult> AddPayment(
        string id,
        AddPaymentRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(ApiEnvelope.Ok(await service.AddPaymentAsync(
            id,
            request,
            UserId(),
            cancellationToken)));
    }

    [HttpPost("{id}/refund")]
    [Authorize(Roles = SecurityRoles.Management)]
    public async Task<IActionResult> Refund(
        string id,
        RefundInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(ApiEnvelope.Ok(await service.RefundAsync(
            id,
            request,
            UserId(),
            cancellationToken)));
    }

    [HttpGet("{id}/print-data")]
    public async Task<IActionResult> PrintData(string id, CancellationToken cancellationToken)
    {
        var invoice = await context.Collection<Invoice>()
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
        var order = await context.Collection<RepairOrder>()
            .Find(x => x.Id == invoice.RepairOrderId)
            .FirstOrDefaultAsync(cancellationToken);
        var vehicle = order is null
            ? null
            : await context.Collection<Vehicle>()
                .Find(x => x.Id == order.VehicleId)
                .FirstOrDefaultAsync(cancellationToken);
        return Ok(ApiEnvelope.Ok(new { invoice, repairOrder = order, vehicle }));
    }

    private string UserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
}
