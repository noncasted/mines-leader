using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Backend.Gateway;

public static class UserFlowExtensions
{
    public static IHostApplicationBuilder AddUserFlow(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        
        services.AddSignalR();
        services.AddSingleton<IConnectedUsers, ConnectedUsers>();
        services.AddHostedService<UserProjectionEntryPoint>();
        services.AddSingleton<IUserCommandsCollection, UserCommandsCollection>();
        services.AddSingleton<IUserCommandsDispatcher, UserCommandsDispatcher>();
        
        return builder;
    }
}