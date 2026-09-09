using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Configuration;

namespace Orchestration;

public static class OrleansSetupExtensions
{
    public static readonly TimeSpan ReplyTimeoutSeconds = TimeSpan.FromSeconds(5);

    // Dev-only localhost clustering ports. Orleans' defaults are 11111/30000, but 30000 is
    // routinely taken by Rider's embedded JCEF browser (cef_server), which kills the silo's
    // gateway listener on startup — keep the gateway well clear of it.
    public const int LocalSiloPort = 11111;
    public const int LocalGatewayPort = 30010;

    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddOrleansClient()
        {
            builder.UseOrleansClient(clientBuilder => {
                var postgresConnectionString = clientBuilder.Configuration.GetConnectionString(ConnectionNames.Postgres)
                                                            .ThrowIfNull();

                clientBuilder.Configure<ClientMessagingOptions>(options => {
                    options.ResponseTimeout = ReplyTimeoutSeconds;
                    options.ResponseTimeoutWithDebugger = ReplyTimeoutSeconds * 10;
                });

                if (builder.Environment.IsDevelopment() == true)
                {
                    clientBuilder.UseLocalhostClustering(LocalGatewayPort);
                }
                else
                {
                    clientBuilder.UseAdoNetClustering(options => {
                        options.Invariant = "Npgsql";
                        options.ConnectionString = postgresConnectionString;
                    });
                }

                clientBuilder.UseConnectionRetryFilter((_, _) => Task.FromResult(true));
            });

            return builder;
        }

        public IHostApplicationBuilder ConfigureSilo()
        {
            var configuration = builder.Configuration;

            // Orleans 10.3 wraps grain placement in a Polly pipeline whose telemetry logs two Information
            // records per new activation ("Resilience pipeline executed", "Execution attempt"). With file,
            // console and OTLP log providers this costs ~0.2 ms per activation, so keep only warnings.
            builder.Logging.AddFilter("Polly", LogLevel.Warning);

            builder.UseOrleans(siloBuilder => {
                var npgsqlConnectionString = configuration.GetConnectionString(ConnectionNames.Postgres).ThrowIfNull();

                siloBuilder.Configure<SiloMessagingOptions>(options => {
                    options.ResponseTimeout = ReplyTimeoutSeconds;
                    options.ResponseTimeoutWithDebugger = ReplyTimeoutSeconds * 10;
                });

                if (builder.Environment.IsDevelopment() == true)
                {
                    siloBuilder.UseLocalhostClustering(LocalSiloPort, LocalGatewayPort);
                }
                else
                {
                    siloBuilder.UseAdoNetClustering(options => {
                        options.Invariant = "Npgsql";
                        options.ConnectionString = npgsqlConnectionString;
                    });
                }

                siloBuilder.AddActivityPropagation();

                siloBuilder.AddGrainExtension<IGrainTransactionHandler, GrainTransactionHandler>();
            });

            return builder;
        }
    }
}