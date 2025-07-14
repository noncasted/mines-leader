using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceLoop;

namespace Backend.Gateway;

public static class UserFlowExtensions
{
    public static IHostApplicationBuilder AddUserFlow(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddSingleton<IConnectedUsers, ConnectedUsers>();
        services.AddHostedService<UserProjectionEntryPoint>();
        services.AddSingleton<IUserCommandsCollection, UserCommandsCollection>();
        services.AddSingleton<IUserCommandsDispatcher, UserCommandsDispatcher>();

        return builder;
    }

    public static IHostApplicationBuilder AddUserCommand<T>(this IHostApplicationBuilder builder)
        where T : class, IUserCommand
    {
        builder.AddSingleton<IUserCommand, T>();
        return builder;
    }
}