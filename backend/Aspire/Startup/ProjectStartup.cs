using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Aspire;

public class ProjectStartup : BackgroundService
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProjectStartup> _logger;

    public ProjectStartup(
        IHostApplicationLifetime applicationLifetime,
        IConfiguration configuration,
        ILogger<ProjectStartup> logger)
    {
        _applicationLifetime = applicationLifetime;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        _logger.LogInformation("[Startup] Startup in progress");

        var connectionString = _configuration.GetConnectionString(ConnectionNames.Postgres);

        _logger.LogInformation("[Startup] Connection string 2: {ConnectionString}", connectionString);

        const string checkTableQuery = @"
                                        SELECT EXISTS (
                                            SELECT 1
                                            FROM information_schema.tables
                                            WHERE table_schema = 'public' AND table_name = 'orleansstorage'
                                        )";

        var connection = await GetConnection();

        await using var checkCommand = new NpgsqlCommand(checkTableQuery, connection);

        var tableExists = await checkCommand.ExecuteScalarAsync(cancellation);

        if (tableExists is bool exists && exists == true)
        {
            const string membershipTruncateQuery = "TRUNCATE TABLE orleansmembershiptable;";
            await using var membershipTruncateCommand = new NpgsqlCommand(membershipTruncateQuery, connection);
            await membershipTruncateCommand.ExecuteNonQueryAsync(cancellation);
            
            _logger.LogInformation("[Startup] Startup is not required");
            _applicationLifetime.StopApplication();
            connection.Dispose();
            return;
        }

        var sqlFiles = new[]
        {
            "PostgreSQL-Main.sql",
            "PostgreSQL-Persistence.sql",
            "PostgreSQL-Clustering.sql",
            "PostgreSQL-Clustering-3.7.0.sql"
        };

        foreach (var file in sqlFiles)
        {
            var script = await File.ReadAllTextAsync(file, cancellation);

            _logger.LogInformation("[Startup] Execute db script: {File}", file);

            await using var command = new NpgsqlCommand(script, connection);
            await command.ExecuteNonQueryAsync(cancellation);
        }


        _logger.LogInformation("[Startup] Startup completed");
        _applicationLifetime.StopApplication();
        connection.Dispose();

        async Task<NpgsqlConnection> GetConnection()
        {
            var safeGuard = 0;

            while (safeGuard < 10)
            {
                safeGuard++;

                try
                {
                    var newConnection = new NpgsqlConnection(connectionString);
                    await newConnection.OpenAsync(cancellation);
                    return newConnection;
                }
                catch (Exception e)
                {
                    _logger.LogError("Failed to connect to database: {Message}", e.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellation);
            }

            throw new Exception();
        }
    }
}