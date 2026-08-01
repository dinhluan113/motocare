using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using MotoCare.Api.Contracts;
using MotoCare.Api.Domain;
using MotoCare.Api.Hubs;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Services;

public sealed class InventoryService(
    MongoDbContext context,
    SequenceService sequences,
    IHubContext<NotificationHub> hub)
{
    public async Task<InventoryTransaction> MoveAsync(
        StockMovementRequest request,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new InvalidOperationException("Số lượng giao dịch kho phải lớn hơn 0.");
        }

        using var session = await context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        (Part Part, InventoryTransaction Transaction) result;
        try
        {
            result = await MoveWithinTransactionAsync(
                session,
                request,
                performedBy,
                cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await session.AbortTransactionAsync(cancellationToken);
            throw;
        }

        await NotifyLowStockAsync(result.Part, cancellationToken);
        return result.Transaction;
    }

    internal async Task<(Part Part, InventoryTransaction Transaction)> MoveWithinTransactionAsync(
        IClientSessionHandle session,
        StockMovementRequest request,
        string performedBy,
        CancellationToken cancellationToken)
    {
        var isIncrease = request.Type is InventoryTransactionType.Receipt
            or InventoryTransactionType.RepairReturn
            or InventoryTransactionType.AdjustmentIncrease;
        var delta = isIncrease ? request.Quantity : -request.Quantity;
        var parts = context.Collection<Part>();
        var filter = Builders<Part>.Filter.And(
            Builders<Part>.Filter.Eq(x => x.Id, request.PartId),
            Builders<Part>.Filter.Eq(x => x.IsDeleted, false));
        if (!isIncrease)
        {
            filter &= Builders<Part>.Filter.Gte(x => x.QuantityOnHand, request.Quantity);
        }

        var update = Builders<Part>.Update
            .Inc(x => x.QuantityOnHand, delta)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        if (request.Type == InventoryTransactionType.Receipt && request.UnitCost > 0)
        {
            update = update
                .Set(x => x.ImportPrice, request.UnitCost)
                .Set(x => x.StockPrice, request.UnitCost);
        }
        if (request.Type == InventoryTransactionType.Receipt && !string.IsNullOrWhiteSpace(request.SupplierId))
        {
            update = update.AddToSet(x => x.SupplierIds, request.SupplierId);
        }

        var part = await parts.FindOneAndUpdateAsync(
            session,
            filter,
            update,
            new FindOneAndUpdateOptions<Part, Part>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Phụ tùng không tồn tại hoặc số lượng tồn không đủ.");

        var transaction = new InventoryTransaction
        {
            Code = await sequences.NextAsync("inventory", "STK", cancellationToken),
            PartId = request.PartId,
            Type = request.Type,
            Quantity = request.Quantity,
            UnitCost = request.UnitCost,
            ReferenceType = request.ReferenceType,
            ReferenceId = request.ReferenceId,
            SupplierId = request.SupplierId,
            TransactionDate = request.TransactionDate?.ToUniversalTime() ?? DateTime.UtcNow,
            PerformedBy = performedBy,
            Notes = request.Notes?.Trim()
        };
        await context.Collection<InventoryTransaction>()
            .InsertOneAsync(session, transaction, cancellationToken: cancellationToken);

        if (request.Type == InventoryTransactionType.Receipt)
        {
            if (string.IsNullOrWhiteSpace(request.SupplierId))
            {
                throw new InvalidOperationException("Nhập kho phải thực hiện qua nhà cung cấp và phiếu chi.");
            }

            var stockFilter = Builders<SupplierPartStock>.Filter.And(
                Builders<SupplierPartStock>.Filter.Eq(x => x.SupplierId, request.SupplierId),
                Builders<SupplierPartStock>.Filter.Eq(x => x.PartId, request.PartId));
            var stockUpdate = Builders<SupplierPartStock>.Update
                .SetOnInsert(x => x.Id, MongoDB.Bson.ObjectId.GenerateNewId().ToString())
                .SetOnInsert(x => x.SupplierId, request.SupplierId)
                .SetOnInsert(x => x.PartId, request.PartId)
                .SetOnInsert(x => x.CreatedAt, DateTime.UtcNow)
                .Inc(x => x.QuantityOnHand, request.Quantity)
                .Set(x => x.LastUnitCost, request.UnitCost)
                .Set(x => x.LastReceiptAt, request.TransactionDate?.ToUniversalTime() ?? DateTime.UtcNow)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);
            await context.Collection<SupplierPartStock>().UpdateOneAsync(
                session,
                stockFilter,
                stockUpdate,
                new UpdateOptions { IsUpsert = true },
                cancellationToken);
        }
        else if (!isIncrease)
        {
            await DecreaseSupplierStocksAsync(session, request.PartId, request.Quantity, cancellationToken);
        }
        return (part, transaction);
    }

    private async Task DecreaseSupplierStocksAsync(
        IClientSessionHandle session,
        string partId,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        var stocks = await context.Collection<SupplierPartStock>()
            .Find(session, x => x.PartId == partId && x.QuantityOnHand > 0 && !x.IsDeleted)
            .SortBy(x => x.LastReceiptAt)
            .ToListAsync(cancellationToken);
        var remaining = quantity;
        foreach (var stock in stocks)
        {
            if (remaining <= 0) break;
            var deducted = Math.Min(stock.QuantityOnHand, remaining);
            await context.Collection<SupplierPartStock>().UpdateOneAsync(
                session,
                x => x.Id == stock.Id,
                Builders<SupplierPartStock>.Update
                    .Inc(x => x.QuantityOnHand, -deducted)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow),
                cancellationToken: cancellationToken);
            remaining -= deducted;
        }
    }

    internal async Task NotifyLowStockAsync(
        Part part,
        CancellationToken cancellationToken)
    {
        if (part.QuantityOnHand >= part.MinQuantity)
        {
            return;
        }

        var notification = new Notification
        {
            Role = SecurityRoles.Manager,
            Type = "LowStock",
            Title = "Phụ tùng dưới mức tối thiểu",
            Message = $"{part.Code} - {part.Name}: còn {part.QuantityOnHand} {part.Unit}.",
            ReferenceType = nameof(Part),
            ReferenceId = part.Id
        };
        await context.Collection<Notification>()
            .InsertOneAsync(notification, cancellationToken: cancellationToken);
        await hub.Clients.Group($"role:{SecurityRoles.Manager}")
            .SendAsync("notification", notification, cancellationToken);
        await hub.Clients.Group($"role:{SecurityRoles.Admin}")
            .SendAsync("notification", notification, cancellationToken);
    }
}
