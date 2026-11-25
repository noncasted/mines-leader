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
// The services every role of the cluster shares.
public static class ProjectBaseSetupExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        internal IHostApplicationBuilder AddBase(ServiceTag serviceTag)
        {
            if (builder is WebApplicationBuilder webBuilder)
                webBuilder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = true);

            builder.Services.AddHostedService<ClusterParticipantStartup>();
            builder.Services.AddHostedService<DeployHealthChecker>();

            builder.Add<ClusterParticipantContext>()
                   .As<IClusterParticipantContext>();

            builder.Add<DeployContext>()
                   .As<IDeployContext>();

            builder.Add<ClusterStartupConfig>()
                   .As<IClusterStartupConfig>();

            builder.ConfigureRequiredServices(GameRoles.Required.ToArray());

            builder
                .AddEnvironment(serviceTag)
                .AddServiceLoop()
                .AddMessaging()
                .AddOrleansUtils()
                .AddServiceDiscovery()
                .AddTaskScheduling()
                .AddDeployCleanup()
                .AddClusterFeatures()
                .AddTests()
                .AddConfigs()
                .AddGameConfigs()
                .AddSideEffects()
                .AddStates()
                .AddMartenStore()
                .AddMonitoring()
                .AddGameMonitoring()
                .AddGameFlags()
                .AddUserServices();

            builder.AddBotServices();

            builder.Add<GeneratedUserProjectionsLoader>()
                   .As<IUserProjectionsLoader>();

            builder.Add<DbSource>()
                   .As<IDbSource>();

            builder.Add<StateStorageReader>()
                   .As<IStateStorageReader>();

            builder.AddMetricsSnapshots();

            return builder;
        }

        internal IHostApplicationBuilder AddStates()
        {
            var states = new List<GrainStateInfo>();
            GeneratedStatesRegistration.AddAllStates(states);
            var registry = new GrainStatesRegistry(states);
            builder.Add(registry).As<IGrainStatesRegistry>();
            return builder;
        }

        internal IHostApplicationBuilder AddSideEffects()
        {
            builder.Add<SideEffectsStorage>()
                   .As<ISideEffectsStorage>();

            builder.Services.Add<SideEffectsMonitorService>()
                             .As<ILocalSetupCompleted>();

            return builder;
        }

        internal IHostApplicationBuilder AddBlazorComponents()
        {
            builder.Services
                   .AddRazorComponents()
                   .AddInteractiveServerComponents();

            return builder;
        }
    }
}
