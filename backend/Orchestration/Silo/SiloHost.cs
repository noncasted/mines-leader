using Game.ClientTelemetry;
using Orchestration;

namespace Silo;

// The host of the silo role: the separate service (Services/Silo) runs it alone, a monolith next to the other roles.
public static class SiloHost
{
    public static WebApplication Build(WebApplicationOptions options)
    {
        var builder = WebApplication.CreateBuilder(options);

        builder.SetupSilo();
        builder.AddClientTelemetrySessions();

        var app = builder.Build();

        app.MapDefaultEndpoints();

        return app;
    }
}
