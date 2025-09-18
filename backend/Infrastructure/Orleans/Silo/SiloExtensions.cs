using Common;
using Orleans.Configuration;
using Orleans.Runtime.Hosting;

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

                if (builder.Environment.IsDevelopment() == true)
                {
                    siloBuilder.UseLocalhostClustering();
                }
                else
                {
                    siloBuilder.UseAdoNetClustering(options =>
                        {
                            options.Invariant = "Npgsql";
                            options.ConnectionString = npgsqlConnectionString;
                        }
                    );
                }

                siloBuilder.AddAdoNetGrainStorageAsDefault(options =>
                    {
                        options.Invariant = "Npgsql";
                        options.ConnectionString = npgsqlConnectionString;
                    }
                );

                foreach (var name in States.StateTables)
                {
                    siloBuilder.Services.AddGrainStorage(name,
                        (s, _) => NamedGrainStorageFactory.Create(s, name, npgsqlConnectionString)
                    );
                }

                siloBuilder.AddActivityPropagation();
            }
        );

        return builder;
    }
}