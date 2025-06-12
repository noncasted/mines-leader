using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Game;

public static class GameGlobalExtensions
{
    public static IHostApplicationBuilder AddGlobalSessions(this IHostApplicationBuilder app)
    {
        app.Services.AddSingleton<ISessionsCollection, SessionsCollection>();
        app.Services.AddSingleton<ISessionFactory, SessionFactory>();
        app.Services.AddSingleton<ISessionSearch, SessionSearch>();

        return app;
    }
}