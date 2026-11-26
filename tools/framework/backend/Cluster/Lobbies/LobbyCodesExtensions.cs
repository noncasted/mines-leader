using Cluster.Deploy;
using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Cluster.Lobbies;

public static class LobbyCodesExtensions
{
    public static IHostApplicationBuilder AddLobbyCodes(this IHostApplicationBuilder builder)
    {
        builder.Add<LobbyCodes>()
               .As<ILobbyCodes>();

        return builder;
    }
}
