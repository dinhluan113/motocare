using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;
using MotoCare.Api.Services;

namespace MotoCare.Api.Controllers;

[ApiController]
[Authorize]
public abstract class CrudController<T>(IMongoRepository<T> repository) : ControllerBase
    where T : BaseDocument
{
    protected IMongoRepository<T> Repository { get; } = repository;

    [HttpGet]
    public virtual async Task<IActionResult> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var result = await Repository.GetPageAsync(
            BuildSearchFilter(search),
            page,
            pageSize,
            cancellationToken: cancellationToken,
            includeDeleted: includeDeleted);
        return Ok(ApiEnvelope.Ok(result));
    }

    [HttpGet("{id}")]
    public virtual async Task<IActionResult> GetById(
        string id,
        [FromQuery] bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var entity = await Repository.GetByIdAsync(id, cancellationToken, includeDeleted);
        return entity is null
            ? NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy dữ liệu."))
            : Ok(ApiEnvelope.Ok(entity));
    }

    [HttpPost]
    [Authorize(Roles = SecurityRoles.Operations)]
    public virtual async Task<IActionResult> Create(
        T entity,
        CancellationToken cancellationToken)
    {
        await HttpContext.RequestServices
            .GetRequiredService<AutoCodeService>()
            .EnsureAsync(entity, cancellationToken);
        entity.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
        entity.IsDeleted = false;
        Prepare(entity);
        ValidateBusinessRules(entity);
        await Repository.InsertAsync(entity, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ApiEnvelope.Ok(entity));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = SecurityRoles.Operations)]
    public virtual async Task<IActionResult> Update(
        string id,
        T entity,
        CancellationToken cancellationToken)
    {
        var current = await Repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy dữ liệu.");
        entity.Id = id;
        entity.CreatedAt = current.CreatedAt;
        entity.IsDeleted = current.IsDeleted;
        Prepare(entity);
        ValidateBusinessRules(entity);
        await Repository.ReplaceAsync(entity, cancellationToken);
        return Ok(ApiEnvelope.Ok(entity, "Đã cập nhật."));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = SecurityRoles.Management)]
    public virtual async Task<IActionResult> Delete(
        string id,
        CancellationToken cancellationToken)
    {
        var deleted = await Repository.SoftDeleteAsync(id, cancellationToken);
        return deleted
            ? Ok(ApiEnvelope.Ok(new { id, deleted = true }, "Đã xóa."))
            : NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy dữ liệu."));
    }

    protected virtual FilterDefinition<T> BuildSearchFilter(string? search) =>
        Builders<T>.Filter.Empty;

    protected virtual void Prepare(T entity)
    {
    }

    protected virtual void ValidateBusinessRules(T entity)
    {
    }
}
