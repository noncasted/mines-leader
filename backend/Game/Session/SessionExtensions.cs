using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Game;

public static class SessionExtensions
{
    public static IHostApplicationBuilder AddSessionServices(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddSingleton<ICommandsCollection, CommandsCollection>();

        services.AddScoped<ISessionUsers, SessionUsers>();
        services.AddScoped<ISessionEntities, SessionEntities>();
        services.AddScoped<IExecutionQueue, ExecutionQueue>();

        services.AddSingleton<IResponseCommand, EntityCreateCommand>();
        services.AddSingleton<ICommand, EntityPropertyUpdateCommand>();
        services.AddSingleton<ICommand, EntityDestroyCommand>();
        services.AddSingleton<ICommand, EntityEventCommand>();
        services.AddSingleton<IResponseCommand, ServiceEntityCreateCommand>();

        services.AddScoped<Func<SessionContainerData, ISession>>(provider => data =>
        {
            var commandsCollection = provider.GetRequiredService<ICommandsCollection>();
            var users = provider.GetRequiredService<ISessionUsers>();
            var entities = provider.GetRequiredService<ISessionEntities>();
            var logger = provider.GetRequiredService<ILogger<Session>>();
            var executionQueue = provider.GetRequiredService<IExecutionQueue>();

            var createOptions = data.CreateOptions;
            var metadata = new SessionMetadata(createOptions.ExpectedUsers, createOptions.Type);
            var entityFactory = new EntityFactory(entities, data.Lifetime);

            var userFactory = new UserFactory(users, logger, executionQueue);

            var session = new Session(
                metadata,
                userFactory,
                users,
                commandsCollection,
                entities,
                entityFactory,
                data.Lifetime,
                executionQueue,
                logger,
                data.Id);

            return session;
        });


        return builder;
    }
}