using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Runtime.Hosting;

namespace Orchestration;

public static class OrleansSetupExtensions
{
    public static readonly TimeSpan ReplyTimeoutSeconds = TimeSpan.FromSeconds(5);

    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddOrleansClient()
        {
            builder.UseOrleansClient(clientBuilder =>
                {
                    var postgresConnectionString =
                        clientBuilder.Configuration.GetConnectionString(ConnectionNames.Postgres)!;

                    clientBuilder.UseTransactions();

                    clientBuilder.Configure<ClientMessagingOptions>(options =>
                        {
                            options.ResponseTimeout = ReplyTimeoutSeconds;
                            options.ResponseTimeoutWithDebugger = ReplyTimeoutSeconds;
                        }
                    );

                    if (builder.Environment.IsDevelopment() == true)
                    {
                        clientBuilder.UseLocalhostClustering();
                    }
                    else
                    {
                        clientBuilder.UseAdoNetClustering(options =>
                            {
                                options.Invariant = "Npgsql";
                                options.ConnectionString = postgresConnectionString;
                            }
                        );
                    }

                    clientBuilder.UseConnectionRetryFilter((_, _) => Task.FromResult(true));
                }
            );

            return builder;
        }

        public IHostApplicationBuilder ConfigureSilo()
        {
            var configuration = builder.Configuration;

            TransactionalStateOptions.DefaultLockTimeout = ReplyTimeoutSeconds;

            builder.UseOrleans(siloBuilder =>
                {
                    var npgsqlConnectionString = configuration.GetConnectionString(ConnectionNames.Postgres)!;

                    siloBuilder.UseTransactions();

                    siloBuilder.Configure<SiloMessagingOptions>(options =>
                        {
                            options.ResponseTimeout = ReplyTimeoutSeconds;
                            options.ResponseTimeoutWithDebugger = ReplyTimeoutSeconds;
                        }
                    );

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
}