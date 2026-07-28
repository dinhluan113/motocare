using MongoDB.Driver;
using MotoCare.Api.Domain;

namespace MotoCare.Api.Infrastructure;

public interface IMongoRepository<T> where T : BaseDocument
{
    IMongoCollection<T> Collection { get; }
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<PagedResult<T>> GetPageAsync(
        FilterDefinition<T> filter,
        int page,
        int pageSize,
        SortDefinition<T>? sort = null,
        CancellationToken cancellationToken = default);
    Task InsertAsync(T entity, CancellationToken cancellationToken = default);
    Task<bool> ReplaceAsync(T entity, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(string id, CancellationToken cancellationToken = default);
}

public sealed class MongoRepository<T>(MongoDbContext context) : IMongoRepository<T>
    where T : BaseDocument
{
    public IMongoCollection<T> Collection { get; } = context.Collection<T>();

    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        await Collection.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<T>> GetPageAsync(
        FilterDefinition<T> filter,
        int page,
        int pageSize,
        SortDefinition<T>? sort = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var activeFilter = Builders<T>.Filter.And(
            Builders<T>.Filter.Eq(x => x.IsDeleted, false),
            filter);
        var total = await Collection.CountDocumentsAsync(activeFilter, cancellationToken: cancellationToken);
        var query = Collection.Find(activeFilter)
            .Sort(sort ?? Builders<T>.Sort.Descending(x => x.UpdatedAt))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize);
        var items = await query.ToListAsync(cancellationToken);
        return new PagedResult<T>(items, total, page, pageSize);
    }

    public Task InsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = entity.CreatedAt;
        return Collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
    }

    public async Task<bool> ReplaceAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        var result = await Collection.ReplaceOneAsync(
            x => x.Id == entity.Id && !x.IsDeleted,
            entity,
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task<bool> SoftDeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var update = Builders<T>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        var result = await Collection.UpdateOneAsync(
            x => x.Id == id && !x.IsDeleted,
            update,
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }
}
