using Common.Extensions;
using Microsoft.Extensions.Configuration;

namespace Aspire.Startup;

public record DbUpstream
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string Database { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }
    public IResourceBuilder<ContainerResource>? PostgresResource { get; init; }
}

// Production: external Postgres via Coolify env (DB_HOST, DB_USER, etc.).
// Dev: local Postgres container for `aspire run`.
public static class DbUpstreamFactory
{
    public static DbUpstream Create(IDistributedApplicationBuilder builder, IConfigurationManager configuration)
    {
        var externalHost = Environment.GetEnvironmentVariable("DB_HOST");

        // --- Production mode: external Postgres from Coolify env ---
        if (!string.IsNullOrEmpty(externalHost))
        {
            var user = Environment.GetEnvironmentVariable("DB_USER")
                       ?? throw new InvalidOperationException("DB_USER is required when DB_HOST is set");
            var password = Environment.GetEnvironmentVariable("DB_PASSWORD")
                             ?? throw new InvalidOperationException("DB_PASSWORD is required when DB_HOST is set");
            var database = Environment.GetEnvironmentVariable("DB_NAME")
                             ?? throw new InvalidOperationException("DB_NAME is required when DB_HOST is set");
            var port = int.TryParse(Environment.GetEnvironmentVariable("DB_PORT"), out var p) ? p : 5432;

            Console.WriteLine($"[AppHost] [DB] Using external database at {externalHost}:{port}/{database}");

            return new DbUpstream
            {
                Host = externalHost,
                Port = port,
                Database = database,
                User = user,
                Password = password,
                PostgresResource = null
            };
        }

        // --- Dev mode: local Postgres container ---
        var localDb = configuration.GetConnectionString("db")
                      ?? throw new InvalidOperationException("connectionString 'db' is required for local dev mode");
        var local = ParseConnString(localDb);

        var localPort = int.Parse(local["Port"]);
        var localDatabase = local["Database"];
        var localUser = local["User Id"];
        var localPassword = local["Password"];

        var postgres = builder
                       .AddContainer("postgres", "postgres", "17.6")
                       .WithHttpEndpoint(port: localPort, targetPort: 5432, name: "tcp", isProxied: false)
                       .WithVolume("mines-leader-postgres-data", "/var/lib/postgresql/data")
                       .WithEnvironment("POSTGRES_PASSWORD", localPassword)
                       .WithEnvironment("POSTGRES_DB", localDatabase)
                       .WithEnvironment("POSTGRES_USER", localUser)
                       .WithEnvironment("POSTGRES_HOST_AUTH_METHOD", "trust")
                       .WithLifetime(ContainerLifetime.Persistent);

        Console.WriteLine($"[AppHost] [DB] Using local database at localhost:{localPort}/{localDatabase}");

        return new DbUpstream
        {
            Host = "postgres",
            Port = 5432,
            Database = localDatabase,
            User = localUser,
            Password = localPassword,
            PostgresResource = postgres
        };
    }

    public static Dictionary<string, string> ParseConnString(string connString)
    {
        return connString
               .Split(';', StringSplitOptions.RemoveEmptyEntries)
               .Select(p => p.Split('=', 2))
               .Where(p => p.Length == 2)
               .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
