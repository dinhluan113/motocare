namespace MotoCare.Api.Infrastructure;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; init; } = "mongodb://localhost:27017/?replicaSet=rs0";
    public string DatabaseName { get; init; } = "motocare";
    public bool InitializeOnStartup { get; init; } = true;
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "MotoCare.Api";
    public string Audience { get; init; } = "MotoCare.Cms";
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 480;
}
