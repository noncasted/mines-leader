using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Orleans;

public static class OrleansExtensions
{
    public static IHostApplicationBuilder AddOrleansClient(this IHostApplicationBuilder builder)
    {
        builder.UseOrleansClient(clientBuilder =>
        {
            var postgresConnectionString = clientBuilder.Configuration.GetConnectionString(ConnectionNames.Postgres)!;

            clientBuilder.UseTransactions();

            clientBuilder.UseAdoNetClustering(options =>
            {
                options.Invariant = "Npgsql";
                options.ConnectionString = postgresConnectionString;
            });
        });

        return builder;
    }
}