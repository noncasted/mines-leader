using Aspire;
using Backend.Matches;
using Backend.Users;
using Common;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using Infrastructure.TaskScheduling;
using ServiceLoop;

namespace Backend.Gateway;

public static class BackendGatewaySetup
{
    public static IHostApplicationBuilder SetupBackendGateway(this IHostApplicationBuilder builder)
    {
        // Basic services
        builder
            .AddServiceDefaults()
            .AddOrleansClient();

        // Cluster services
        builder
            .AddEnvironment(ServiceTag.Backend)
            .AddServiceLoop()
            .AddMessaging()
            .AddOrleansUtils()
            .AddStateAttributes()
            .AddServersCollection()
            .AddTaskScheduling()
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
}