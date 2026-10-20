using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Configuration;
using Orleans.Serialization.Configuration;
using Shared;

namespace Orchestration;

public static class OrleansSetupExtensions
{
    public static readonly TimeSpan ReplyTimeoutSeconds = TimeSpan.FromSeconds(5);

    // Dev-only localhost clustering ports. Orleans' defaults are 11111/30000, but 30000 is
    // routinely taken by Rider's embedded JCEF browser (cef_server), which kills the silo's
    // gateway listener on startup — keep the gateway well clear of it.
    public const int LocalSiloPort = 11111;
    public const int LocalGatewayPort = 30010;

    // Required: two games (or two environments) on one Postgres must not share the membership table rows.
    // Dev values come from the Aspire AppHost, production values from docker-compose.yaml.
    public const string ClusterIdKey = "Orleans:ClusterId";
    public const string ServiceIdKey = "Orleans:ServiceId";

    public static ClusterOptions ReadClusterIdentity(IConfiguration configuration)
    {
        return new ClusterOptions
        {
            ClusterId = ReadRequired(ClusterIdKey),
            ServiceId = ReadRequired(ServiceIdKey)
        };

        string ReadRequired(string key)
        {
            var value = configuration[key];

            if (string.IsNullOrWhiteSpace(value) == true)
            {
                throw new InvalidOperationException(
                    $"Orleans cluster identity '{key}' is not configured (environment variable '{key.Replace(":", "__")}')");
            }

            return value;
        }
    }

    private static void ApplyClusterIdentity(ClusterOptions target, ClusterOptions source)
    {
        target.ClusterId = source.ClusterId;
        target.ServiceId = source.ServiceId;
    }

    // Shared has no Orleans codegen, so its types (e.g. CardType) aren't in the type allowlist.
    // They get encoded by name whenever the runtime type differs from the declared one —
    // e.g. Dictionary<int, IReadOnlyList<CardType>> passed as IReadOnlyDictionary<...>.
    // AddAllowedAssembly doesn't cover generic arguments here, so allow each type explicitly.
    public static void AllowSharedTypes(TypeManifestOptions options)
    {
        foreach (var type in typeof(CardType).Assembly.GetTypes())
            options.AddAllowedType(type);
    }

    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddOrleansClient()
        {
            builder.UseOrleansClient(clientBuilder => {
                var postgresConnectionString = clientBuilder.Configuration.GetConnectionString(ConnectionNames.Postgres)
                                                            .ThrowIfNull();
                var identity = ReadClusterIdentity(clientBuilder.Configuration);

                clientBuilder.Configure<ClientMessagingOptions>(options => {
                    options.ResponseTimeout = ReplyTimeoutSeconds;
                    options.ResponseTimeoutWithDebugger = ReplyTimeoutSeconds * 10;
                });

                if (builder.Environment.IsDevelopment() == true)
                {
                    clientBuilder.UseLocalhostClustering(LocalGatewayPort, identity.ServiceId, identity.ClusterId);
                }
                else
                {
                    clientBuilder.UseAdoNetClustering(options => {
                        options.Invariant = "Npgsql";
                        options.ConnectionString = postgresConnectionString;
                    });
                }

                clientBuilder.Configure<ClusterOptions>(options => ApplyClusterIdentity(options, identity));
                clientBuilder.UseConnectionRetryFilter((_, _) => Task.FromResult(true));
                clientBuilder.Configure<TypeManifestOptions>(AllowSharedTypes);
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
                var identity = ReadClusterIdentity(configuration);

                siloBuilder.Configure<SiloMessagingOptions>(options => {
                    options.ResponseTimeout = ReplyTimeoutSeconds;
                    options.ResponseTimeoutWithDebugger = ReplyTimeoutSeconds * 10;
                });

                if (builder.Environment.IsDevelopment() == true)
                {
                    siloBuilder.UseLocalhostClustering(LocalSiloPort, LocalGatewayPort,
                        serviceId: identity.ServiceId, clusterId: identity.ClusterId);
                }
                else
                {
                    siloBuilder.UseAdoNetClustering(options => {
                        options.Invariant = "Npgsql";
                        options.ConnectionString = npgsqlConnectionString;
                    });
                }

                siloBuilder.Configure<ClusterOptions>(options => ApplyClusterIdentity(options, identity));
                siloBuilder.AddActivityPropagation();
                siloBuilder.Configure<TypeManifestOptions>(AllowSharedTypes);

                siloBuilder.AddGrainExtension<IGrainTransactionHandler, GrainTransactionHandler>();
            });

            return builder;
        }
    }
}