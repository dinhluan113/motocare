using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using MotoCare.Api.Domain;

namespace MotoCare.Api.Infrastructure;

public sealed class MongoDbContext
{
    private static int _conventionsRegistered;

    public MongoDbContext(IOptions<MongoOptions> options)
    {
        RegisterConventions();
        var settings = MongoClientSettings.FromConnectionString(options.Value.ConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        Client = new MongoClient(settings);
        Database = Client.GetDatabase(options.Value.DatabaseName);
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }

    public IMongoCollection<T> Collection<T>() where T : BaseDocument =>
        Database.GetCollection<T>(CollectionNames.For<T>());

    private static void RegisterConventions()
    {
        if (Interlocked.Exchange(ref _conventionsRegistered, 1) == 1)
        {
            return;
        }

        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreExtraElementsConvention(true)
        };
        ConventionRegistry.Register("MotoCare conventions", pack, _ => true);
    }
}

public static class CollectionNames
{
    private static readonly IReadOnlyDictionary<Type, string> Names =
        new Dictionary<Type, string>
        {
            [typeof(AppUser)] = "users",
            [typeof(Customer)] = "customers",
            [typeof(Employee)] = "employees",
            [typeof(VehicleBrand)] = "vehicle_brands",
            [typeof(VehicleModel)] = "vehicle_models",
            [typeof(Vehicle)] = "vehicles",
            [typeof(PartBrand)] = "part_brands",
            [typeof(Part)] = "parts",
            [typeof(InventoryTransaction)] = "inventory_transactions",
            [typeof(RepairOrder)] = "repair_orders",
            [typeof(Invoice)] = "invoices",
            [typeof(CashTransaction)] = "cash_transactions",
            [typeof(LoyaltyTier)] = "loyalty_tiers",
            [typeof(LoyaltyRule)] = "loyalty_rules",
            [typeof(LoyaltyAccount)] = "loyalty_accounts",
            [typeof(LoyaltyTransaction)] = "loyalty_transactions",
            [typeof(Notification)] = "notifications",
            [typeof(AuditLog)] = "audit_logs",
            [typeof(Sequence)] = "sequences"
        };

    public static string For<T>() where T : BaseDocument =>
        Names.TryGetValue(typeof(T), out var name)
            ? name
            : throw new InvalidOperationException($"No MongoDB collection configured for {typeof(T).Name}.");
}
