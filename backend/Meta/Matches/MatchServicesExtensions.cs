using Cluster.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Meta.Matches;

public static class MatchServicesExtensions
{
    public static IHostApplicationBuilder AddBackendMatchServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IMatchFactory, MatchFactory>();
        builder.Services.AddSingleton<ILobbyFactory, LobbyFactory>();

        // No game gateway writes its sessions yet (LiveSessionsData): every server counts zero, the pick is random.
        builder.Services.AddSingleton<IGameServerBalancer, GameServerBalancer>();

        return builder;
    }
}