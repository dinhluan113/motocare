using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using MotoCare.Api.Contracts;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Controllers;

[ApiController]
[Route("api/v1/vehicles")]
[Authorize]
public sealed class VehiclesController(
    IMongoRepository<Vehicle> repository,
    MongoDbContext context) : ControllerBase
{
    [HttpGet("lookup-by-license-plate")]
    public async Task<IActionResult> LookupByLicensePlate(
        [FromQuery] string licensePlate,
        CancellationToken cancellationToken)
    {
        var normalizedPlate = Normalize.LicensePlate(licensePlate);
        if (string.IsNullOrEmpty(normalizedPlate))
        {
            return Ok(ApiEnvelope.Ok(new
            {
                found = false,
                vehicle = (Vehicle?)null,
                customer = (Customer?)null
            }));
        }

        var vehicle = await context.Collection<Vehicle>()
            .Find(x => x.NormalizedLicensePlate == normalizedPlate && !x.IsDeleted && x.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
        var customer = vehicle is null
            ? null
            : await context.Collection<Customer>()
                .Find(x => x.Id == vehicle.CustomerId && !x.IsDeleted && x.IsActive)
                .FirstOrDefaultAsync(cancellationToken);
        return Ok(ApiEnvelope.Ok(new
        {
            found = vehicle is not null && customer is not null,
            vehicle,
            customer
        }));
    }

    [HttpGet]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? customerId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<Vehicle>>();
        if (!string.IsNullOrWhiteSpace(customerId))
        {
            filters.Add(Builders<Vehicle>.Filter.Eq(x => x.CustomerId, customerId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new BsonRegularExpression(
                System.Text.RegularExpressions.Regex.Escape(Normalize.LicensePlate(search)),
                "i");
            filters.Add(Builders<Vehicle>.Filter.Regex(x => x.NormalizedLicensePlate, regex));
        }

        var filter = filters.Count == 0
            ? Builders<Vehicle>.Filter.Empty
            : Builders<Vehicle>.Filter.And(filters);
        return Ok(ApiEnvelope.Ok(await repository.GetPageAsync(
            filter,
            page,
            pageSize,
            cancellationToken: cancellationToken,
            includeDeleted: includeDeleted)));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(
        string id,
        [FromQuery] bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var vehicle = await repository.GetByIdAsync(id, cancellationToken, includeDeleted);
        return vehicle is null
            ? NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy xe."))
            : Ok(ApiEnvelope.Ok(vehicle));
    }

    [HttpPost]
    [Authorize(Roles = SecurityRoles.Operations)]
    public async Task<IActionResult> Create(
        UpsertVehicleRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateReferences(request, cancellationToken);
        var vehicle = new Vehicle();
        Apply(vehicle, request);
        await repository.InsertAsync(vehicle, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = vehicle.Id }, ApiEnvelope.Ok(vehicle));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = SecurityRoles.Operations)]
    public async Task<IActionResult> Update(
        string id,
        UpsertVehicleRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateReferences(request, cancellationToken);
        var vehicle = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy xe.");
        Apply(vehicle, request);
        await repository.ReplaceAsync(vehicle, cancellationToken);
        return Ok(ApiEnvelope.Ok(vehicle, "Đã cập nhật xe."));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = SecurityRoles.Management)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        return await repository.SoftDeleteAsync(id, cancellationToken)
            ? Ok(ApiEnvelope.Ok(new { id, deleted = true }))
            : NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy xe."));
    }

    private async Task ValidateReferences(
        UpsertVehicleRequest request,
        CancellationToken cancellationToken)
    {
        var customerExists = await context.Collection<Customer>()
            .Find(x => x.Id == request.CustomerId && !x.IsDeleted)
            .AnyAsync(cancellationToken);
        var modelExists = await context.Collection<VehicleModel>()
            .Find(x => x.Id == request.VehicleModelId && !x.IsDeleted)
            .AnyAsync(cancellationToken);
        if (!customerExists || !modelExists)
        {
            throw new InvalidOperationException("Khách hàng hoặc dòng xe không tồn tại.");
        }
    }

    private static void Apply(Vehicle vehicle, UpsertVehicleRequest request)
    {
        vehicle.CustomerId = request.CustomerId;
        vehicle.VehicleModelId = request.VehicleModelId;
        vehicle.LicensePlate = request.LicensePlate.Trim().ToUpperInvariant();
        vehicle.NormalizedLicensePlate = Normalize.LicensePlate(request.LicensePlate);
        vehicle.FrameNumber = request.FrameNumber?.Trim().ToUpperInvariant();
        vehicle.EngineNumber = request.EngineNumber?.Trim().ToUpperInvariant();
        vehicle.ManufactureYear = request.ManufactureYear;
        vehicle.Color = request.Color?.Trim();
        vehicle.Odometer = request.Odometer;
        vehicle.PurchaseDate = request.PurchaseDate;
        vehicle.Notes = request.Notes?.Trim();
        vehicle.IsActive = request.IsActive;
    }
}
