using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MotoCare.Api.Domain;
using MotoCare.Api.Services;

namespace MotoCare.Api.Infrastructure;

public sealed class MongoDbInitializer(
    MongoDbContext context,
    IOptions<MongoOptions> options,
    IConfiguration configuration,
    ImageStorageService imageStorage,
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
            await MigrateUserRoles(cancellationToken);
            await SeedAdmin(cancellationToken);
            await SeedLoyalty(cancellationToken);
            await SeedCashCategories(cancellationToken);
            await MigrateEmbeddedImages(cancellationToken);
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

    private async Task MigrateEmbeddedImages(CancellationToken cancellationToken)
    {
        var transactions = context.Collection<CashTransaction>();
        var legacyTransactions = await transactions
            .Find(x => x.AttachmentUrl != null && x.AttachmentUrl.StartsWith("data:image/"))
            .ToListAsync(cancellationToken);
        foreach (var transaction in legacyTransactions)
        {
            var path = await imageStorage.SaveDataUrlAsync(
                transaction.AttachmentUrl!,
                "finance",
                cancellationToken);
            await transactions.UpdateOneAsync(
                x => x.Id == transaction.Id,
                Builders<CashTransaction>.Update
                    .Set(x => x.AttachmentUrl, path)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow),
                cancellationToken: cancellationToken);
        }

        var orders = context.Collection<RepairOrder>();
        var legacyImageFilter = Builders<RepairOrder>.Filter.Regex(
            "vehicleConditionImages",
            new BsonRegularExpression("^data:image/", "i"));
        var legacyOrders = await orders.Find(legacyImageFilter).ToListAsync(cancellationToken);
        foreach (var order in legacyOrders)
        {
            var paths = new List<string>(order.VehicleConditionImages.Count);
            foreach (var image in order.VehicleConditionImages)
            {
                paths.Add(image.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
                    ? await imageStorage.SaveDataUrlAsync(image, "repair-orders", cancellationToken)
                    : image);
            }
            await orders.UpdateOneAsync(
                x => x.Id == order.Id,
                Builders<RepairOrder>.Update
                    .Set(x => x.VehicleConditionImages, paths)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow),
                cancellationToken: cancellationToken);
        }

        if (legacyTransactions.Count > 0 || legacyOrders.Count > 0)
        {
            logger.LogInformation(
                "Migrated embedded images for {TransactionCount} transactions and {RepairOrderCount} repair orders.",
                legacyTransactions.Count,
                legacyOrders.Count);
        }
    }

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
        await CreateUniqueIndex(context.Collection<Supplier>(), x => x.Code, "ux_suppliers_code", cancellationToken);
        await CreateUniqueIndex(context.Collection<PartCategory>(), x => x.Code, "ux_part_categories_code", cancellationToken);
        await CreateUniqueIndex(context.Collection<ServiceCategory>(), x => x.Code, "ux_service_categories_code", cancellationToken);
        await CreateUniqueIndex(context.Collection<Part>(), x => x.Code, "ux_parts_code", cancellationToken);
        await context.Collection<Part>().Indexes.CreateOneAsync(
            new CreateIndexModel<Part>(
                Builders<Part>.IndexKeys.Ascending("specifications.value"),
                new CreateIndexOptions { Name = "ix_parts_specification_values" }),
            cancellationToken: cancellationToken);
        await context.Collection<SupplierPartStock>().Indexes.CreateOneAsync(
            new CreateIndexModel<SupplierPartStock>(
                Builders<SupplierPartStock>.IndexKeys
                    .Ascending(x => x.SupplierId)
                    .Ascending(x => x.PartId),
                new CreateIndexOptions { Unique = true, Name = "ux_supplier_part_stock" }),
            cancellationToken: cancellationToken);
        await CreateUniqueIndex(context.Collection<RepairOrder>(), x => x.Code, "ux_repair_orders_code", cancellationToken);
        await CreateUniqueIndex(context.Collection<Invoice>(), x => x.Code, "ux_invoices_code", cancellationToken);
        await CreateUniqueIndex(context.Collection<Coupon>(), x => x.Code, "ux_coupons_code", cancellationToken);
        await CreateUniqueIndex(context.Collection<CashTransaction>(), x => x.Code, "ux_cash_transactions_code", cancellationToken);
        await CreateUniqueIndex(context.Collection<CashCategory>(), x => x.Code, "ux_cash_categories_code", cancellationToken);
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
        await context.Collection<AuditLog>().Indexes.CreateManyAsync(
            [
                new CreateIndexModel<AuditLog>(
                    Builders<AuditLog>.IndexKeys.Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "ix_audit_created" }),
                new CreateIndexModel<AuditLog>(
                    Builders<AuditLog>.IndexKeys.Ascending(x => x.UserId).Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "ix_audit_user_created" }),
                new CreateIndexModel<AuditLog>(
                    Builders<AuditLog>.IndexKeys.Ascending(x => x.EntityType).Ascending(x => x.EntityId).Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "ix_audit_entity_created" })
            ],
            cancellationToken);

        await context.Collection<InventoryTransaction>().Indexes.CreateManyAsync(
            [
                new CreateIndexModel<InventoryTransaction>(
                    Builders<InventoryTransaction>.IndexKeys
                        .Ascending(x => x.PartId)
                        .Descending(x => x.TransactionDate),
                    new CreateIndexOptions { Name = "ix_inventory_part_date" }),
                new CreateIndexModel<InventoryTransaction>(
                    Builders<InventoryTransaction>.IndexKeys
                        .Ascending(x => x.SupplierId)
                        .Descending(x => x.TransactionDate),
                    new CreateIndexOptions { Name = "ix_inventory_supplier_date" })
            ],
            cancellationToken);

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
            Roles = [SecurityRoles.Admin]
        };
        var hasher = new PasswordHasher<AppUser>();
        user.PasswordHash = hasher.HashPassword(
            user,
            configuration["SeedAdmin:Password"] ?? "Admin@123456");
        await users.InsertOneAsync(user, cancellationToken: cancellationToken);
        logger.LogWarning("Seeded initial admin account '{Username}'. Change its password immediately.", username);
    }

    private async Task MigrateUserRoles(CancellationToken cancellationToken)
    {
        var users = context.Collection<AppUser>();
        var existingUsers = await users.Find(x => !x.IsDeleted).ToListAsync(cancellationToken);
        foreach (var user in existingUsers)
        {
            var role = user.Roles.Any(x => x.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                                           || x.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
                ? SecurityRoles.Admin
                : user.Roles.Any(x => x.Equals("Manager", StringComparison.OrdinalIgnoreCase))
                    ? SecurityRoles.Manager
                    : SecurityRoles.Employee;
            if (user.Roles.Count == 1 && user.Roles[0] == role) continue;
            await users.UpdateOneAsync(
                x => x.Id == user.Id,
                Builders<AppUser>.Update
                    .Set(x => x.Roles, new List<string> { role })
                    .Set(x => x.UpdatedAt, DateTime.UtcNow),
                cancellationToken: cancellationToken);
        }
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

    private async Task SeedCashCategories(CancellationToken cancellationToken)
    {
        var categories = context.Collection<CashCategory>();
        if (await categories.Find(x => !x.IsDeleted).AnyAsync(cancellationToken))
        {
            return;
        }
        await categories.InsertManyAsync(
            [
                new CashCategory { Code = "THU_KHAC", Name = "Thu khác", Scope = CashCategoryScope.Income },
                new CashCategory { Code = "CHI_KHAC", Name = "Chi khác", Scope = CashCategoryScope.Expense },
                new CashCategory { Code = "DIEN_NUOC", Name = "Điện nước", Scope = CashCategoryScope.Expense },
                new CashCategory { Code = "LUONG", Name = "Lương nhân viên", Scope = CashCategoryScope.Expense }
            ],
            cancellationToken: cancellationToken);
    }
}
