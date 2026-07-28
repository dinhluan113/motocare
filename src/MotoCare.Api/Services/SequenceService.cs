using MongoDB.Driver;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Services;

public sealed class SequenceService(MongoDbContext context)
{
    public async Task<string> NextAsync(
        string name,
        string prefix,
        CancellationToken cancellationToken = default)
    {
        var datePart = DateTime.UtcNow.ToString("yyyyMM");
        var sequenceName = $"{name}:{datePart}";
        var update = Builders<Sequence>.Update
            .Inc(x => x.Value, 1)
            .SetOnInsert(x => x.Name, sequenceName)
            .SetOnInsert(x => x.CreatedAt, DateTime.UtcNow)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        var sequence = await context.Collection<Sequence>().FindOneAndUpdateAsync<Sequence>(
            x => x.Name == sequenceName,
            update,
            new FindOneAndUpdateOptions<Sequence, Sequence>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);

        return $"{prefix}-{datePart}-{sequence.Value:0000}";
    }
}
