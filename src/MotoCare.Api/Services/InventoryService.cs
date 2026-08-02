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
        var parts = context.Collection<Part>();
        var filter = Builders<Part>.Filter.And(
            Builders<Part>.Filter.Eq(x => x.Id, request.PartId),
            Builders<Part>.Filter.Eq(x => x.IsDeleted, false));
        var part = await parts.Find(session, filter).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Phụ tùng không tồn tại.");
        var originalQuantity = part.QuantityOnHand;
        if (!isIncrease && originalQuantity < request.Quantity)
        {
            throw new InvalidOperationException("Số lượng tồn không đủ.");
        }

        part.WarehouseLocationIds = (part.WarehouseLocationIds ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
        if (!string.IsNullOrWhiteSpace(part.WarehouseLocationId)
            && !part.WarehouseLocationIds.Contains(part.WarehouseLocationId))
        {
            part.WarehouseLocationIds.Insert(0, part.WarehouseLocationId);
        }
        part.WarehouseStocks ??= [];
        if (part.WarehouseStocks.Count == 0 && originalQuantity > 0
            && !string.IsNullOrWhiteSpace(part.WarehouseLocationId))
        {
            part.WarehouseStocks.Add(new PartWarehouseStock
            {
                WarehouseLocationId = part.WarehouseLocationId,
                QuantityOnHand = originalQuantity
            });
        }

        var allocations = new List<InventoryLocationAllocation>();
        if (isIncrease)
        {
            var targetLocationId = request.WarehouseLocationId
                ?? part.WarehouseLocationId
                ?? part.WarehouseLocationIds.FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "Phụ tùng chưa có vị trí nhập mặc định trong kho.");
            var targetLocation = await context.Collection<WarehouseLocation>()
                .Find(session, x => x.Id == targetLocationId && !x.IsDeleted && x.IsActive)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Vị trí nhập kho không tồn tại hoặc đã ngừng sử dụng.");
            if (!part.WarehouseLocationIds.Contains(targetLocationId))
            {
                part.WarehouseLocationIds.Add(targetLocationId);
            }
            part.WarehouseLocationId ??= targetLocationId;
            var stock = part.WarehouseStocks.FirstOrDefault(x => x.WarehouseLocationId == targetLocationId);
            if (stock is null)
            {
                stock = new PartWarehouseStock { WarehouseLocationId = targetLocationId };
                part.WarehouseStocks.Add(stock);
            }
            stock.QuantityOnHand += request.Quantity;
            allocations.Add(new InventoryLocationAllocation
            {
                WarehouseLocationId = targetLocation.Id,
                WarehouseLocationCode = targetLocation.Code,
                Quantity = request.Quantity
            });
        }
        else
        {
            var remaining = request.Quantity;
            var availableStocks = part.WarehouseStocks
                .Where(x => x.QuantityOnHand > 0
                    && (request.WarehouseLocationId is null
                        || x.WarehouseLocationId == request.WarehouseLocationId))
                .OrderByDescending(x => x.WarehouseLocationId == part.WarehouseLocationId)
                .ToList();
            var locationIds = availableStocks.Select(x => x.WarehouseLocationId).Distinct().ToList();
            var locations = await context.Collection<WarehouseLocation>()
                .Find(session, x => locationIds.Contains(x.Id) && !x.IsDeleted && x.IsActive)
                .ToListAsync(cancellationToken);
            foreach (var stock in availableStocks)
            {
                if (remaining <= 0) break;
                var location = locations.FirstOrDefault(x => x.Id == stock.WarehouseLocationId);
                if (location is null) continue;
                var quantity = Math.Min(stock.QuantityOnHand, remaining);
                stock.QuantityOnHand -= quantity;
                remaining -= quantity;
                allocations.Add(new InventoryLocationAllocation
                {
                    WarehouseLocationId = location.Id,
                    WarehouseLocationCode = location.Code,
                    Quantity = quantity
                });
            }
            if (remaining > 0)
            {
                throw new InvalidOperationException(
                    "Tồn tại các ngăn đang hoạt động không đủ số lượng cần xuất.");
            }
        }

        part.QuantityOnHand = isIncrease
            ? originalQuantity + request.Quantity
            : originalQuantity - request.Quantity;
        part.UpdatedAt = DateTime.UtcNow;
        if (request.Type == InventoryTransactionType.Receipt && request.UnitCost > 0)
        {
            part.ImportPrice = request.UnitCost;
            part.StockPrice = request.UnitCost;
        }
        if (request.Type == InventoryTransactionType.Receipt && !string.IsNullOrWhiteSpace(request.SupplierId))
        {
            if (!part.SupplierIds.Contains(request.SupplierId)) part.SupplierIds.Add(request.SupplierId);
        }
        var replaceResult = await parts.ReplaceOneAsync(
            session,
            filter & Builders<Part>.Filter.Eq(x => x.QuantityOnHand, originalQuantity),
            part,
            cancellationToken: cancellationToken);
        if (replaceResult.ModifiedCount != 1)
        {
            throw new InvalidOperationException("Tồn kho vừa thay đổi bởi một thao tác khác. Vui lòng thử lại.");
        }

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
            WarehouseLocationId = allocations.Count == 1 ? allocations[0].WarehouseLocationId : null,
            WarehouseLocationCode = string.Join(", ", allocations.Select(x => x.WarehouseLocationCode)),
            LocationAllocations = allocations,
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

    public async Task<InventoryTransaction> TransferAsync(
        StockTransferRequest request,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new InvalidOperationException("Số lượng chuyển phải lớn hơn 0.");
        }
        if (request.FromWarehouseLocationId == request.ToWarehouseLocationId)
        {
            throw new InvalidOperationException("Ngăn nguồn và ngăn đích phải khác nhau.");
        }

        using var session = await context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var parts = context.Collection<Part>();
            var partFilter = Builders<Part>.Filter.And(
                Builders<Part>.Filter.Eq(x => x.Id, request.PartId),
                Builders<Part>.Filter.Eq(x => x.IsDeleted, false));
            var part = await parts.Find(session, partFilter).FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Phụ tùng không tồn tại.");
            var originalUpdatedAt = part.UpdatedAt;

            part.WarehouseLocationIds = (part.WarehouseLocationIds ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
            part.WarehouseStocks ??= [];
            if (part.WarehouseStocks.Count == 0 && part.QuantityOnHand > 0
                && !string.IsNullOrWhiteSpace(part.WarehouseLocationId))
            {
                part.WarehouseStocks.Add(new PartWarehouseStock
                {
                    WarehouseLocationId = part.WarehouseLocationId,
                    QuantityOnHand = part.QuantityOnHand
                });
            }

            var locationIds = new[]
            {
                request.FromWarehouseLocationId,
                request.ToWarehouseLocationId
            };
            var locations = await context.Collection<WarehouseLocation>()
                .Find(session, x => locationIds.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync(cancellationToken);
            var sourceLocation = locations.FirstOrDefault(x => x.Id == request.FromWarehouseLocationId)
                ?? throw new InvalidOperationException("Ngăn nguồn không tồn tại.");
            var destinationLocation = locations.FirstOrDefault(x =>
                    x.Id == request.ToWarehouseLocationId && x.IsActive)
                ?? throw new InvalidOperationException("Ngăn đích không tồn tại hoặc đã ngừng sử dụng.");

            var sourceStock = part.WarehouseStocks
                .FirstOrDefault(x => x.WarehouseLocationId == sourceLocation.Id);
            if (sourceStock is null || sourceStock.QuantityOnHand < request.Quantity)
            {
                throw new InvalidOperationException(
                    $"Ngăn {sourceLocation.Code} không đủ tồn kho. Hiện có {sourceStock?.QuantityOnHand ?? 0} {part.Unit}.");
            }

            var destinationStock = part.WarehouseStocks
                .FirstOrDefault(x => x.WarehouseLocationId == destinationLocation.Id);
            if (destinationStock is null)
            {
                destinationStock = new PartWarehouseStock
                {
                    WarehouseLocationId = destinationLocation.Id
                };
                part.WarehouseStocks.Add(destinationStock);
            }
            sourceStock.QuantityOnHand -= request.Quantity;
            destinationStock.QuantityOnHand += request.Quantity;
            if (!part.WarehouseLocationIds.Contains(sourceLocation.Id))
            {
                part.WarehouseLocationIds.Add(sourceLocation.Id);
            }
            if (!part.WarehouseLocationIds.Contains(destinationLocation.Id))
            {
                part.WarehouseLocationIds.Add(destinationLocation.Id);
            }
            part.WarehouseLocationId ??= destinationLocation.Id;
            part.UpdatedAt = DateTime.UtcNow;

            var replaceResult = await parts.ReplaceOneAsync(
                session,
                partFilter & Builders<Part>.Filter.Eq(x => x.UpdatedAt, originalUpdatedAt),
                part,
                cancellationToken: cancellationToken);
            if (replaceResult.ModifiedCount != 1)
            {
                throw new InvalidOperationException(
                    "Tồn kho vừa thay đổi bởi một thao tác khác. Vui lòng tải lại và thử lại.");
            }

            var transaction = new InventoryTransaction
            {
                Code = await sequences.NextAsync("inventory", "STK", cancellationToken),
                PartId = part.Id,
                Type = InventoryTransactionType.Transfer,
                Quantity = request.Quantity,
                UnitCost = part.StockPrice,
                ReferenceType = "WarehouseTransfer",
                FromWarehouseLocationId = sourceLocation.Id,
                FromWarehouseLocationCode = sourceLocation.Code,
                ToWarehouseLocationId = destinationLocation.Id,
                ToWarehouseLocationCode = destinationLocation.Code,
                WarehouseLocationCode = $"{sourceLocation.Code} → {destinationLocation.Code}",
                TransactionDate = DateTime.UtcNow,
                PerformedBy = performedBy,
                Notes = request.Notes.Trim()
            };
            await context.Collection<InventoryTransaction>()
                .InsertOneAsync(session, transaction, cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return transaction;
        }
        catch
        {
            await session.AbortTransactionAsync(cancellationToken);
            throw;
        }
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
