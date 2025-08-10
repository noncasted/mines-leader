using Common;
using Microsoft.Extensions.DependencyInjection;

namespace Game;

public static class SessionExtensions
{
    public static void AddSessionServices(this IServiceCollection services, SessionContainerData data)
    {
        services.AddLogging();
        services.AddSingleton<ISession, Session>();

        services.AddSingleton(data);
        services.AddSingleton<ISessionData>(new SessionData
            {
                ExpectedUsers = data.CreateOptions.ExpectedUsers,
                Type = data.CreateOptions.Type,
                Id = data.Id,
                Lifetime = data.Lifetime
            }
        );

        services.AddSingleton<ICommandsCollection, CommandsCollection>();
        services.AddSingleton<IExecutionQueue, ExecutionQueue>();

        services.AddSingleton<IUserFactory, UserFactory>();
        services.AddSingleton<ISessionUsers, SessionUsers>();

        services.AddSingleton<ISessionObjects, SessionObjects>();
        services.AddSingleton<IEntityFactory, EntityFactory>();
        services.AddSingleton<ISessionEntities, SessionEntities>();

        services.Add<IServiceFactory, ServiceFactory>()
            .As<ISessionCreated>();

        services.AddSingleton<ISessionServices, SessionServices>();

        services.AddSingleton<IResponseCommand, EntityCreateCommand>();
        services.AddSingleton<ICommand, SetPropertyCommand>();
        services.AddSingleton<ICommand, EntityDestroyCommand>();
        services.AddSingleton<ICommand, EntityEventCommand>();
        services.AddSingleton<IResponseCommand, ServiceGetOrCreateCommand>();
    }
}