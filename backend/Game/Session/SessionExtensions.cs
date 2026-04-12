using Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Game.Session;

public static class SessionExtensions
{
    public static void AddSessionServices(this IServiceCollection services, SessionContainerData data)
    {
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddSingleton<ISessionLogger, SessionFileLogger>();
        services.AddSingleton<ISession, Session>();

        services.AddSingleton<IPropertyUpdateSender, PropertyUpdateSender>();

        services.AddSingleton(data);

        services.AddSingleton<ISessionData>(new SessionData
        {
            Id = data.Id,
            Lifetime = data.Lifetime,
            ExpectedUsers = data.ExpectedUsers,
            Type = data.Type
        });

        services.AddSingleton<ICommandsCollection, CommandsCollection>();
        services.AddSingleton<IExecutionQueue, ExecutionQueue>();

        services.AddSingleton<IUserFactory, UserFactory>();
        services.AddSingleton<ISessionUsers, SessionUsers>();

        services.AddSingleton<ISessionObjects, SessionObjects>();
        services.AddSingleton<IEntityFactory, EntityFactory>();
        services.AddSingleton<ISessionEntities, SessionEntities>();

        services.Add<IServiceFactory, ServiceFactory>();

        services.AddSingleton<ISessionServices, SessionServices>();

        services.AddSingleton<IResponseCommand, EntityCreateCommand>();
        services.AddSingleton<ICommand, SetPropertyCommand>();
        services.AddSingleton<ICommand, EntityDestroyCommand>();
        services.AddSingleton<ICommand, EntityEventCommand>();
        services.AddSingleton<IResponseCommand, ServiceGetOrCreateCommand>();
    }
}