using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using MotoCare.Api.Contracts;
using MotoCare.Api.Domain;
using MotoCare.Api.Hubs;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Services;

public sealed class InvoiceService(
    MongoDbContext context,
    SequenceService sequences,
    LoyaltyService loyalty,
    IHubContext<NotificationHub> hub)
{
    public async Task<Invoice> CreateAsync(
        CreateInvoiceRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var orders = context.Collection<RepairOrder>();
        var order = await orders.Find(x => x.Id == request.RepairOrderId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu sửa chữa.");
        if (order.Status is not RepairOrderStatus.Completed and not RepairOrderStatus.Delivered)
        {
            throw new InvalidOperationException("Chỉ lập hóa đơn khi phiếu sửa chữa đã hoàn tất.");
        }

        if (order.Items.Count == 0)
        {
            throw new InvalidOperationException("Phiếu sửa chữa chưa có hạng mục.");
        }

        if (order.Items.Any(x => x.ItemType == RepairItemType.Part && !x.InventoryIssued))
        {
            throw new InvalidOperationException("Phải xuất kho đầy đủ phụ tùng trước khi lập hóa đơn.");
        }

        if (await context.Collection<Invoice>()
            .Find(x => x.RepairOrderId == order.Id
                && x.PaymentStatus != InvoicePaymentStatus.Cancelled
                && !x.IsDeleted)
            .AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException("Phiếu sửa chữa đã có hóa đơn.");
        }

        var customer = await context.Collection<Customer>()
            .Find(x => x.Id == order.CustomerId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy khách hàng.");
        var items = order.Items.Select(x => new InvoiceItem
        {
            ItemType = x.ItemType,
            ReferenceId = x.PartId ?? x.ServiceId,
            Description = x.Description,
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice,
            DiscountAmount = x.DiscountAmount,
            TaxRate = request.TaxRate,
            LineTotal = x.LineTotal
        }).ToList();
        var subtotal = items.Sum(x => x.Quantity * x.UnitPrice);
        var discount = items.Sum(x => x.DiscountAmount) + request.DiscountAmount;
        var beforeTax = Math.Max(0, subtotal - discount);
        var taxAmount = decimal.Round(beforeTax * request.TaxRate / 100m, 0, MidpointRounding.AwayFromZero);
        var total = beforeTax + taxAmount;
        var invoice = new Invoice
        {
            Code = await sequences.NextAsync("invoice", "INV", cancellationToken),
            RepairOrderId = order.Id,
            CustomerId = customer.Id,
            Subtotal = subtotal,
            DiscountAmount = discount,
            TaxRate = request.TaxRate,
            TaxAmount = taxAmount,
            TotalAmount = total,
            RemainingAmount = total,
            CustomerName = customer.FullName,
            CustomerPhone = customer.Phone,
            CustomerAddress = customer.Address,
            CustomerTaxCode = customer.TaxCode,
            CreatedBy = userId,
            Notes = request.Notes?.Trim(),
            Items = items
        };
        await context.Collection<Invoice>().InsertOneAsync(invoice, cancellationToken: cancellationToken);
        await orders.UpdateOneAsync(
            x => x.Id == order.Id,
            Builders<RepairOrder>.Update
                .Set(x => x.FinalTotal, total)
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);
        return invoice;
    }

    public async Task<Invoice> AddPaymentAsync(
        string invoiceId,
        AddPaymentRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount == 0 && request.LoyaltyPoints == 0)
        {
            throw new InvalidOperationException("Cần nhập số tiền hoặc số điểm thanh toán.");
        }

        using var session = await context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        Invoice invoice;
        try
        {
            var invoices = context.Collection<Invoice>();
            invoice = await invoices.Find(session, x => x.Id == invoiceId && !x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
            if (invoice.PaymentStatus is InvoicePaymentStatus.Cancelled or InvoicePaymentStatus.Refunded)
            {
                throw new InvalidOperationException("Hóa đơn đã hủy/hoàn tiền.");
            }

            if (invoice.Payments.Any(x => x.IdempotencyKey == request.IdempotencyKey))
            {
                await session.AbortTransactionAsync(cancellationToken);
                return invoice;
            }

            var loyaltyDiscount = await loyalty.RedeemWithinTransactionAsync(
                session,
                invoice.CustomerId,
                invoice.Id,
                invoice.RemainingAmount,
                request.LoyaltyPoints,
                request.IdempotencyKey,
                userId,
                cancellationToken);
            var payableAfterPoints = Math.Max(0, invoice.RemainingAmount - loyaltyDiscount);
            if (request.Amount > payableAfterPoints)
            {
                throw new InvalidOperationException(
                    $"Số tiền thanh toán vượt quá công nợ còn lại {payableAfterPoints:N0} VND.");
            }

            invoice.Payments.Add(new Payment
            {
                IdempotencyKey = request.IdempotencyKey,
                Amount = request.Amount,
                Method = request.Method.Trim(),
                ReferenceCode = request.ReferenceCode?.Trim(),
                ReceivedBy = userId,
                Notes = request.Notes?.Trim()
            });

            invoice.PaidAmount += request.Amount;
            invoice.LoyaltyPointsRedeemed += request.LoyaltyPoints;
            invoice.LoyaltyDiscountAmount += loyaltyDiscount;
            invoice.RemainingAmount = Math.Max(
                0,
                invoice.TotalAmount - invoice.PaidAmount - invoice.LoyaltyDiscountAmount);
            invoice.PaymentStatus = invoice.RemainingAmount == 0
                ? InvoicePaymentStatus.Paid
                : InvoicePaymentStatus.PartiallyPaid;
            invoice.UpdatedAt = DateTime.UtcNow;

            if (invoice.PaymentStatus == InvoicePaymentStatus.Paid && !invoice.LoyaltyEarned)
            {
                await loyalty.EarnWithinTransactionAsync(
                    session,
                    invoice.CustomerId,
                    invoice.Id,
                    invoice.TotalAmount - invoice.LoyaltyDiscountAmount,
                    userId,
                    cancellationToken);
                invoice.LoyaltyEarned = true;
            }

            await invoices.ReplaceOneAsync(
                session,
                x => x.Id == invoice.Id,
                invoice,
                cancellationToken: cancellationToken);

            if (request.Amount > 0)
            {
                var cash = new CashTransaction
                {
                    Code = await sequences.NextAsync("cash", "RCT", cancellationToken),
                    Type = CashTransactionType.Income,
                    Category = "InvoicePayment",
                    Amount = request.Amount,
                    PaymentMethod = request.Method.Trim(),
                    ReferenceType = nameof(Invoice),
                    ReferenceId = invoice.Id,
                    Description = $"Thu tiền hóa đơn {invoice.Code}",
                    CreatedBy = userId
                };
                await context.Collection<CashTransaction>()
                    .InsertOneAsync(session, cash, cancellationToken: cancellationToken);
            }

            await session.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(cancellationToken);
            }

            throw;
        }

        var notification = new Notification
        {
            Role = SecurityRoles.Manager,
            Type = "InvoicePayment",
            Title = $"Thanh toán hóa đơn {invoice.Code}",
            Message = $"Đã thu {request.Amount:N0} VND; còn lại {invoice.RemainingAmount:N0} VND.",
            ReferenceType = nameof(Invoice),
            ReferenceId = invoice.Id
        };
        await context.Collection<Notification>().InsertOneAsync(notification, cancellationToken: cancellationToken);
        await hub.Clients.Group($"role:{SecurityRoles.Manager}")
            .SendAsync("notification", notification, cancellationToken);
        return invoice;
    }

    public async Task<Invoice> RefundAsync(
        string invoiceId,
        RefundInvoiceRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        using var session = await context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var invoices = context.Collection<Invoice>();
            var invoice = await invoices.Find(session, x => x.Id == invoiceId && !x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
            if (invoice.PaymentStatus is InvoicePaymentStatus.Refunded or InvoicePaymentStatus.Cancelled)
            {
                return invoice;
            }

            await loyalty.ReverseInvoiceWithinTransactionAsync(
                session,
                invoice.Id,
                userId,
                cancellationToken);
            if (invoice.PaidAmount > 0)
            {
                var cash = new CashTransaction
                {
                    Code = await sequences.NextAsync("cash", "PAY", cancellationToken),
                    Type = CashTransactionType.Expense,
                    Category = "InvoiceRefund",
                    Amount = invoice.PaidAmount,
                    PaymentMethod = "Refund",
                    ReferenceType = nameof(Invoice),
                    ReferenceId = invoice.Id,
                    Description = $"Hoàn tiền hóa đơn {invoice.Code}: {request.Reason.Trim()}",
                    CreatedBy = userId
                };
                await context.Collection<CashTransaction>()
                    .InsertOneAsync(session, cash, cancellationToken: cancellationToken);
            }

            invoice.PaymentStatus = InvoicePaymentStatus.Refunded;
            invoice.RemainingAmount = 0;
            invoice.UpdatedAt = DateTime.UtcNow;
            invoice.Notes = string.Join(
                Environment.NewLine,
                new[] { invoice.Notes, $"Hoàn tiền: {request.Reason.Trim()}" }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
            await invoices.ReplaceOneAsync(
                session,
                x => x.Id == invoice.Id,
                invoice,
                cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return invoice;
        }
        catch
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(cancellationToken);
            }

            throw;
        }
    }
}
