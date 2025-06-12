using Common;
using Orleans.Configuration;

namespace Infrastructure.Orleans;

public static class SiloExtensions
{
    public static WebApplicationBuilder AddOrleans(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        TransactionalStateOptions.DefaultLockTimeout = TimeSpan.FromSeconds(5);

        builder.UseOrleans(siloBuilder =>
        {
            var npgsqlConnectionString = configuration.GetConnectionString(ConnectionNames.Postgres)!;

            siloBuilder.UseTransactions();
            
            siloBuilder.UseAdoNetClustering(options => {
                options.Invariant = "Npgsql";
                options.ConnectionString = npgsqlConnectionString;
            });

            siloBuilder.AddAdoNetGrainStorageAsDefault(options =>
            {
                options.Invariant = "Npgsql";
                options.ConnectionString = npgsqlConnectionString;
            });

            siloBuilder.AddActivityPropagation();
        });

        return builder;
    }
}