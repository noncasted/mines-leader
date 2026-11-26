using Benchmarks;
using Cluster.Configs;
using Cluster.Coordination;
using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.Monitoring;
using Cluster.Monitoring.Online;
using Cluster.State;
using Common;
using Common.Extensions;
using Game.Global;
using Game.MatchMetrics;
using Infrastructure;
using Infrastructure.Execution;
using Infrastructure.Startup;
using Infrastructure.State;
using Meta.Bots;
using Meta.Matches;
using Meta.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Shared;

namespace Orchestration;

public static class BenchmarkSetupExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        // Benchmark nodes (every role), the runner and its storage. Off (the default, production): nothing of it is
        // registered and the silo does not activate the benchmark grains, although their assembly is deployed with it.
        internal IHostApplicationBuilder AddTests()
        {
            var availability = ReadBenchmarksAvailability(builder);
            builder.Services.AddSingleton(availability);

            if (availability.IsEnabled == false)
            {
                // GrainTypeOptions is filled from the type manifest of every referenced assembly: drop the benchmark
                // grain classes, so that a call to a benchmark grain fails instead of activating it. Clients ignore it.
                builder.Services.PostConfigure<GrainTypeOptions>(options =>
                    options.Classes.RemoveWhere(type => type.Assembly == BenchmarksAvailability.Assembly));

                return builder;
            }

            builder.Add<BenchmarkRunner>();
            builder.Add<BenchmarkStorage>();
            builder.Add<ClusterTestUtils>();

            // Auto-discover all BenchmarkNode<> subclasses in the Benchmarks assembly
            foreach (var type in BenchmarksAvailability.Assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (!IsTestNodeType(type))
                    continue;

                builder.Services.AddSingleton(type);
                builder.Services.AddSingleton(typeof(ICoordinatorSetupCompleted), sp => sp.GetRequiredService(type));
            }

            builder.Add<StateMigrationTest.MigrationTestStep_V0>()
                   .As<IStateMigrationStep>();

            builder.Add<StateMigrationTest.MigrationTestStep_V1>()
                   .As<IStateMigrationStep>();

            return builder;
        }

        internal static BenchmarksAvailability ReadBenchmarksAvailability(IHostApplicationBuilder host)
        {
            return BenchmarksAvailability.Read(host.Configuration, host.Environment);
        }

        private static bool IsTestNodeType(Type type)
        {
            var current = type.BaseType;

            while (current != null)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(BenchmarkNode<>))
                    return true;
                current = current.BaseType;
            }

            return false;
        }
    }
}
