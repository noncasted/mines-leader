using Cluster.Discovery;
using Cluster.Monolith;
using ConsoleGateway;
using Coordinator;
using GameGateway;
using MetaGateway;
using Silo;

// All roles of the game in one process: the host factories of Orchestration/<Role>, the ports and content roots from
// Monolith:Hosts:<Tag> of the configuration (backend/Tools/deploy/docker-compose.monolith.yaml). Each application name is
// an assembly of this process (ASP.NET Core loads it as a hosting startup assembly): the library of the role, and for
// the console this exe, whose manifest (Monolith.staticwebassets.endpoints.json) holds the static web assets that
// MapStaticAssets finds by the application name.
await MonolithRunner.Create(args, [
    Host(GameRoles.Silo, typeof(SiloHost), SiloHost.Build),
    Host(ServiceTag.Coordinator, typeof(CoordinatorHost), CoordinatorHost.Build),
    Host(GameRoles.Meta, typeof(MetaHost), MetaHost.Build),
    Host(GameRoles.Game, typeof(GameHost), GameHost.Build),
    Host(GameRoles.Console, typeof(Program), ConsoleHost.Build)
]).Run(CancellationToken.None);

static MonolithHost Host(ServiceTag tag, Type assemblyOf, Func<WebApplicationOptions, WebApplication> build)
{
    return new MonolithHost { Tag = tag, ApplicationName = assemblyOf.Assembly.GetName().Name!, Build = build };
}
