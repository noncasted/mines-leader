using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Game.Global;

public static class GameGlobalExtensions
{
    public static IHostApplicationBuilder AddGlobalSessions(this IHostApplicationBuilder app)
    {
        app.Services.Add<ISessionsCollection, SessionsCollection>();
        app.Services.Add<ISessionFactory, SessionFactory>();
        app.Services.Add<ISessionSearch, SessionSearch>();
        app.Services.Add<ICoordinatorSetupCompleted, SessionEndpoints>();

        return app;
    }
}