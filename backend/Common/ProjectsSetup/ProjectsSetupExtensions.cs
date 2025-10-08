using Aspire;
using Backend.Gateway;
using Backend.Matches;
using Backend.Users;
using Game;
using Infrastructure.Coordination;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using Infrastructure.TaskScheduling;
using Management.Configs;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceLoop;
using Services;

namespace Common;

public static class ProjectsSetupExtensions
{
    public static IHostApplicationBuilder SetupCoordinator(this IHostApplicationBuilder builder)
    {
        // Basic services
        builder
            .AddServiceDefaults()
            .AddOrleansClient();

        // Cluster services
        builder
            .AddBase(ServiceTag.Coordinator);

        // Project services

        return builder;
    }

    public static IHostApplicationBuilder SetupBackendGateway(this IHostApplicationBuilder builder)
    {
        // Basic services
        builder
            .AddServiceDefaults()
            .AddOrleansClient();

        // Cluster services
        builder
            .AddBase(ServiceTag.Gateway)
            .ConfigureCors();

        // Project services
        builder
            .AddUserFlow()
            .AddUserFactory()
            .AddBackendMatchServices()
            .AddMatchmakingServices()
            .AddUserCommands();

        builder.Services.AddOpenApi();

        return builder;
    }

    public static IHostApplicationBuilder SetupGameGateway(this IHostApplicationBuilder builder)
    {
        // Basic services
        builder
            .AddServiceDefaults()
            .AddOrleansClient();

        // Cluster services
        builder
            .AddBase(ServiceTag.Game)
            .ConfigureCors();

        // Project services
        builder
            .AddGlobalSessions();

        builder.Services
            .AddOpenApi()
            .AddCors();

        return builder;
    }

    public static IHostApplicationBuilder SetupSilo(this IHostApplicationBuilder builder)
    {
        // Basic services
        builder
            .AddServiceDefaults()
            .ConfigureSilo();

        // Cluster services
        builder
            .AddBase(ServiceTag.Silo);

        return builder;
    }

    public static IHostApplicationBuilder SetupConsole(this IHostApplicationBuilder builder)
    {
        // Basic services
        builder
            .AddServiceDefaults()
            .AddOrleansClient();

        // Cluster services
        builder
            .AddBase(ServiceTag.Console);

        return builder;
    }

    private static IHostApplicationBuilder AddBase(this IHostApplicationBuilder builder, ServiceTag serviceTag)
    {
        if (builder is WebApplicationBuilder webBuilder)
            webBuilder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = true);

        builder.Services.AddHostedService<ClusterParticipantStartup>();

        builder
            .AddEnvironment(serviceTag)
            .AddStateAttributes()
            .AddServiceLoop()
            .AddMessaging()
            .AddOrleansUtils()
            .AddServiceDiscovery()
            .AddConfigsServices()
            .AddTaskScheduling()
            .AddClusterFeatures();

        return builder;
    }
}