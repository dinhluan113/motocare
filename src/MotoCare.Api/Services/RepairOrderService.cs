using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using MotoCare.Api.Contracts;
using MotoCare.Api.Domain;
using MotoCare.Api.Hubs;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Services;

public sealed class RepairOrderService(
    MongoDbContext context,
    SequenceService sequences,
    InventoryService inventory,
    IHubContext<NotificationHub> hub)
{
    private static readonly IReadOnlyDictionary<RepairOrderStatus, RepairOrderStatus[]> AllowedTransitions =
        new Dictionary<RepairOrderStatus, RepairOrderStatus[]>
        {
            [RepairOrderStatus.Received] =
                [RepairOrderStatus.Inspecting, RepairOrderStatus.Cancelled],
            [RepairOrderStatus.Inspecting] =
                [RepairOrderStatus.AwaitingApproval, RepairOrderStatus.Repairing, RepairOrderStatus.Cancelled],
            [RepairOrderStatus.AwaitingApproval] =
                [RepairOrderStatus.Repairing, RepairOrderStatus.Cancelled],
            [RepairOrderStatus.Repairing] =
                [RepairOrderStatus.AwaitingParts, RepairOrderStatus.Completed, RepairOrderStatus.Cancelled],
            [RepairOrderStatus.AwaitingParts] =
                [RepairOrderStatus.Repairing, RepairOrderStatus.Cancelled],
            [RepairOrderStatus.Completed] =
                [RepairOrderStatus.Delivered, RepairOrderStatus.Repairing],
            [RepairOrderStatus.Delivered] = [],
            [RepairOrderStatus.Cancelled] = []
        };

    public async Task<RepairOrder> CreateAsync(
        CreateRepairOrderRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var customer = await context.Collection<Customer>()
            .Find(x => x.Id == request.CustomerId && !x.IsDeleted && x.IsActive)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Khách hàng không tồn tại hoặc đã ngừng hoạt động.");
        var vehicle = await context.Collection<Vehicle>()
            .Find(x => x.Id == request.VehicleId && !x.IsDeleted && x.IsActive)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Xe không tồn tại hoặc đã ngừng hoạt động.");
        if (vehicle.CustomerId != customer.Id)
        {
            throw new InvalidOperationException("Xe không thuộc khách hàng đã chọn.");
        }

        var order = new RepairOrder
        {
            Code = await sequences.NextAsync("repair-order", "RO", cancellationToken),
            CustomerId = customer.Id,
            VehicleId = vehicle.Id,
            ExpectedDeliveryAt = request.ExpectedDeliveryAt?.ToUniversalTime(),
            OdometerIn = request.OdometerIn,
            FuelLevel = request.FuelLevel?.Trim(),
            VehicleCondition = request.VehicleCondition.Trim(),
            CustomerRequest = request.CustomerRequest.Trim(),
            Diagnosis = request.Diagnosis?.Trim(),
            InternalNotes = request.InternalNotes?.Trim(),
            Priority = request.Priority,
            CreatedBy = userId,
            ServiceAdvisorId = request.ServiceAdvisorId,
            StatusHistory =
            [
                new RepairStatusHistory
                {
                    ToStatus = RepairOrderStatus.Received,
                    ChangedBy = userId,
                    Note = "Tạo phiếu tiếp nhận."
                }
            ]
        };
        await context.Collection<RepairOrder>().InsertOneAsync(order, cancellationToken: cancellationToken);
        return order;
    }

    public async Task<RepairOrder> AddItemAsync(
        string orderId,
        AddRepairOrderItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var orders = context.Collection<RepairOrder>();
        var order = await orders.Find(x => x.Id == orderId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu sửa chữa.");
        if (order.Status is RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Không thể sửa hạng mục của phiếu đã giao hoặc đã hủy.");
        }

        var unitPrice = request.UnitPrice;
        if (request.ItemType == RepairItemType.Part)
        {
            if (string.IsNullOrWhiteSpace(request.PartId))
            {
                throw new InvalidOperationException("Hạng mục phụ tùng phải có PartId.");
            }

            var part = await context.Collection<Part>()
                .Find(x => x.Id == request.PartId && !x.IsDeleted && x.IsActive)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Phụ tùng không tồn tại hoặc đã ngừng bán.");
            if (unitPrice == 0)
            {
                unitPrice = part.SalePrice;
            }
        }

        var item = new RepairOrderItem
        {
            ItemType = request.ItemType,
            ServiceId = request.ServiceId,
            PartId = request.PartId,
            Description = request.Description.Trim(),
            Quantity = request.Quantity,
            UnitPrice = unitPrice,
            DiscountAmount = request.DiscountAmount,
            LineTotal = Math.Max(0, request.Quantity * unitPrice - request.DiscountAmount),
            AssignedEmployeeId = request.AssignedEmployeeId,
            TechnicianNotes = request.TechnicianNotes?.Trim()
        };
        order.Items.Add(item);
        Recalculate(order);
        order.UpdatedAt = DateTime.UtcNow;
        await orders.ReplaceOneAsync(x => x.Id == order.Id, order, cancellationToken: cancellationToken);

        if (!string.IsNullOrWhiteSpace(item.AssignedEmployeeId))
        {
            await NotifyAssignment(order, item, cancellationToken);
        }

        return order;
    }

    public async Task<RepairOrder> ChangeStatusAsync(
        string orderId,
        ChangeRepairStatusRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var orders = context.Collection<RepairOrder>();
        var order = await orders.Find(x => x.Id == orderId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu sửa chữa.");

        if (order.Status == request.Status)
        {
            return order;
        }

        if (!AllowedTransitions[order.Status].Contains(request.Status))
        {
            throw new InvalidOperationException(
                $"Không thể chuyển phiếu từ {order.Status} sang {request.Status}.");
        }

        var previous = order.Status;
        order.Status = request.Status;
        order.UpdatedAt = DateTime.UtcNow;
        if (request.Status == RepairOrderStatus.Delivered)
        {
            order.DeliveredAt = DateTime.UtcNow;
        }

        order.StatusHistory.Add(new RepairStatusHistory
        {
            FromStatus = previous,
            ToStatus = request.Status,
            ChangedBy = userId,
            Note = request.Note?.Trim()
        });
        await orders.ReplaceOneAsync(x => x.Id == order.Id, order, cancellationToken: cancellationToken);

        var notification = new Notification
        {
            Role = SecurityRoles.Receptionist,
            Type = "RepairStatusChanged",
            Title = $"Phiếu {order.Code} đổi trạng thái",
            Message = $"{previous} → {request.Status}",
            ReferenceType = nameof(RepairOrder),
            ReferenceId = order.Id
        };
        await context.Collection<Notification>().InsertOneAsync(notification, cancellationToken: cancellationToken);
        await hub.Clients.Group($"role:{SecurityRoles.Receptionist}")
            .SendAsync("notification", notification, cancellationToken);
        return order;
    }

    public async Task<RepairOrder> UpdateWorkAsync(
        string orderId,
        string itemId,
        UpdateRepairWorkRequest request,
        CancellationToken cancellationToken = default)
    {
        var orders = context.Collection<RepairOrder>();
        var order = await orders.Find(x => x.Id == orderId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu sửa chữa.");
        var item = order.Items.FirstOrDefault(x => x.Id == itemId)
            ?? throw new KeyNotFoundException("Không tìm thấy hạng mục sửa chữa.");

        item.WorkStatus = request.Status;
        item.TechnicianNotes = request.TechnicianNotes?.Trim();
        if (request.Status == WorkStatus.InProgress && item.StartedAt is null)
        {
            item.StartedAt = DateTime.UtcNow;
        }

        if (request.Status == WorkStatus.Completed)
        {
            item.CompletedAt = DateTime.UtcNow;
        }

        order.UpdatedAt = DateTime.UtcNow;
        await orders.ReplaceOneAsync(x => x.Id == order.Id, order, cancellationToken: cancellationToken);
        return order;
    }

    public async Task<RepairOrder> IssuePartsAsync(
        string orderId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        using var session = await context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        var changedParts = new List<Part>();
        RepairOrder order;
        try
        {
            var orders = context.Collection<RepairOrder>();
            order = await orders.Find(session, x => x.Id == orderId && !x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy phiếu sửa chữa.");
            if (order.Status is RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled)
            {
                throw new InvalidOperationException("Không thể xuất kho cho phiếu đã giao hoặc đã hủy.");
            }

            foreach (var item in order.Items.Where(x =>
                         x.ItemType == RepairItemType.Part
                         && !x.InventoryIssued
                         && !string.IsNullOrWhiteSpace(x.PartId)))
            {
                var result = await inventory.MoveWithinTransactionAsync(
                    session,
                    new StockMovementRequest(
                        item.PartId!,
                        InventoryTransactionType.RepairIssue,
                        item.Quantity,
                        0,
                        nameof(RepairOrder),
                        order.Id,
                        $"Xuất cho phiếu {order.Code}"),
                    userId,
                    cancellationToken);
                changedParts.Add(result.Part);
                item.InventoryIssued = true;
            }

            order.UpdatedAt = DateTime.UtcNow;
            await orders.ReplaceOneAsync(
                session,
                x => x.Id == order.Id,
                order,
                cancellationToken: cancellationToken);
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

        foreach (var part in changedParts)
        {
            await inventory.NotifyLowStockAsync(part, cancellationToken);
        }

        return order;
    }

    private async Task NotifyAssignment(
        RepairOrder order,
        RepairOrderItem item,
        CancellationToken cancellationToken)
    {
        var user = await context.Collection<AppUser>()
            .Find(x => x.EmployeeId == item.AssignedEmployeeId && x.IsActive && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        var notification = new Notification
        {
            UserId = user?.Id,
            Role = user is null ? SecurityRoles.Technician : null,
            Type = "RepairAssignment",
            Title = $"Công việc mới - {order.Code}",
            Message = item.Description,
            ReferenceType = nameof(RepairOrder),
            ReferenceId = order.Id
        };
        await context.Collection<Notification>().InsertOneAsync(notification, cancellationToken: cancellationToken);
        if (user is not null)
        {
            await hub.Clients.User(user.Id).SendAsync("notification", notification, cancellationToken);
        }
        else
        {
            await hub.Clients.Group($"role:{SecurityRoles.Technician}")
                .SendAsync("notification", notification, cancellationToken);
        }
    }

    private static void Recalculate(RepairOrder order)
    {
        order.EstimatedTotal = order.Items.Sum(x => x.LineTotal);
        order.FinalTotal = Math.Max(0, order.EstimatedTotal - order.DiscountAmount);
    }
}
