using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Game;

public static class SessionExtensions
{
    public static IHostApplicationBuilder AddSessionServices(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddSingleton<ISession, Session>();
        services.AddSingleton<ICommandsCollection, CommandsCollection>();
        services.AddSingleton<IExecutionQueue, ExecutionQueue>();

        services.AddSingleton<IUserFactory, UserFactory>();
        services.AddSingleton<ISessionUsers, SessionUsers>();

        services.AddSingleton<ISessionProperties, SessionProperties>();
        services.AddSingleton<IEntityFactory, EntityFactory>();
        services.AddSingleton<ISessionEntities, SessionEntities>();
        services.AddSingleton<IServiceFactory, ServiceFactory>();
        services.AddSingleton<ISessionServices, SessionServices>();

        services.AddSingleton<IResponseCommand, EntityCreateCommand>();
        services.AddSingleton<ICommand, SetPropertyCommand>();
        services.AddSingleton<ICommand, EntityDestroyCommand>();
        services.AddSingleton<ICommand, EntityEventCommand>();
        services.AddSingleton<IResponseCommand, ServiceGetOrCreateCommand>();

        return builder;
    }
}