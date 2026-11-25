using Common.Extensions;
using Game.GamePlay;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Game.Global;

public static class GameGlobalExtensions
{
    public static IHostApplicationBuilder AddGlobalSessions(this IHostApplicationBuilder app)
    {
        app.Add<SessionsCollection>()
           .As<ISessionsCollection>();

        app.Add<SessionFactory>()
           .As<ISessionFactory>();

        app.Add<CheatsAccess>()
           .As<ICheatsAccess>();

        app.Add<SessionSearch>()
           .As<ISessionSearch>();

        app.Add<SessionEndpoints>()
           .As<ICoordinatorSetupCompleted>();

        return app;
    }
}