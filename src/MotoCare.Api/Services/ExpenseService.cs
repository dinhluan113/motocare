using MongoDB.Bson;
using MongoDB.Driver;
using MotoCare.Api.Contracts;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Services;

public sealed class ExpenseService(
    MongoDbContext context,
    InventoryService inventory,
    SequenceService sequences)
{
    private const decimal MinimumProfitRate = 20m;

    public async Task<CashTransaction> CreateAsync(
        CashTransaction voucher,
        string userId,
        CancellationToken cancellationToken)
    {
        voucher.Id = ObjectId.GenerateNewId().ToString();
        voucher.CreatedAt = DateTime.UtcNow;
        voucher.UpdatedAt = voucher.CreatedAt;
        voucher.IsDeleted = false;
        voucher.CreatedBy = userId;
        voucher.Code = string.IsNullOrWhiteSpace(voucher.Code)
            ? await sequences.NextAsync(
                voucher.Type == CashTransactionType.Expense ? "cash-expense" : "cash-income",
                voucher.Type == CashTransactionType.Expense ? "PC" : "PT",
                cancellationToken)
            : voucher.Code.Trim().ToUpperInvariant();
        voucher.Description = voucher.Description.Trim();
        voucher.Category = voucher.Category.Trim();
        voucher.ConfirmedAt = null;
        voucher.ConfirmedBy = null;
        ValidatePaymentProof(voucher);

        if (voucher.Type != CashTransactionType.Expense || voucher.Purpose != "PartsPurchase")
        {
            if (voucher.Amount <= 0)
            {
                throw new InvalidOperationException("Số tiền thu/chi phải lớn hơn 0.");
            }
            await ApplyCashCategoryAsync(voucher, cancellationToken);
            voucher.Status = voucher.Type == CashTransactionType.Expense ? "New" : "Confirmed";
            await context.Collection<CashTransaction>()
                .InsertOneAsync(voucher, cancellationToken: cancellationToken);
            return voucher;
        }

        await PreparePurchaseVoucherAsync(voucher, cancellationToken);
        voucher.Status = "New";
        await context.Collection<CashTransaction>()
            .InsertOneAsync(voucher, cancellationToken: cancellationToken);
        return voucher;
    }

    public async Task ValidateDraftAsync(
        CashTransaction voucher,
        CancellationToken cancellationToken)
    {
        ValidatePaymentProof(voucher);
        if (voucher.Purpose != "PartsPurchase")
        {
            await ApplyCashCategoryAsync(voucher, cancellationToken);
        }
    }

    public async Task<CashTransaction> ConfirmAsync(
        string id,
        string userId,
        CancellationToken cancellationToken)
    {
        using var session = await context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var voucher = await context.Collection<CashTransaction>()
                .Find(session, x => x.Id == id && !x.IsDeleted && x.Status == "New")
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Phiếu chi không tồn tại hoặc đã được xác nhận.");

            if (voucher.Type != CashTransactionType.Expense)
            {
                throw new InvalidOperationException("Chỉ phiếu chi mới cần xác nhận.");
            }

            if (voucher.Purpose == "PartsPurchase")
            {
                await PreparePurchaseVoucherAsync(voucher, cancellationToken);
                foreach (var item in voucher.PurchaseItems)
                {
                    await inventory.MoveWithinTransactionAsync(
                        session,
                        new StockMovementRequest(
                            item.PartId,
                            InventoryTransactionType.Receipt,
                            item.Quantity,
                            item.UnitCost,
                            nameof(CashTransaction),
                            voucher.Id,
                            $"Nhập hàng theo phiếu chi {voucher.Code}",
                            voucher.SupplierId,
                            voucher.TransactionDate),
                        userId,
                        cancellationToken);
                }
            }

            voucher.Status = "Confirmed";
            voucher.ConfirmedAt = DateTime.UtcNow;
            voucher.ConfirmedBy = userId;
            voucher.UpdatedAt = voucher.ConfirmedAt.Value;
            var updateResult = await context.Collection<CashTransaction>().ReplaceOneAsync(
                session,
                x => x.Id == voucher.Id && x.Status == "New",
                voucher,
                cancellationToken: cancellationToken);
            if (updateResult.MatchedCount != 1)
            {
                throw new InvalidOperationException("Phiếu chi đã được xử lý bởi một yêu cầu khác.");
            }
            await session.CommitTransactionAsync(cancellationToken);
            return voucher;
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

    private async Task PreparePurchaseVoucherAsync(
        CashTransaction voucher,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(voucher.SupplierId))
        {
            throw new InvalidOperationException("Phiếu chi nhập hàng phải chọn nhà cung cấp.");
        }

        var supplier = await context.Collection<Supplier>()
            .Find(x => x.Id == voucher.SupplierId && !x.IsDeleted && x.IsActive)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Nhà cung cấp không tồn tại hoặc đã ngừng hoạt động.");
        if (voucher.PurchaseItems.Count == 0)
        {
            throw new InvalidOperationException("Phiếu chi nhập hàng phải có ít nhất một phụ tùng.");
        }

        var partIds = voucher.PurchaseItems.Select(x => x.PartId).Distinct().ToList();
        if (partIds.Count != voucher.PurchaseItems.Count)
        {
            throw new InvalidOperationException("Một phụ tùng chỉ được xuất hiện một lần trong phiếu chi.");
        }
        var parts = await context.Collection<Part>()
            .Find(x => partIds.Contains(x.Id) && !x.IsDeleted && x.IsActive)
            .ToListAsync(cancellationToken);
        if (parts.Count != partIds.Count)
        {
            throw new InvalidOperationException("Có phụ tùng không tồn tại hoặc đã ngừng kinh doanh.");
        }

        foreach (var item in voucher.PurchaseItems)
        {
            if (item.Quantity <= 0 || item.UnitCost <= 0)
            {
                throw new InvalidOperationException("Số lượng và giá nhập phải lớn hơn 0.");
            }

            var part = parts.First(x => x.Id == item.PartId);
            if (string.IsNullOrWhiteSpace(item.Id)) item.Id = ObjectId.GenerateNewId().ToString();
            item.PartCode = part.Code;
            item.PartName = part.Name;
            item.LineTotal = item.Quantity * item.UnitCost;
            item.SalePriceSnapshot = part.SalePrice;
            item.ProfitRate = Math.Round((part.SalePrice - item.UnitCost) / item.UnitCost * 100m, 2);
            item.IsLowProfit = item.ProfitRate < MinimumProfitRate;
        }

        voucher.Amount = voucher.PurchaseItems.Sum(x => x.LineTotal);
        voucher.Category = "Nhập phụ tùng";
        voucher.ReferenceType = nameof(Supplier);
        voucher.ReferenceId = supplier.Id;
    }

    private async Task ApplyCashCategoryAsync(
        CashTransaction voucher,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(voucher.CashCategoryId))
        {
            throw new InvalidOperationException("Phải chọn danh mục thu chi.");
        }
        var category = await context.Collection<CashCategory>()
            .Find(x => x.Id == voucher.CashCategoryId && !x.IsDeleted && x.IsActive)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Danh mục thu chi không tồn tại hoặc đã ngừng sử dụng.");
        var expectedScope = voucher.Type == CashTransactionType.Income
            ? CashCategoryScope.Income
            : CashCategoryScope.Expense;
        if (category.Scope != CashCategoryScope.Both && category.Scope != expectedScope)
        {
            throw new InvalidOperationException("Danh mục không phù hợp với loại giao dịch đã chọn.");
        }
        voucher.Category = category.Name;
    }

    private static void ValidatePaymentProof(CashTransaction voucher)
    {
        if (string.IsNullOrWhiteSpace(voucher.AttachmentUrl))
        {
            voucher.AttachmentUrl = null;
            return;
        }
        if (!voucher.AttachmentUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            && !voucher.AttachmentUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Tệp đính kèm phải là một hình ảnh hợp lệ.");
        }
        if (voucher.AttachmentUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
            && voucher.AttachmentUrl.Length > 6_000_000)
        {
            throw new InvalidOperationException("Ảnh đính kèm vượt quá dung lượng cho phép.");
        }
    }
}
