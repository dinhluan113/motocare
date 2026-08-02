using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Globalization;
using System.Security.Claims;
using MongoDB.Bson;
using MongoDB.Driver;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;
using MotoCare.Api.Services;

namespace MotoCare.Api.Controllers;

[Route("api/v1/employees")]
public sealed class EmployeesController(
    IMongoRepository<Employee> repository,
    MongoDbContext context)
    : CrudController<Employee>(repository)
{
    [HttpGet("{id}/work-history")]
    public async Task<IActionResult> WorkHistory(
        string id,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var orders = await context.Collection<RepairOrder>()
            .Find(x => x.Items.Any(item => item.AssignedEmployeeId == id) && !x.IsDeleted)
            .SortByDescending(x => x.ReceivedAt)
            .Limit(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
        var history = orders.Select(order => new
        {
            order.Id,
            order.Code,
            order.Status,
            order.ReceivedAt,
            order.DeliveredAt,
            items = order.Items.Where(x => x.AssignedEmployeeId == id).ToList()
        });
        return Ok(ApiEnvelope.Ok(history));
    }

    protected override FilterDefinition<Employee> BuildSearchFilter(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return Builders<Employee>.Filter.Empty;
        }

        var regex = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(search.Trim()), "i");
        return Builders<Employee>.Filter.Or(
            Builders<Employee>.Filter.Regex(x => x.FullName, regex),
            Builders<Employee>.Filter.Regex(x => x.EmployeeCode, regex),
            Builders<Employee>.Filter.Regex(x => x.Phone, regex));
    }

    protected override void Prepare(Employee entity)
    {
        entity.EmployeeCode = entity.EmployeeCode.Trim().ToUpperInvariant();
        entity.FullName = entity.FullName.Trim();
        entity.Phone = Normalize.Phone(entity.Phone);
    }

    protected override void ValidateBusinessRules(Employee entity)
    {
        if (string.IsNullOrWhiteSpace(entity.EmployeeCode)
            || string.IsNullOrWhiteSpace(entity.FullName)
            || string.IsNullOrWhiteSpace(entity.Phone))
        {
            throw new InvalidOperationException("Mã, họ tên và số điện thoại nhân viên là bắt buộc.");
        }
    }
}

[Route("api/v1/vehicle-brands")]
public sealed class VehicleBrandsController(IMongoRepository<VehicleBrand> repository)
    : CrudController<VehicleBrand>(repository)
{
    protected override void Prepare(VehicleBrand entity)
    {
        entity.Code = entity.Code.Trim().ToUpperInvariant();
        entity.Name = entity.Name.Trim();
    }
}

[Route("api/v1/vehicle-models")]
public sealed class VehicleModelsController(IMongoRepository<VehicleModel> repository)
    : CrudController<VehicleModel>(repository)
{
    protected override FilterDefinition<VehicleModel> BuildSearchFilter(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return Builders<VehicleModel>.Filter.Empty;
        }

        var regex = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(search.Trim()), "i");
        return Builders<VehicleModel>.Filter.Or(
            Builders<VehicleModel>.Filter.Regex(x => x.Code, regex),
            Builders<VehicleModel>.Filter.Regex(x => x.Name, regex));
    }

    protected override void Prepare(VehicleModel entity)
    {
        entity.Code = entity.Code.Trim().ToUpperInvariant();
        entity.Name = entity.Name.Trim();
    }

    protected override void ValidateBusinessRules(VehicleModel entity)
    {
        if (string.IsNullOrWhiteSpace(entity.BrandId))
        {
            throw new InvalidOperationException("Dòng xe phải thuộc một hãng xe.");
        }
    }
}

[Route("api/v1/part-brands")]
public sealed class PartBrandsController(IMongoRepository<PartBrand> repository)
    : CrudController<PartBrand>(repository)
{
    protected override void Prepare(PartBrand entity)
    {
        entity.Code = entity.Code.Trim().ToUpperInvariant();
        entity.Name = entity.Name.Trim();
    }
}

[Route("api/v1/parts")]
public sealed class PartsController(
    IMongoRepository<Part> repository,
    MongoDbContext context)
    : CrudController<Part>(repository)
{
    protected override FilterDefinition<Part> BuildSearchFilter(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return Builders<Part>.Filter.Empty;
        }

        var regex = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(search.Trim()), "i");
        return Builders<Part>.Filter.Or(
            Builders<Part>.Filter.Regex(x => x.Code, regex),
            Builders<Part>.Filter.Regex(x => x.Name, regex),
            Builders<Part>.Filter.Regex(x => x.Barcode, regex),
            Builders<Part>.Filter.Regex("specifications.value", regex));
    }

    public override async Task<IActionResult> GetPage(
        string? search,
        int page = 1,
        int pageSize = 20,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildSearchFilter(search);
        var categoryId = Request.Query["categoryId"].ToString();
        var supplierId = Request.Query["supplierId"].ToString();
        var warehouseLocationId = Request.Query["warehouseLocationId"].ToString();
        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            filter &= Builders<Part>.Filter.Eq(x => x.PartCategoryId, categoryId);
        }
        if (!string.IsNullOrWhiteSpace(supplierId))
        {
            filter &= Builders<Part>.Filter.AnyEq(x => x.SupplierIds, supplierId);
        }
        if (!string.IsNullOrWhiteSpace(warehouseLocationId))
        {
            filter &= Builders<Part>.Filter.Or(
                Builders<Part>.Filter.Eq(x => x.WarehouseLocationId, warehouseLocationId),
                Builders<Part>.Filter.AnyEq(x => x.WarehouseLocationIds, warehouseLocationId),
                Builders<Part>.Filter.ElemMatch(x => x.WarehouseStocks,
                    x => x.WarehouseLocationId == warehouseLocationId));
        }

        var result = await Repository.GetPageAsync(
            filter,
            page,
            pageSize,
            cancellationToken: cancellationToken,
            includeDeleted: includeDeleted);
        return Ok(ApiEnvelope.Ok(result));
    }

    public override async Task<IActionResult> Create(Part entity, CancellationToken cancellationToken)
    {
        await ValidateAndNormalizeSpecificationsAsync(entity, cancellationToken);
        entity.ImportPrice = 0;
        entity.StockPrice = 0;
        entity.QuantityOnHand = 0;
        entity.SupplierIds = [];
        entity.WarehouseStocks = entity.WarehouseLocationIds
            .Select(x => new PartWarehouseStock { WarehouseLocationId = x })
            .ToList();
        return await base.Create(entity, cancellationToken);
    }

    public override async Task<IActionResult> Update(string id, Part entity, CancellationToken cancellationToken)
    {
        var current = await Repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phụ tùng.");
        await ValidateAndNormalizeSpecificationsAsync(entity, cancellationToken);
        entity.ImportPrice = current.ImportPrice;
        entity.StockPrice = current.StockPrice;
        entity.QuantityOnHand = current.QuantityOnHand;
        entity.SupplierIds = current.SupplierIds;
        var currentStocks = CurrentWarehouseStocks(current);
        var removedWithStock = currentStocks.Any(x => x.QuantityOnHand > 0
            && !entity.WarehouseLocationIds.Contains(x.WarehouseLocationId));
        if (removedWithStock)
        {
            throw new InvalidOperationException(
                "Không thể bỏ vị trí vẫn còn tồn kho. Hãy chuyển hết hàng sang ngăn khác trước.");
        }
        entity.WarehouseStocks = entity.WarehouseLocationIds.Select(locationId =>
            currentStocks.FirstOrDefault(x => x.WarehouseLocationId == locationId)
            ?? new PartWarehouseStock { WarehouseLocationId = locationId }).ToList();
        return await base.Update(id, entity, cancellationToken);
    }

    protected override void Prepare(Part entity)
    {
        entity.Code = entity.Code.Trim().ToUpperInvariant();
        entity.Name = entity.Name.Trim();
        entity.Unit = entity.Unit.Trim();
        entity.ReplacementIntervalKm = entity.ReplacementIntervalKm is > 0
            ? entity.ReplacementIntervalKm
            : null;
        entity.ReplacementIntervalMonths = entity.ReplacementIntervalMonths is > 0
            ? entity.ReplacementIntervalMonths
            : null;
        foreach (var specification in entity.Specifications)
        {
            specification.Code = specification.Code.Trim().ToUpperInvariant();
            specification.Name = specification.Name.Trim();
            specification.Value = specification.Value.Trim();
            specification.Unit = specification.Unit?.Trim();
        }
    }

    protected override void ValidateBusinessRules(Part entity)
    {
        if (entity.ImportPrice < 0 || entity.StockPrice < 0 || entity.SalePrice < 0
            || entity.QuantityOnHand < 0 || entity.MinQuantity < 0
            || entity.ReplacementIntervalKm < 0 || entity.ReplacementIntervalMonths < 0)
        {
            throw new InvalidOperationException("Giá và số lượng phụ tùng không được âm.");
        }
        if (string.IsNullOrWhiteSpace(entity.PartCategoryId))
        {
            throw new InvalidOperationException("Phụ tùng phải thuộc một danh mục.");
        }
        if (string.IsNullOrWhiteSpace(entity.Name))
        {
            throw new InvalidOperationException("Tên phụ tùng là bắt buộc.");
        }
        if (string.IsNullOrWhiteSpace(entity.Unit))
        {
            throw new InvalidOperationException("Đơn vị phụ tùng là bắt buộc.");
        }
    }

    private async Task ValidateAndNormalizeSpecificationsAsync(
        Part entity,
        CancellationToken cancellationToken)
    {
        entity.WarehouseLocationIds = (entity.WarehouseLocationIds ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
        if (!string.IsNullOrWhiteSpace(entity.WarehouseLocationId)
            && !entity.WarehouseLocationIds.Contains(entity.WarehouseLocationId))
        {
            entity.WarehouseLocationIds.Insert(0, entity.WarehouseLocationId);
        }
        if (string.IsNullOrWhiteSpace(entity.WarehouseLocationId))
        {
            entity.WarehouseLocationId = entity.WarehouseLocationIds.FirstOrDefault();
        }
        if (entity.WarehouseLocationIds.Count > 0)
        {
            var locationCount = await context.Collection<WarehouseLocation>()
                .CountDocumentsAsync(x => entity.WarehouseLocationIds.Contains(x.Id)
                    && !x.IsDeleted && x.IsActive, cancellationToken: cancellationToken);
            if (locationCount != entity.WarehouseLocationIds.Count)
            {
                throw new InvalidOperationException("Một hoặc nhiều vị trí kho không tồn tại hoặc đã ngừng sử dụng.");
            }
        }

        var category = await context.Collection<PartCategory>()
            .Find(x => x.Id == entity.PartCategoryId && !x.IsDeleted && x.IsActive)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Danh mục phụ tùng không tồn tại hoặc đã ngừng hoạt động.");
        var submitted = entity.Specifications
            .GroupBy(x => x.Code.Trim().ToUpperInvariant())
            .ToDictionary(x => x.Key, x => x.First().Value?.Trim() ?? string.Empty);
        var normalized = new List<PartSpecificationValue>();
        foreach (var definition in category.SpecificationDefinitions)
        {
            submitted.TryGetValue(definition.Code, out var value);
            if (definition.IsRequired && string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Thông số '{definition.Name}' là bắt buộc.");
            }
            if (!string.IsNullOrWhiteSpace(value))
            {
                value = NormalizeSpecificationValue(definition, value);
                normalized.Add(new PartSpecificationValue
                {
                    Code = definition.Code,
                    Name = definition.Name,
                    Unit = definition.Unit,
                    Value = value
                });
            }
        }
        entity.Specifications = normalized;
    }

    private static List<PartWarehouseStock> CurrentWarehouseStocks(Part part)
    {
        if (part.WarehouseStocks.Count > 0) return part.WarehouseStocks;
        if (!string.IsNullOrWhiteSpace(part.WarehouseLocationId) && part.QuantityOnHand > 0)
        {
            return
            [
                new PartWarehouseStock
                {
                    WarehouseLocationId = part.WarehouseLocationId,
                    QuantityOnHand = part.QuantityOnHand
                }
            ];
        }
        return [];
    }

    private static string NormalizeSpecificationValue(
        PartSpecificationDefinition definition,
        string value)
    {
        if (definition.DataType == PartSpecificationDataType.Number)
        {
            if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out number))
            {
                throw new InvalidOperationException($"Thông số '{definition.Name}' phải là số.");
            }
            return number.ToString(CultureInfo.InvariantCulture);
        }

        if (definition.DataType == PartSpecificationDataType.Boolean)
        {
            return value.Trim().ToLowerInvariant() switch
            {
                "true" or "1" or "yes" or "có" => "true",
                "false" or "0" or "no" or "không" => "false",
                _ => throw new InvalidOperationException($"Thông số '{definition.Name}' phải là Có hoặc Không.")
            };
        }

        if (definition.DataType == PartSpecificationDataType.Selection)
        {
            var option = definition.Options.FirstOrDefault(x =>
                string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
            return option ?? throw new InvalidOperationException(
                $"Giá trị của thông số '{definition.Name}' không nằm trong danh sách lựa chọn.");
        }

        return value.Trim();
    }
}

[Route("api/v1/part-categories")]
public sealed class PartCategoriesController(IMongoRepository<PartCategory> repository)
    : CrudController<PartCategory>(repository)
{
    protected override FilterDefinition<PartCategory> BuildSearchFilter(string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return Builders<PartCategory>.Filter.Empty;
        var regex = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(search.Trim()), "i");
        return Builders<PartCategory>.Filter.Or(
            Builders<PartCategory>.Filter.Regex(x => x.Code, regex),
            Builders<PartCategory>.Filter.Regex(x => x.Name, regex));
    }

    protected override void Prepare(PartCategory entity)
    {
        entity.Code = entity.Code.Trim().ToUpperInvariant();
        entity.Name = entity.Name.Trim();
        entity.Description = entity.Description?.Trim();
        var usedCodes = entity.SpecificationDefinitions
            .Where(x => !string.IsNullOrWhiteSpace(x.Code))
            .Select(x => x.Code.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextSpecificationNumber = 1;
        foreach (var definition in entity.SpecificationDefinitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Code))
            {
                string generated;
                do
                {
                    generated = $"TSKT-{nextSpecificationNumber++:000}";
                } while (usedCodes.Contains(generated));
                definition.Code = generated;
                usedCodes.Add(generated);
            }
            definition.Code = definition.Code.Trim().ToUpperInvariant();
            definition.Name = definition.Name.Trim();
            definition.Unit = definition.Unit?.Trim();
            definition.Options = definition.DataType == PartSpecificationDataType.Selection
                ? (definition.Options ?? [])
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : [];
            if (definition.DataType == PartSpecificationDataType.Boolean)
            {
                definition.Unit = null;
            }
        }
    }

    protected override void ValidateBusinessRules(PartCategory entity)
    {
        if (entity.SpecificationDefinitions.Any(x => string.IsNullOrWhiteSpace(x.Code) || string.IsNullOrWhiteSpace(x.Name)))
        {
            throw new InvalidOperationException("Mỗi thông số kỹ thuật phải có mã và tên.");
        }
        if (entity.SpecificationDefinitions.GroupBy(x => x.Code).Any(x => x.Count() > 1))
        {
            throw new InvalidOperationException("Mã thông số kỹ thuật không được trùng trong cùng danh mục.");
        }
        if (entity.SpecificationDefinitions.Any(x => !Enum.IsDefined(x.DataType)))
        {
            throw new InvalidOperationException("Loại thông số kỹ thuật không hợp lệ.");
        }
        if (entity.SpecificationDefinitions.Any(x => x.DataType == PartSpecificationDataType.Selection && (x.Options?.Count ?? 0) < 2))
        {
            throw new InvalidOperationException("Thông số dạng danh sách lựa chọn phải có ít nhất hai phương án.");
        }
    }
}

[Route("api/v1/warehouse-locations")]
public sealed class WarehouseLocationsController(
    IMongoRepository<WarehouseLocation> repository,
    MongoDbContext context)
    : CrudController<WarehouseLocation>(repository)
{
    protected override FilterDefinition<WarehouseLocation> BuildSearchFilter(string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return Builders<WarehouseLocation>.Filter.Empty;
        var regex = new BsonRegularExpression(
            System.Text.RegularExpressions.Regex.Escape(search.Trim()), "i");
        return Builders<WarehouseLocation>.Filter.Or(
            Builders<WarehouseLocation>.Filter.Regex(x => x.Code, regex),
            Builders<WarehouseLocation>.Filter.Regex(x => x.Name, regex));
    }

    protected override void Prepare(WarehouseLocation entity)
    {
        entity.Code = $"K{entity.Rack}-T{entity.Level}-N{entity.Bin}";
        entity.Name = string.IsNullOrWhiteSpace(entity.Name)
            ? $"Kệ {entity.Rack} · Tầng {entity.Level} · Ngăn {entity.Bin}"
            : entity.Name.Trim();
        entity.Description = entity.Description?.Trim();
    }

    protected override void ValidateBusinessRules(WarehouseLocation entity)
    {
        if (entity.Rack <= 0 || entity.Level <= 0 || entity.Bin <= 0)
        {
            throw new InvalidOperationException("Kệ, tầng và ngăn phải là số lớn hơn 0.");
        }
    }

    public override async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var isInUse = await context.Collection<Part>()
            .Find(x => !x.IsDeleted && (x.WarehouseLocationId == id
                || x.WarehouseLocationIds.Contains(id)
                || x.WarehouseStocks.Any(stock => stock.WarehouseLocationId == id)))
            .AnyAsync(cancellationToken);
        if (isInUse)
        {
            throw new InvalidOperationException(
                "Vị trí đang được gán cho phụ tùng. Hãy chuyển phụ tùng sang vị trí khác trước khi xóa.");
        }
        return await base.Delete(id, cancellationToken);
    }
}

[Route("api/v1/service-categories")]
public sealed class ServiceCategoriesController(IMongoRepository<ServiceCategory> repository)
    : CrudController<ServiceCategory>(repository)
{
    protected override FilterDefinition<ServiceCategory> BuildSearchFilter(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return Builders<ServiceCategory>.Filter.Empty;
        }

        var regex = new BsonRegularExpression(
            System.Text.RegularExpressions.Regex.Escape(search.Trim()), "i");
        return Builders<ServiceCategory>.Filter.Or(
            Builders<ServiceCategory>.Filter.Regex(x => x.Code, regex),
            Builders<ServiceCategory>.Filter.Regex(x => x.Name, regex),
            Builders<ServiceCategory>.Filter.Regex(x => x.Description, regex));
    }

    [Authorize(Roles = SecurityRoles.Administrators)]
    public override Task<IActionResult> Create(ServiceCategory entity, CancellationToken cancellationToken) =>
        base.Create(entity, cancellationToken);

    [Authorize(Roles = SecurityRoles.Administrators)]
    public override Task<IActionResult> Update(string id, ServiceCategory entity, CancellationToken cancellationToken) =>
        base.Update(id, entity, cancellationToken);

    [Authorize(Roles = SecurityRoles.Administrators)]
    public override Task<IActionResult> Delete(string id, CancellationToken cancellationToken) =>
        base.Delete(id, cancellationToken);

    protected override void Prepare(ServiceCategory entity)
    {
        entity.Code = entity.Code.Trim().ToUpperInvariant();
        entity.Name = entity.Name.Trim();
        entity.Description = entity.Description?.Trim();
    }

    protected override void ValidateBusinessRules(ServiceCategory entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code) || string.IsNullOrWhiteSpace(entity.Name))
        {
            throw new InvalidOperationException("Mã và tên dịch vụ là bắt buộc.");
        }

        if (entity.DefaultPrice < 0)
        {
            throw new InvalidOperationException("Giá mặc định không được âm.");
        }
    }
}

[Route("api/v1/suppliers")]
public sealed class SuppliersController(
    IMongoRepository<Supplier> repository,
    MongoDbContext context) : CrudController<Supplier>(repository)
{
    protected override FilterDefinition<Supplier> BuildSearchFilter(string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return Builders<Supplier>.Filter.Empty;
        var regex = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(search.Trim()), "i");
        return Builders<Supplier>.Filter.Or(
            Builders<Supplier>.Filter.Regex(x => x.Code, regex),
            Builders<Supplier>.Filter.Regex(x => x.Name, regex),
            Builders<Supplier>.Filter.Regex(x => x.Phone, regex),
            Builders<Supplier>.Filter.Regex(x => x.TaxCode, regex));
    }

    [HttpGet("{id}/stock")]
    public async Task<IActionResult> Stock(
        string id,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var supplier = await context.Collection<Supplier>()
            .Find(x => x.Id == id && (includeDeleted || !x.IsDeleted))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy nhà cung cấp.");
        var stocks = await context.Collection<SupplierPartStock>()
            .Find(x => x.SupplierId == id && !x.IsDeleted)
            .SortByDescending(x => x.QuantityOnHand)
            .ToListAsync(cancellationToken);
        var partIds = stocks.Select(x => x.PartId).ToList();
        var parts = await context.Collection<Part>()
            .Find(x => partIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var rows = stocks.Select(stock => new
        {
            stock.PartId,
            partCode = parts.FirstOrDefault(x => x.Id == stock.PartId)?.Code ?? "",
            partName = parts.FirstOrDefault(x => x.Id == stock.PartId) is { } part
                ? $"{part.Name}{(part.IsDeleted ? " (đã xóa)" : string.Empty)}"
                : "Phụ tùng không còn tồn tại",
            stock.QuantityOnHand,
            stock.LastUnitCost,
            stock.LastReceiptAt
        });
        return Ok(ApiEnvelope.Ok(new
        {
            supplier.Id,
            supplier.Name,
            totalQuantityOnHand = stocks.Sum(x => x.QuantityOnHand),
            items = rows
        }));
    }

    protected override void Prepare(Supplier entity)
    {
        entity.Code = entity.Code.Trim().ToUpperInvariant();
        entity.Name = entity.Name.Trim();
        entity.Phone = Normalize.Phone(entity.Phone);
        entity.TaxCode = string.IsNullOrWhiteSpace(entity.TaxCode) ? null : entity.TaxCode.Trim();
        entity.Address = entity.Address?.Trim();
        entity.Notes = entity.Notes?.Trim();
    }

    protected override void ValidateBusinessRules(Supplier entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Name) || string.IsNullOrWhiteSpace(entity.Phone))
        {
            throw new InvalidOperationException("Tên và số điện thoại nhà cung cấp là bắt buộc.");
        }
    }
}

[Route("api/v1/cash-transactions")]
public sealed class CashTransactionsController(
    IMongoRepository<CashTransaction> repository,
    ExpenseService expenses)
    : CrudController<CashTransaction>(repository)
{
    public override async Task<IActionResult> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<CashTransaction>>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new BsonRegularExpression(
                System.Text.RegularExpressions.Regex.Escape(search.Trim()),
                "i");
            filters.Add(Builders<CashTransaction>.Filter.Or(
                Builders<CashTransaction>.Filter.Regex(x => x.Code, regex),
                Builders<CashTransaction>.Filter.Regex(x => x.Description, regex),
                Builders<CashTransaction>.Filter.Regex(x => x.Category, regex),
                Builders<CashTransaction>.Filter.Regex("purchaseItems.partName", regex)));
        }

        if (Enum.TryParse<CashTransactionType>(Request.Query["type"], true, out var type))
        {
            filters.Add(Builders<CashTransaction>.Filter.Eq(x => x.Type, type));
        }
        var status = Request.Query["status"].ToString();
        if (!string.IsNullOrWhiteSpace(status))
        {
            filters.Add(Builders<CashTransaction>.Filter.Eq(x => x.Status, status));
        }
        var cashCategoryId = Request.Query["cashCategoryId"].ToString();
        if (!string.IsNullOrWhiteSpace(cashCategoryId))
        {
            filters.Add(Builders<CashTransaction>.Filter.Eq(x => x.CashCategoryId, cashCategoryId));
        }
        var supplierId = Request.Query["supplierId"].ToString();
        if (!string.IsNullOrWhiteSpace(supplierId))
        {
            filters.Add(Builders<CashTransaction>.Filter.Eq(x => x.SupplierId, supplierId));
        }
        if (DateTime.TryParse(Request.Query["from"], out var from))
        {
            filters.Add(Builders<CashTransaction>.Filter.Gte(
                x => x.TransactionDate,
                DateTime.SpecifyKind(from.Date, DateTimeKind.Utc)));
        }
        if (DateTime.TryParse(Request.Query["to"], out var to))
        {
            filters.Add(Builders<CashTransaction>.Filter.Lt(
                x => x.TransactionDate,
                DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc)));
        }

        var filter = filters.Count == 0
            ? Builders<CashTransaction>.Filter.Empty
            : Builders<CashTransaction>.Filter.And(filters);
        var result = await Repository.GetPageAsync(
            filter,
            page,
            pageSize,
            Builders<CashTransaction>.Sort.Descending(x => x.TransactionDate),
            cancellationToken,
            includeDeleted);
        return Ok(ApiEnvelope.Ok(result));
    }

    public override async Task<IActionResult> Create(
        CashTransaction entity,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        var created = await expenses.CreateAsync(entity, userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ApiEnvelope.Ok(created));
    }

    [HttpPost("{id}/confirm")]
    [Authorize(Roles = SecurityRoles.Finance)]
    public async Task<IActionResult> Confirm(string id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        var confirmed = await expenses.ConfirmAsync(id, userId, cancellationToken);
        return Ok(ApiEnvelope.Ok(confirmed, "Đã xác nhận phiếu chi và hoàn tất cập nhật tồn kho."));
    }

    public override async Task<IActionResult> Update(
        string id,
        CashTransaction entity,
        CancellationToken cancellationToken)
    {
        var current = await Repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu thu/chi.");
        if (current.Purpose == "PartsPurchase" || (current.Type == CashTransactionType.Expense && current.Status != "New"))
        {
            throw new InvalidOperationException("Phiếu chi đã kết thúc hoặc có nghiệp vụ nhập kho nên không thể sửa trực tiếp.");
        }
        entity.Status = current.Status;
        entity.Type = current.Type;
        entity.Purpose = current.Purpose;
        entity.ConfirmedAt = current.ConfirmedAt;
        entity.ConfirmedBy = current.ConfirmedBy;
        await expenses.ValidateDraftAsync(entity, cancellationToken);
        return await base.Update(id, entity, cancellationToken);
    }

    public override async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var current = await Repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu thu/chi.");
        if (current.Purpose == "PartsPurchase" || (current.Type == CashTransactionType.Expense && current.Status != "New"))
        {
            throw new InvalidOperationException("Phiếu chi đã kết thúc hoặc có nghiệp vụ nhập kho nên không thể xóa trực tiếp.");
        }
        return await base.Delete(id, cancellationToken);
    }

    protected override void ValidateBusinessRules(CashTransaction entity)
    {
        if (entity.Amount <= 0)
        {
            throw new InvalidOperationException("Số tiền thu/chi phải lớn hơn 0.");
        }
    }
}

[Route("api/v1/cash-categories")]
public sealed class CashCategoriesController(IMongoRepository<CashCategory> repository)
    : CrudController<CashCategory>(repository)
{
    protected override FilterDefinition<CashCategory> BuildSearchFilter(string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return Builders<CashCategory>.Filter.Empty;
        var regex = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(search.Trim()), "i");
        return Builders<CashCategory>.Filter.Or(
            Builders<CashCategory>.Filter.Regex(x => x.Code, regex),
            Builders<CashCategory>.Filter.Regex(x => x.Name, regex));
    }

    protected override void Prepare(CashCategory entity)
    {
        entity.Code = entity.Code.Trim().ToUpperInvariant();
        entity.Name = entity.Name.Trim();
        entity.Description = entity.Description?.Trim();
    }

    protected override void ValidateBusinessRules(CashCategory entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code) || string.IsNullOrWhiteSpace(entity.Name))
        {
            throw new InvalidOperationException("Mã và tên danh mục thu chi là bắt buộc.");
        }
    }
}
