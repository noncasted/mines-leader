using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.Lobbies;
using Cluster.Monitoring;
using Common.Network;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Session;
using Game.Hosting;
using Game.Hosting.Collection;
using Game.Lobbies;
using Game.Session;
using Internal;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;

namespace Framework.Tests.Hosting;

public static class HostingTestSessionExtensions
{
    // The lobby service of a lobby session.
    public static LobbyService LobbyService(this TestSession session)
    {
        return session.Provider.GetServices<IService>().OfType<LobbyService>().Single();
    }

    // The listing service of a match started by a lobby; null for any other session.
    public static MatchListingService? MatchListing(this TestSession session)
    {
        return session.Provider.GetServices<IService>().OfType<MatchListingService>().SingleOrDefault();
    }

    public static LobbySessionInfo LobbyInfo(this TestSession session)
    {
        return session.Provider.GetRequiredService<LobbySessionInfo>();
    }

    // A join by the code of a lobby that has not started its match (what the meta sends then).
    public static LobbyJoinStatus JoinLobby(this LobbySessions lobbies, Guid sessionId, Guid userId)
    {
        return lobbies.Join(sessionId, Guid.Empty, string.Empty, userId).Status;
    }

    public static T Read<T>(this IService service, int propertyId)
    {
        return MemoryPackSerializer.Deserialize<T>(service.Properties[propertyId].RawValue)!;
    }
}
