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
                [RepairOrderStatus.AwaitingParts, RepairOrderStatus.Cancelled],
            [RepairOrderStatus.AwaitingParts] =
                [RepairOrderStatus.Repairing, RepairOrderStatus.Cancelled],
            [RepairOrderStatus.Completed] = [RepairOrderStatus.Delivered],
            [RepairOrderStatus.Delivered] = [],
            [RepairOrderStatus.Cancelled] = []
        };

    public async Task<RepairOrder> CreateAsync(
        CreateRepairOrderRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var conditionImages = ValidateConditionImages(request.VehicleConditionImages);
        var (customer, vehicle, createdReception) = await ResolveReceptionAsync(request, cancellationToken);
        var order = new RepairOrder
        {
            Code = await sequences.NextAsync("repair-order", "RO", cancellationToken),
            CustomerId = customer.Id,
            VehicleId = vehicle.Id,
            ExpectedDeliveryAt = request.ExpectedDeliveryAt?.ToUniversalTime(),
            OdometerIn = request.OdometerIn,
            FuelLevel = request.FuelLevel?.Trim(),
            VehicleCondition = request.VehicleCondition.Trim(),
            VehicleConditionImages = conditionImages,
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
        try
        {
            await context.Collection<RepairOrder>().InsertOneAsync(order, cancellationToken: cancellationToken);
        }
        catch
        {
            if (createdReception)
            {
                await context.Collection<Vehicle>().DeleteOneAsync(
                    x => x.Id == vehicle.Id,
                    CancellationToken.None);
                await context.Collection<Customer>().DeleteOneAsync(
                    x => x.Id == customer.Id,
                    CancellationToken.None);
            }
            throw;
        }
        if (request.OdometerIn.HasValue && vehicle.Odometer != request.OdometerIn)
        {
            vehicle.Odometer = request.OdometerIn;
            vehicle.UpdatedAt = DateTime.UtcNow;
            await context.Collection<Vehicle>().ReplaceOneAsync(
                x => x.Id == vehicle.Id && !x.IsDeleted,
                vehicle,
                cancellationToken: cancellationToken);
        }
        return order;
    }

    private async Task<(Customer Customer, Vehicle Vehicle, bool Created)> ResolveReceptionAsync(
        CreateRepairOrderRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedPlate = Normalize.LicensePlate(request.LicensePlate ?? string.Empty);
        if (!string.IsNullOrEmpty(normalizedPlate))
        {
            var existingVehicle = await context.Collection<Vehicle>()
                .Find(x => x.NormalizedLicensePlate == normalizedPlate && !x.IsDeleted && x.IsActive)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingVehicle is not null)
            {
                var existingCustomer = await GetActiveCustomerAsync(
                    existingVehicle.CustomerId,
                    cancellationToken);
                return (existingCustomer, existingVehicle, false);
            }

            return await CreateWalkInReceptionAsync(
                request.LicensePlate!.Trim().ToUpperInvariant(),
                normalizedPlate,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(request.CustomerId) || string.IsNullOrWhiteSpace(request.VehicleId))
        {
            throw new InvalidOperationException("Vui lòng nhập biển số xe.");
        }

        var customer = await GetActiveCustomerAsync(request.CustomerId, cancellationToken);
        var vehicle = await context.Collection<Vehicle>()
            .Find(x => x.Id == request.VehicleId && !x.IsDeleted && x.IsActive)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Xe không tồn tại hoặc đã ngừng hoạt động.");
        if (vehicle.CustomerId != customer.Id)
        {
            throw new InvalidOperationException("Xe không thuộc khách hàng đã chọn.");
        }
        return (customer, vehicle, false);
    }

    private async Task<Customer> GetActiveCustomerAsync(
        string customerId,
        CancellationToken cancellationToken) =>
        await context.Collection<Customer>()
            .Find(x => x.Id == customerId && !x.IsDeleted && x.IsActive)
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new InvalidOperationException("Khách hàng không tồn tại hoặc đã ngừng hoạt động.");

    private async Task<(Customer Customer, Vehicle Vehicle, bool Created)> CreateWalkInReceptionAsync(
        string licensePlate,
        string normalizedPlate,
        CancellationToken cancellationToken)
    {
        var customer = new Customer
        {
            Code = await sequences.NextAsync("customer", "CUS", cancellationToken),
            FullName = $"Khách lẻ - {licensePlate}",
            Notes = "Hồ sơ được tạo tự động khi tiếp nhận xe chỉ có biển số."
        };
        await context.Collection<Customer>().InsertOneAsync(
            customer,
            cancellationToken: cancellationToken);

        var vehicle = new Vehicle
        {
            CustomerId = customer.Id,
            LicensePlate = licensePlate,
            NormalizedLicensePlate = normalizedPlate,
            Notes = "Xe được tạo tự động từ phiếu tiếp nhận."
        };
        try
        {
            await context.Collection<Vehicle>().InsertOneAsync(
                vehicle,
                cancellationToken: cancellationToken);
            return (customer, vehicle, true);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            await context.Collection<Customer>().DeleteOneAsync(
                x => x.Id == customer.Id,
                CancellationToken.None);
            var existingVehicle = await context.Collection<Vehicle>()
                .Find(x => x.NormalizedLicensePlate == normalizedPlate && !x.IsDeleted && x.IsActive)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingVehicle is null)
            {
                throw;
            }
            return (await GetActiveCustomerAsync(existingVehicle.CustomerId, cancellationToken), existingVehicle, false);
        }
        catch
        {
            await context.Collection<Customer>().DeleteOneAsync(
                x => x.Id == customer.Id,
                CancellationToken.None);
            throw;
        }
    }

    public async Task<RepairOrder> UpdateConditionImagesAsync(
        string orderId,
        IReadOnlyList<string> images,
        CancellationToken cancellationToken = default)
    {
        var orders = context.Collection<RepairOrder>();
        var order = await orders.Find(x => x.Id == orderId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu sửa chữa.");
        if (order.Status is RepairOrderStatus.Completed or RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Không thể cập nhật ảnh của phiếu đã hoàn tất, đã giao hoặc đã hủy.");
        }

        order.VehicleConditionImages = ValidateConditionImages(images);
        order.UpdatedAt = DateTime.UtcNow;
        await orders.ReplaceOneAsync(
            x => x.Id == orderId && !x.IsDeleted,
            order,
            cancellationToken: cancellationToken);
        return order;
    }

    public async Task<RepairOrder> UpdateOdometerAsync(
        string orderId,
        int odometerIn,
        CancellationToken cancellationToken = default)
    {
        var orders = context.Collection<RepairOrder>();
        var order = await orders.Find(x => x.Id == orderId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu sửa chữa.");
        if (order.Status is RepairOrderStatus.Completed or RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Không thể cập nhật ODO của phiếu đã hoàn tất, đã giao hoặc đã hủy.");
        }

        var vehicle = await context.Collection<Vehicle>()
            .Find(x => x.Id == order.VehicleId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy xe của phiếu sửa chữa.");
        order.OdometerIn = odometerIn;
        order.UpdatedAt = DateTime.UtcNow;
        vehicle.Odometer = odometerIn;
        vehicle.UpdatedAt = DateTime.UtcNow;
        await orders.ReplaceOneAsync(
            x => x.Id == orderId && !x.IsDeleted,
            order,
            cancellationToken: cancellationToken);
        await context.Collection<Vehicle>().ReplaceOneAsync(
            x => x.Id == vehicle.Id && !x.IsDeleted,
            vehicle,
            cancellationToken: cancellationToken);
        return order;
    }

    private static List<string> ValidateConditionImages(IReadOnlyList<string>? images)
    {
        var normalized = images?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList() ?? [];
        if (normalized.Count > 10)
        {
            throw new InvalidOperationException("Chỉ được lưu tối đa 10 ảnh tình trạng xe.");
        }
        if (normalized.Any(x => !x.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            && !x.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Ảnh tình trạng xe không hợp lệ.");
        }
        if (normalized.Any(x => x.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
            && x.Length > 3_000_000))
        {
            throw new InvalidOperationException("Mỗi ảnh tình trạng xe tối đa 2 MB.");
        }
        return normalized;
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
        if (order.Status is RepairOrderStatus.Completed or RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Không thể thêm hạng mục vào phiếu đã hoàn tất, đã giao hoặc đã hủy.");
        }

        var unitPrice = request.UnitPrice;
        var quantity = request.ItemType == RepairItemType.Service ? 1 : request.Quantity;
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
        else
        {
            if (string.IsNullOrWhiteSpace(request.ServiceId))
            {
                throw new InvalidOperationException("Hạng mục dịch vụ phải được chọn từ danh mục dịch vụ.");
            }

            var service = await context.Collection<ServiceCategory>()
                .Find(x => x.Id == request.ServiceId && !x.IsDeleted && x.IsActive)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Dịch vụ không tồn tại hoặc đã ngừng hoạt động.");
            if (unitPrice == 0)
            {
                unitPrice = service.DefaultPrice;
            }
        }

        var discountValue = request.DiscountValue > 0 ? request.DiscountValue : request.DiscountAmount;
        var discountAmount = CalculateDiscount(quantity * unitPrice, request.DiscountType, discountValue);
        var item = new RepairOrderItem
        {
            ItemType = request.ItemType,
            ServiceId = request.ServiceId,
            PartId = request.PartId,
            Description = request.Description.Trim(),
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountType = request.DiscountType,
            DiscountValue = discountValue,
            DiscountAmount = discountAmount,
            LineTotal = Math.Max(0, quantity * unitPrice - discountAmount),
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

    public async Task<RepairOrder> UpdateItemAsync(
        string orderId,
        string itemId,
        UpdateRepairOrderItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var orders = context.Collection<RepairOrder>();
        var order = await orders.Find(x => x.Id == orderId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu sửa chữa.");
        if (order.Status is RepairOrderStatus.Completed or RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Không thể sửa hạng mục của phiếu đã hoàn tất, đã giao hoặc đã hủy.");
        }

        var item = order.Items.FirstOrDefault(x => x.Id == itemId)
            ?? throw new KeyNotFoundException("Không tìm thấy hạng mục sửa chữa.");
        if (item.InventoryIssued)
        {
            throw new InvalidOperationException("Không thể cập nhật phụ tùng đã xuất kho.");
        }

        var unitPrice = request.UnitPrice;
        var quantity = request.ItemType == RepairItemType.Service ? 1 : request.Quantity;
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
        else
        {
            if (string.IsNullOrWhiteSpace(request.ServiceId))
            {
                throw new InvalidOperationException("Hạng mục dịch vụ phải được chọn từ danh mục dịch vụ.");
            }

            var service = await context.Collection<ServiceCategory>()
                .Find(x => x.Id == request.ServiceId && !x.IsDeleted && x.IsActive)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Dịch vụ không tồn tại hoặc đã ngừng hoạt động.");
            if (unitPrice == 0)
            {
                unitPrice = service.DefaultPrice;
            }
        }

        item.ItemType = request.ItemType;
        item.ServiceId = request.ItemType == RepairItemType.Service ? request.ServiceId : null;
        item.PartId = request.ItemType == RepairItemType.Part ? request.PartId : null;
        item.Description = request.Description.Trim();
        item.Quantity = quantity;
        item.UnitPrice = unitPrice;
        var discountValue = request.DiscountValue > 0 ? request.DiscountValue : request.DiscountAmount;
        var discountAmount = CalculateDiscount(quantity * unitPrice, request.DiscountType, discountValue);
        item.DiscountType = request.DiscountType;
        item.DiscountValue = discountValue;
        item.DiscountAmount = discountAmount;
        item.LineTotal = Math.Max(0, quantity * unitPrice - discountAmount);
        item.AssignedEmployeeId = request.AssignedEmployeeId;
        item.TechnicianNotes = request.TechnicianNotes?.Trim();

        Recalculate(order);
        order.UpdatedAt = DateTime.UtcNow;
        await orders.ReplaceOneAsync(x => x.Id == order.Id, order, cancellationToken: cancellationToken);

        if (!string.IsNullOrWhiteSpace(item.AssignedEmployeeId))
        {
            await NotifyAssignment(order, item, cancellationToken);
        }

        return order;
    }

    public async Task<RepairOrder> DeleteItemAsync(
        string orderId,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        var orders = context.Collection<RepairOrder>();
        var order = await orders.Find(x => x.Id == orderId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu sửa chữa.");
        if (order.Status is RepairOrderStatus.Completed or RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Không thể xóa hạng mục của phiếu đã hoàn tất, đã giao hoặc đã hủy.");
        }

        var item = order.Items.FirstOrDefault(x => x.Id == itemId)
            ?? throw new KeyNotFoundException("Không tìm thấy hạng mục sửa chữa.");
        if (item.InventoryIssued)
        {
            throw new InvalidOperationException("Không thể xóa phụ tùng đã xuất kho.");
        }

        var hasActiveInvoice = await context.Collection<Invoice>()
            .Find(x => x.RepairOrderId == orderId
                && !x.IsDeleted
                && x.PaymentStatus != InvoicePaymentStatus.Cancelled)
            .AnyAsync(cancellationToken);
        if (hasActiveInvoice)
        {
            throw new InvalidOperationException(
                "Không thể xóa hạng mục khi phiếu sửa chữa đang có hóa đơn còn hiệu lực.");
        }

        order.Items.Remove(item);
        Recalculate(order);
        order.UpdatedAt = DateTime.UtcNow;
        await orders.ReplaceOneAsync(
            x => x.Id == order.Id && !x.IsDeleted,
            order,
            cancellationToken: cancellationToken);
        return order;
    }

    public async Task<RepairOrder> IssuePartAsync(
        string orderId,
        string itemId,
        IssueRepairPartRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var orders = context.Collection<RepairOrder>();
        var order = await orders.Find(x => x.Id == orderId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu sửa chữa.");
        if (order.Status is RepairOrderStatus.Completed or RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Không thể xuất kho cho phiếu đã hoàn tất, đã giao hoặc đã hủy.");
        }

        var item = order.Items.FirstOrDefault(x => x.Id == itemId)
            ?? throw new KeyNotFoundException("Không tìm thấy hạng mục sửa chữa.");
        if (item.ItemType != RepairItemType.Part || string.IsNullOrWhiteSpace(item.PartId))
        {
            throw new InvalidOperationException("Chỉ có thể xuất kho cho hạng mục phụ tùng.");
        }
        if (item.WorkStatus == WorkStatus.Cancelled)
        {
            throw new InvalidOperationException("Không thể xuất kho cho hạng mục đã hủy.");
        }
        if (item.InventoryIssued)
        {
            throw new InvalidOperationException("Phụ tùng này đã được xuất kho.");
        }

        var part = await context.Collection<Part>()
            .Find(x => x.Id == item.PartId && !x.IsDeleted && x.IsActive)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Phụ tùng không tồn tại hoặc đã ngừng kinh doanh.");
        if (part.QuantityOnHand < item.Quantity)
        {
            throw new InvalidOperationException(
                $"Tồn kho không đủ. Hiện còn {part.QuantityOnHand} {part.Unit}, cần xuất {item.Quantity} {part.Unit}.");
        }
        var locationIds = (part.WarehouseLocationIds ?? [])
            .Concat(part.WarehouseStocks?.Select(x => x.WarehouseLocationId) ?? [])
            .Append(part.WarehouseLocationId ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
        var selectedLocation = await context.Collection<WarehouseLocation>()
            .Find(x => x.Id == request.WarehouseLocationId
                && locationIds.Contains(x.Id)
                && !x.IsDeleted
                && x.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
        if (selectedLocation is null)
        {
            throw new InvalidOperationException(
                "Vị trí xuất kho không hợp lệ hoặc đã ngừng sử dụng.");
        }
        var selectedLocationQuantity = part.WarehouseStocks?.FirstOrDefault(x =>
                x.WarehouseLocationId == selectedLocation.Id)?.QuantityOnHand
            ?? (part.WarehouseLocationId == selectedLocation.Id && !(part.WarehouseStocks?.Any() ?? false)
                ? part.QuantityOnHand
                : 0);
        if (selectedLocationQuantity < item.Quantity)
        {
            throw new InvalidOperationException(
                $"Ngăn {selectedLocation.Code} không đủ tồn kho. Hiện có {selectedLocationQuantity} {part.Unit}, cần xuất {item.Quantity} {part.Unit}.");
        }

        using var session = await context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        Part changedPart;
        try
        {
            var movement = await inventory.MoveWithinTransactionAsync(
                session,
                new StockMovementRequest(
                    item.PartId,
                    InventoryTransactionType.RepairIssue,
                    item.Quantity,
                    0,
                    nameof(RepairOrder),
                    order.Id,
                    $"Xuất cho {order.Code} · {item.Description}",
                    WarehouseLocationId: selectedLocation.Id),
                userId,
                cancellationToken);
            changedPart = movement.Part;

            var issueFilter = Builders<RepairOrder>.Filter.And(
                Builders<RepairOrder>.Filter.Eq(x => x.Id, order.Id),
                Builders<RepairOrder>.Filter.Eq(x => x.IsDeleted, false),
                Builders<RepairOrder>.Filter.ElemMatch(
                    x => x.Items,
                    x => x.Id == itemId && !x.InventoryIssued));
            var issueUpdate = Builders<RepairOrder>.Update
                .Set("items.$.inventoryIssued", true)
                .Set("items.$.issuedWarehouseLocationId", selectedLocation.Id)
                .Set("items.$.issuedWarehouseLocationCode", selectedLocation.Code)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);
            var updateResult = await orders.UpdateOneAsync(
                session,
                issueFilter,
                issueUpdate,
                cancellationToken: cancellationToken);
            if (updateResult.ModifiedCount != 1)
            {
                throw new InvalidOperationException("Phụ tùng đã được xuất bởi một thao tác khác.");
            }
            await session.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(cancellationToken);
            throw;
        }

        await inventory.NotifyLowStockAsync(changedPart, cancellationToken);
        return await orders.Find(x => x.Id == orderId && !x.IsDeleted)
            .FirstAsync(cancellationToken);
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
            Role = SecurityRoles.Employee,
            Type = "RepairStatusChanged",
            Title = $"Phiếu {order.Code} đổi trạng thái",
            Message = $"{previous} → {request.Status}",
            ReferenceType = nameof(RepairOrder),
            ReferenceId = order.Id
        };
        await context.Collection<Notification>().InsertOneAsync(notification, cancellationToken: cancellationToken);
        await hub.Clients.Group($"role:{SecurityRoles.Employee}")
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
        if (order.Status is RepairOrderStatus.Completed or RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Không thể cập nhật tiến độ của phiếu đã hoàn tất, đã giao hoặc đã hủy.");
        }
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
            Role = user is null ? SecurityRoles.Employee : null,
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
            await hub.Clients.Group($"role:{SecurityRoles.Employee}")
                .SendAsync("notification", notification, cancellationToken);
        }
    }

    private static void Recalculate(RepairOrder order)
    {
        order.EstimatedTotal = order.Items.Sum(x => x.LineTotal);
        order.FinalTotal = Math.Max(0, order.EstimatedTotal - order.DiscountAmount);
    }

    private static decimal CalculateDiscount(decimal gross, DiscountType type, decimal value)
    {
        if (value < 0 || (type == DiscountType.Percentage && value > 100))
        {
            throw new InvalidOperationException("Giá trị giảm giá không hợp lệ.");
        }

        var discount = type == DiscountType.Percentage
            ? decimal.Round(gross * value / 100m, 0, MidpointRounding.AwayFromZero)
            : value;
        return Math.Min(gross, discount);
    }
}
