using Orchestration;

namespace GameGateway;

// The host of the game gateway role: the separate service (Services/GameGateway) runs it alone, a monolith next to
// the other roles.
public static class GameHost
{
    public static WebApplication Build(WebApplicationOptions options)
    {
        var builder = WebApplication.CreateBuilder(options);

        builder.SetupGameGateway();
        builder.AddConsoleAccess();
        builder.AddSessionGate();

        var app = builder.Build();

        app.AddMiddleware();
        app.MapDefaultEndpoints();
        app.AddSessionEndpoints();

        return app;
    }
}
