using Common.Extensions;
using Infrastructure;
using Orchestration;

namespace Coordinator;

// The host of the coordinator role: the separate service (Services/Coordinator) runs it alone, a monolith next to
// the other roles.
public static class CoordinatorHost
{
    public static WebApplication Build(WebApplicationOptions options)
    {
        var builder = WebApplication.CreateBuilder(options);

        builder.SetupCoordinator();

        builder.Services.Add<ClusterCoordinator>()
               .As<ILocalSetupCompleted>();

        builder.Services.AddHostedService<DeployIdentity>();

        var app = builder.Build();

        app.MapDefaultEndpoints();

        return app;
    }
}
