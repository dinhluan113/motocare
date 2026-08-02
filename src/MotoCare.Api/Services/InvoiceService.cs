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
    InventoryService inventory,
    LoyaltyService loyalty,
    IHubContext<NotificationHub> hub)
{
    public async Task<Invoice> CreateAsync(
        CreateInvoiceRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        using var session = await context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        var changedParts = new List<Part>();
        Invoice? createdInvoice = null;
        try
        {
            var orders = context.Collection<RepairOrder>();
            var invoices = context.Collection<Invoice>();
            var order = await orders.Find(session, x => x.Id == request.RepairOrderId && !x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy phiếu sửa chữa.");
            if (order.Status is not RepairOrderStatus.Repairing and not RepairOrderStatus.Completed)
                throw new InvalidOperationException("Phiếu phải ở trạng thái Đang sửa trước khi xuất hóa đơn.");
            var billableOrderItems = order.Items
                .Where(x => x.WorkStatus != WorkStatus.Cancelled)
                .ToList();
            if (billableOrderItems.Count == 0)
                throw new InvalidOperationException("Phiếu sửa chữa chưa có hạng mục.");
            if (billableOrderItems.Any(x => x.WorkStatus != WorkStatus.Completed))
                throw new InvalidOperationException("Phải hoàn thành tất cả hạng mục trước khi xuất hóa đơn.");
            if (await invoices.Find(session, x => x.RepairOrderId == order.Id
                    && x.PaymentStatus != InvoicePaymentStatus.Cancelled && !x.IsDeleted)
                .AnyAsync(cancellationToken))
                throw new InvalidOperationException("Phiếu sửa chữa đã có hóa đơn.");

            var customer = await context.Collection<Customer>()
                .Find(session, x => x.Id == order.CustomerId && !x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy khách hàng.");

            foreach (var item in billableOrderItems.Where(x =>
                         x.ItemType == RepairItemType.Part
                         && !x.InventoryIssued
                         && !string.IsNullOrWhiteSpace(x.PartId)))
            {
                var movement = await inventory.MoveWithinTransactionAsync(
                    session,
                    new StockMovementRequest(
                        item.PartId!,
                        InventoryTransactionType.RepairIssue,
                        item.Quantity,
                        0,
                        nameof(RepairOrder),
                        order.Id,
                        $"Tự động xuất khi lập hóa đơn cho phiếu {order.Code}"),
                    userId,
                    cancellationToken);
                changedParts.Add(movement.Part);
                item.InventoryIssued = true;
                item.IssuedWarehouseLocationId = movement.Transaction.WarehouseLocationId;
                item.IssuedWarehouseLocationCode = movement.Transaction.WarehouseLocationCode;
            }

            var items = billableOrderItems.Select(x => new InvoiceItem
            {
                ItemType = x.ItemType,
                ReferenceId = x.PartId ?? x.ServiceId,
                Description = x.Description,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                DiscountType = x.DiscountType,
                DiscountValue = x.DiscountValue > 0 ? x.DiscountValue : x.DiscountAmount,
                DiscountAmount = x.DiscountAmount,
                TaxRate = request.TaxRate,
                LineTotal = x.LineTotal
            }).ToList();
            var subtotal = items.Sum(x => x.Quantity * x.UnitPrice);
            var itemDiscount = items.Sum(x => x.DiscountAmount);
            var afterItems = Math.Max(0, subtotal - itemDiscount);
            var invoiceDiscountValue = request.DiscountValue > 0
                ? request.DiscountValue
                : request.DiscountAmount;
            var invoiceDiscount = CalculateDiscount(
                afterItems,
                request.DiscountType,
                invoiceDiscountValue);
            var afterInvoiceDiscount = Math.Max(0, afterItems - invoiceDiscount);

            Coupon? coupon = null;
            var couponDiscount = 0m;
            if (!string.IsNullOrWhiteSpace(request.CouponCode))
            {
                var normalizedCode = request.CouponCode.Trim().ToUpperInvariant();
                coupon = await context.Collection<Coupon>()
                    .Find(session, x => x.Code == normalizedCode && !x.IsDeleted)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException("Coupon không tồn tại.");
                ValidateCoupon(coupon, customer.Id, afterInvoiceDiscount);
                var reserveFilter = Builders<Coupon>.Filter.Eq(x => x.Id, coupon.Id);
                if (coupon.UsageLimit.HasValue)
                {
                    reserveFilter &= Builders<Coupon>.Filter.Lt(
                        x => x.UsedCount,
                        coupon.UsageLimit.Value);
                }
                var reserved = await context.Collection<Coupon>().UpdateOneAsync(
                    session,
                    reserveFilter,
                    Builders<Coupon>.Update
                        .Inc(x => x.UsedCount, 1)
                        .Set(x => x.UpdatedAt, DateTime.UtcNow),
                    cancellationToken: cancellationToken);
                if (reserved.ModifiedCount != 1)
                    throw new InvalidOperationException("Coupon đã hết lượt sử dụng.");
                couponDiscount = CalculateDiscount(
                    afterInvoiceDiscount,
                    coupon.DiscountType,
                    coupon.DiscountValue);
            }

            var beforeTax = Math.Max(0, afterInvoiceDiscount - couponDiscount);
            var taxAmount = decimal.Round(beforeTax * request.TaxRate / 100m, 0, MidpointRounding.AwayFromZero);
            var total = beforeTax + taxAmount;
            var invoice = new Invoice
            {
                Code = await sequences.NextAsync("invoice", "INV", cancellationToken),
                RepairOrderId = order.Id,
                CustomerId = customer.Id,
                Subtotal = subtotal,
                DiscountType = request.DiscountType,
                DiscountValue = invoiceDiscountValue,
                ItemDiscountAmount = itemDiscount,
                CouponId = coupon?.Id,
                CouponCode = coupon?.Code,
                CouponDiscountAmount = couponDiscount,
                DiscountAmount = itemDiscount + invoiceDiscount + couponDiscount,
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
            await invoices.InsertOneAsync(session, invoice, cancellationToken: cancellationToken);
            var previousStatus = order.Status;
            order.Status = RepairOrderStatus.Completed;
            order.FinalTotal = total;
            order.UpdatedAt = DateTime.UtcNow;
            if (previousStatus != RepairOrderStatus.Completed)
            {
                order.StatusHistory.Add(new RepairStatusHistory
                {
                    FromStatus = previousStatus,
                    ToStatus = RepairOrderStatus.Completed,
                    ChangedBy = userId,
                    Note = $"Tự động hoàn tất khi xuất hóa đơn {invoice.Code}."
                });
            }
            await orders.ReplaceOneAsync(
                session,
                x => x.Id == order.Id,
                order,
                cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            createdInvoice = invoice;
        }
        catch
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(cancellationToken);
            throw;
        }

        foreach (var part in changedParts)
        {
            await inventory.NotifyLowStockAsync(part, cancellationToken);
        }
        return createdInvoice!;
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
            if (invoice.PaidAmount <= 0)
            {
                throw new InvalidOperationException("Hóa đơn chưa thanh toán; hãy dùng chức năng hủy hóa đơn.");
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

    public async Task<Invoice> CancelAsync(
        string invoiceId,
        CancelInvoiceRequest request,
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
            if (invoice.PaymentStatus == InvoicePaymentStatus.Cancelled) return invoice;
            if (invoice.PaymentStatus != InvoicePaymentStatus.Unpaid || invoice.PaidAmount > 0)
                throw new InvalidOperationException("Chỉ có thể hủy hóa đơn chưa thanh toán.");

            if (!string.IsNullOrWhiteSpace(invoice.CouponId) && !invoice.CouponUsageReturned)
            {
                await context.Collection<Coupon>().UpdateOneAsync(
                    session,
                    x => x.Id == invoice.CouponId && x.UsedCount > 0,
                    Builders<Coupon>.Update
                        .Inc(x => x.UsedCount, -1)
                        .Set(x => x.UpdatedAt, DateTime.UtcNow),
                    cancellationToken: cancellationToken);
                invoice.CouponUsageReturned = true;
            }

            invoice.PaymentStatus = InvoicePaymentStatus.Cancelled;
            invoice.RemainingAmount = 0;
            invoice.UpdatedAt = DateTime.UtcNow;
            invoice.Notes = string.Join(
                Environment.NewLine,
                new[] { invoice.Notes, $"Hủy bởi {userId}: {request.Reason.Trim()}" }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
            await invoices.ReplaceOneAsync(
                session,
                x => x.Id == invoice.Id,
                invoice,
                cancellationToken: cancellationToken);

            var orders = context.Collection<RepairOrder>();
            var order = await orders.Find(session, x => x.Id == invoice.RepairOrderId && !x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            if (order is not null && order.Status == RepairOrderStatus.Completed)
            {
                order.Status = RepairOrderStatus.Repairing;
                order.FinalTotal = order.EstimatedTotal;
                order.UpdatedAt = DateTime.UtcNow;
                order.StatusHistory.Add(new RepairStatusHistory
                {
                    FromStatus = RepairOrderStatus.Completed,
                    ToStatus = RepairOrderStatus.Repairing,
                    ChangedBy = userId,
                    Note = $"Mở lại do hủy hóa đơn {invoice.Code}."
                });
                await orders.ReplaceOneAsync(
                    session,
                    x => x.Id == order.Id,
                    order,
                    cancellationToken: cancellationToken);
            }
            await session.CommitTransactionAsync(cancellationToken);
            return invoice;
        }
        catch
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(cancellationToken);
            throw;
        }
    }

    private static void ValidateCoupon(Coupon coupon, string customerId, decimal orderValue)
    {
        var now = DateTime.UtcNow;
        if (!coupon.IsActive) throw new InvalidOperationException("Coupon đang tạm khóa.");
        if (coupon.StartAt.HasValue && now < coupon.StartAt.Value)
            throw new InvalidOperationException("Coupon chưa đến thời gian áp dụng.");
        if (coupon.EndAt.HasValue && now > coupon.EndAt.Value)
            throw new InvalidOperationException("Coupon đã hết hạn.");
        if (coupon.UsageLimit.HasValue && coupon.UsedCount >= coupon.UsageLimit.Value)
            throw new InvalidOperationException("Coupon đã hết lượt sử dụng.");
        if (coupon.Audience == CouponAudience.MinimumOrder
            && orderValue < coupon.MinimumOrderAmount)
            throw new InvalidOperationException($"Coupon yêu cầu đơn hàng tối thiểu {coupon.MinimumOrderAmount:N0} VND.");
        if (coupon.Audience == CouponAudience.SpecificCustomers
            && !coupon.CustomerIds.Contains(customerId))
            throw new InvalidOperationException("Coupon không áp dụng cho khách hàng này.");
    }

    private static decimal CalculateDiscount(decimal amount, DiscountType type, decimal value)
    {
        if (value < 0 || type == DiscountType.Percentage && value > 100)
            throw new InvalidOperationException("Giá trị giảm giá không hợp lệ.");
        var discount = type == DiscountType.Percentage
            ? decimal.Round(amount * value / 100m, 0, MidpointRounding.AwayFromZero)
            : value;
        return Math.Min(amount, discount);
    }
}
