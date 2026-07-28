using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;

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
public sealed class PartsController(IMongoRepository<Part> repository)
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
            Builders<Part>.Filter.Regex(x => x.Barcode, regex));
    }

    protected override void Prepare(Part entity)
    {
        entity.Code = entity.Code.Trim().ToUpperInvariant();
        entity.Name = entity.Name.Trim();
    }

    protected override void ValidateBusinessRules(Part entity)
    {
        if (entity.ImportPrice < 0 || entity.StockPrice < 0 || entity.SalePrice < 0
            || entity.QuantityOnHand < 0 || entity.MinQuantity < 0)
        {
            throw new InvalidOperationException("Giá và số lượng phụ tùng không được âm.");
        }
    }
}

[Route("api/v1/cash-transactions")]
public sealed class CashTransactionsController(IMongoRepository<CashTransaction> repository)
    : CrudController<CashTransaction>(repository)
{
    protected override void ValidateBusinessRules(CashTransaction entity)
    {
        if (entity.Amount <= 0)
        {
            throw new InvalidOperationException("Số tiền thu/chi phải lớn hơn 0.");
        }
    }
}
