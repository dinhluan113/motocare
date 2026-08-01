using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using MotoCare.Api.Contracts;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;
using MotoCare.Api.Services;

namespace MotoCare.Api.Controllers;

[ApiController]
[Route("api/v1/customers")]
[Authorize]
public sealed class CustomersController(
    IMongoRepository<Customer> repository,
    MongoDbContext context,
    SequenceService sequences) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<Customer>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var regex = new BsonRegularExpression(
                System.Text.RegularExpressions.Regex.Escape(term),
                "i");
            filter = Builders<Customer>.Filter.Or(
                Builders<Customer>.Filter.Regex(x => x.Code, regex),
                Builders<Customer>.Filter.Regex(x => x.FullName, regex),
                Builders<Customer>.Filter.Regex(x => x.NormalizedPhone, Normalize.Phone(term)));
        }

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
        var customer = await repository.GetByIdAsync(id, cancellationToken, includeDeleted);
        return customer is null
            ? NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy khách hàng."))
            : Ok(ApiEnvelope.Ok(customer));
    }

    [HttpPost]
    [Authorize(Roles = SecurityRoles.Operations)]
    public async Task<IActionResult> Create(
        UpsertCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = Map(request);
        customer.Code = await sequences.NextAsync("customer", "CUS", cancellationToken);
        await repository.InsertAsync(customer, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, ApiEnvelope.Ok(customer));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = SecurityRoles.Operations)]
    public async Task<IActionResult> Update(
        string id,
        UpsertCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy khách hàng.");
        Apply(customer, request);
        await repository.ReplaceAsync(customer, cancellationToken);
        return Ok(ApiEnvelope.Ok(customer, "Đã cập nhật khách hàng."));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = SecurityRoles.Management)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        return await repository.SoftDeleteAsync(id, cancellationToken)
            ? Ok(ApiEnvelope.Ok(new { id, deleted = true }))
            : NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy khách hàng."));
    }

    [HttpGet("{id}/repair-history")]
    public async Task<IActionResult> RepairHistory(
        string id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<RepairOrder>.Filter.And(
            Builders<RepairOrder>.Filter.Eq(x => x.CustomerId, id),
            Builders<RepairOrder>.Filter.Eq(x => x.IsDeleted, false));
        var collection = context.Collection<RepairOrder>();
        var total = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await collection.Find(filter)
            .SortByDescending(x => x.ReceivedAt)
            .Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 200))
            .Limit(Math.Clamp(pageSize, 1, 200))
            .ToListAsync(cancellationToken);
        return Ok(ApiEnvelope.Ok(new PagedResult<RepairOrder>(
            items,
            total,
            Math.Max(page, 1),
            Math.Clamp(pageSize, 1, 200))));
    }

    [HttpGet("{id}/loyalty")]
    public async Task<IActionResult> Loyalty(string id, CancellationToken cancellationToken)
    {
        var account = await context.Collection<LoyaltyAccount>()
            .Find(x => x.CustomerId == id && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            return Ok(ApiEnvelope.Ok(new { account = (object?)null, transactions = Array.Empty<object>() }));
        }

        var transactions = await context.Collection<LoyaltyTransaction>()
            .Find(x => x.LoyaltyAccountId == account.Id && !x.IsDeleted)
            .SortByDescending(x => x.EffectiveAt)
            .Limit(100)
            .ToListAsync(cancellationToken);
        return Ok(ApiEnvelope.Ok(new { account, transactions }));
    }

    private static Customer Map(UpsertCustomerRequest request)
    {
        var customer = new Customer();
        Apply(customer, request);
        return customer;
    }

    private static void Apply(Customer customer, UpsertCustomerRequest request)
    {
        customer.FullName = request.FullName.Trim();
        customer.Phone = request.Phone.Trim();
        customer.NormalizedPhone = Normalize.Phone(request.Phone);
        customer.Email = request.Email?.Trim();
        customer.Address = request.Address?.Trim();
        customer.AddressDetails = request.AddressDetails is null
            ? null
            : new AddressDetails
            {
                AddressLine = request.AddressDetails.AddressLine?.Trim(),
                CountryCode = request.AddressDetails.CountryCode?.Trim().ToUpperInvariant(),
                CountryName = request.AddressDetails.CountryName?.Trim(),
                RegionCode = request.AddressDetails.RegionCode?.Trim(),
                RegionName = request.AddressDetails.RegionName?.Trim(),
                AreaCode = request.AddressDetails.AreaCode?.Trim(),
                AreaName = request.AddressDetails.AreaName?.Trim()
            };
        customer.DateOfBirth = request.DateOfBirth;
        customer.Gender = request.Gender?.Trim();
        customer.TaxCode = request.TaxCode?.Trim();
        customer.Notes = request.Notes?.Trim();
        customer.IsActive = request.IsActive;
    }
}
