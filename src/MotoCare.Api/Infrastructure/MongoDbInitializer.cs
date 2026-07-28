using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MotoCare.Api.Domain;

namespace MotoCare.Api.Infrastructure;

public sealed class MongoDbInitializer(
    MongoDbContext context,
    IOptions<MongoOptions> options,
    IConfiguration configuration,
    ILogger<MongoDbInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.InitializeOnStartup)
        {
            return;
        }

        try
        {
            await context.Database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cancellationToken);
            await CreateIndexes(cancellationToken);
            await SeedAdmin(cancellationToken);
            await SeedLoyalty(cancellationToken);
            logger.LogInformation("MongoDB initialization completed.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "MongoDB initialization skipped because the database is unavailable. The API can start, but data endpoints require MongoDB.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task CreateIndexes(CancellationToken cancellationToken)
    {
        await context.Collection<AppUser>().Indexes.CreateOneAsync(
            new CreateIndexModel<AppUser>(
                Builders<AppUser>.IndexKeys.Ascending(x => x.NormalizedUsername),
                new CreateIndexOptions { Unique = true, Name = "ux_users_normalized_username" }),
            cancellationToken: cancellationToken);

        await context.Collection<Customer>().Indexes.CreateManyAsync(
            [
                new CreateIndexModel<Customer>(
                    Builders<Customer>.IndexKeys.Ascending(x => x.Code),
                    new CreateIndexOptions { Unique = true, Name = "ux_customers_code" }),
                new CreateIndexModel<Customer>(
                    Builders<Customer>.IndexKeys.Ascending(x => x.NormalizedPhone),
                    new CreateIndexOptions { Name = "ix_customers_phone" }),
                new CreateIndexModel<Customer>(
                    Builders<Customer>.IndexKeys.Text(x => x.FullName),
                    new CreateIndexOptions { Name = "tx_customers_name" })
            ],
            cancellationToken);

        await CreateUniqueIndex(context.Collection<Employee>(), x => x.EmployeeCode, "ux_employees_code", cancellationToken);
        await CreateUniqueIndex(context.Collection<VehicleBrand>(), x => x.Code, "ux_vehicle_brands_code", cancellationToken);
        await context.Collection<VehicleModel>().Indexes.CreateOneAsync(
            new CreateIndexModel<VehicleModel>(
                Builders<VehicleModel>.IndexKeys
                    .Ascending(x => x.BrandId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions { Unique = true, Name = "ux_vehicle_models_brand_code" }),
            cancellationToken: cancellationToken);
        await CreateUniqueIndex(context.Collection<Vehicle>(), x => x.NormalizedLicensePlate, "ux_vehicles_plate", cancellationToken);
        await CreateUniqueIndex(context.Collection<PartBrand>(), x => x.Code, "ux_part_brands_code", cancellationToken);
        await CreateUniqueIndex(context.Collection<Part>(), x => x.Code, "ux_parts_code", cancellationToken);
        await CreateUniqueIndex(context.Collection<RepairOrder>(), x => x.Code, "ux_repair_orders_code", cancellationToken);
        await CreateUniqueIndex(context.Collection<Invoice>(), x => x.Code, "ux_invoices_code", cancellationToken);
        await CreateUniqueIndex(context.Collection<LoyaltyTier>(), x => x.Code, "ux_loyalty_tiers_code", cancellationToken);
        await CreateUniqueIndex(context.Collection<LoyaltyAccount>(), x => x.CustomerId, "ux_loyalty_accounts_customer", cancellationToken);
        await CreateUniqueIndex(context.Collection<LoyaltyAccount>(), x => x.MemberCode, "ux_loyalty_accounts_member", cancellationToken);
        await CreateUniqueIndex(context.Collection<LoyaltyTransaction>(), x => x.IdempotencyKey, "ux_loyalty_transactions_idempotency", cancellationToken);
        await CreateUniqueIndex(context.Collection<Sequence>(), x => x.Name, "ux_sequences_name", cancellationToken);

        await context.Collection<Notification>().Indexes.CreateOneAsync(
            new CreateIndexModel<Notification>(
                Builders<Notification>.IndexKeys
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.IsRead)
                    .Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_notifications_user_read_created" }),
            cancellationToken: cancellationToken);

        await context.Collection<RepairOrder>().Indexes.CreateManyAsync(
            [
                new CreateIndexModel<RepairOrder>(
                    Builders<RepairOrder>.IndexKeys
                        .Ascending(x => x.Status)
                        .Descending(x => x.ReceivedAt),
                    new CreateIndexOptions { Name = "ix_repair_orders_status_received" }),
                new CreateIndexModel<RepairOrder>(
                    Builders<RepairOrder>.IndexKeys
                        .Ascending(x => x.CustomerId)
                        .Descending(x => x.ReceivedAt),
                    new CreateIndexOptions { Name = "ix_repair_orders_customer_received" }),
                new CreateIndexModel<RepairOrder>(
                    Builders<RepairOrder>.IndexKeys
                        .Ascending(x => x.VehicleId)
                        .Descending(x => x.ReceivedAt),
                    new CreateIndexOptions { Name = "ix_repair_orders_vehicle_received" })
            ],
            cancellationToken);
        await context.Collection<Invoice>().Indexes.CreateManyAsync(
            [
                new CreateIndexModel<Invoice>(
                    Builders<Invoice>.IndexKeys
                        .Ascending(x => x.PaymentStatus)
                        .Descending(x => x.IssueDate),
                    new CreateIndexOptions { Name = "ix_invoices_status_date" }),
                new CreateIndexModel<Invoice>(
                    Builders<Invoice>.IndexKeys
                        .Ascending(x => x.CustomerId)
                        .Descending(x => x.IssueDate),
                    new CreateIndexOptions { Name = "ix_invoices_customer_date" })
            ],
            cancellationToken);
    }

    private static Task CreateUniqueIndex<T>(
        IMongoCollection<T> collection,
        System.Linq.Expressions.Expression<Func<T, object>> field,
        string name,
        CancellationToken cancellationToken)
    {
        return collection.Indexes.CreateOneAsync(
            new CreateIndexModel<T>(
                Builders<T>.IndexKeys.Ascending(field),
                new CreateIndexOptions { Unique = true, Name = name }),
            cancellationToken: cancellationToken);
    }

    private async Task SeedAdmin(CancellationToken cancellationToken)
    {
        var username = configuration["SeedAdmin:Username"]?.Trim() ?? "admin";
        var normalized = username.ToUpperInvariant();
        var users = context.Collection<AppUser>();
        if (await users.Find(x => x.NormalizedUsername == normalized).AnyAsync(cancellationToken))
        {
            return;
        }

        var user = new AppUser
        {
            Username = username,
            NormalizedUsername = normalized,
            FullName = configuration["SeedAdmin:FullName"] ?? "Quản trị hệ thống",
            Roles = [SecurityRoles.Administrator]
        };
        var hasher = new PasswordHasher<AppUser>();
        user.PasswordHash = hasher.HashPassword(
            user,
            configuration["SeedAdmin:Password"] ?? "Admin@123456");
        await users.InsertOneAsync(user, cancellationToken: cancellationToken);
        logger.LogWarning("Seeded initial admin account '{Username}'. Change its password immediately.", username);
    }

    private async Task SeedLoyalty(CancellationToken cancellationToken)
    {
        var tiers = context.Collection<LoyaltyTier>();
        if (!await tiers.Find(Builders<LoyaltyTier>.Filter.Empty).AnyAsync(cancellationToken))
        {
            await tiers.InsertManyAsync(
                [
                    new LoyaltyTier
                    {
                        Code = "MEMBER",
                        Name = "Thành viên",
                        Rank = 1,
                        MinEligibleSpend = 0,
                        EarnRate = 1,
                        RedemptionValue = 1_000
                    },
                    new LoyaltyTier
                    {
                        Code = "SILVER",
                        Name = "Bạc",
                        Rank = 2,
                        MinEligibleSpend = 10_000_000,
                        EarnRate = 1.1m,
                        RedemptionValue = 1_000
                    },
                    new LoyaltyTier
                    {
                        Code = "GOLD",
                        Name = "Vàng",
                        Rank = 3,
                        MinEligibleSpend = 30_000_000,
                        EarnRate = 1.25m,
                        RedemptionValue = 1_000
                    }
                ],
                cancellationToken: cancellationToken);
        }

        var rules = context.Collection<LoyaltyRule>();
        if (!await rules.Find(x => x.IsActive).AnyAsync(cancellationToken))
        {
            await rules.InsertOneAsync(
                new LoyaltyRule { Name = "Chính sách mặc định" },
                cancellationToken: cancellationToken);
        }
    }
}
