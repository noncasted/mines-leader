using Aspire;
using Backend.Matches;
using Common;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using Infrastructure.TaskScheduling;
using ServiceLoop;
using Services;

namespace Game.Gateway;

public static class GameGatewaySetup
{
    public static IHostApplicationBuilder SetupGameGateway(this IHostApplicationBuilder builder)
    {
        // Basic services
        builder
            .AddServiceDefaults()
            .AddOrleansClient();

        // Cluster services
        builder
            .AddEnvironment(ServiceTag.Server)
            .AddServiceLoop()
            .AddMessaging()
            .AddOrleansUtils()
            .AddServiceDiscovery()
            .AddTaskScheduling()
            .ConfigureCors();

        // Project services
        builder
            .AddGameMatchServices()
            .AddGlobalSessions();

        builder.Services
            .AddOpenApi()
            .AddCors();

        return builder;
    }
}