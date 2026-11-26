using Cluster.Coordination;
using Cluster.Discovery;
using Cluster.Lobbies;
using Common.Extensions;
using Game.Hosting.Collection;
using Game.Lobbies;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Game.Hosting;

public static class HostingExtensions
{
    // The meta: the requests of the users to the game servers (ISessionRequester) and the list of open lobbies
    // (IOpenLobbiesWatcher). The commands of the meta socket that call them stay in the game.
    public static IHostApplicationBuilder AddSessionRequester(this IHostApplicationBuilder builder)
    {
        builder.AddLobbyCodes();
        builder.AddOpenLobbiesWatcher();

        builder.Add<GameServerBalancer>()
               .As<IGameServerBalancer>();

        builder.Add<SessionRequester>()
               .As<ISessionRequester>();

        return builder;
    }

    // A game server: the sessions it hosts, the lobby sessions among them and the handlers of the meta requests. The
    // game registers what builds its sessions: ISessionSearch, IKnownUsersMatches, ILobbyFactory, ILobbyMatches and
    // ILobbyEntryPolicy (LobbyEntryPolicy.Always when every running match can be entered at any time).
    public static IHostApplicationBuilder AddSessionHosting(this IHostApplicationBuilder builder, LobbyOptions lobbies)
    {
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(lobbies);

        builder.Add<SessionsCollection>()
               .As<ISessionsCollection>();

        builder.AddLobbyCodes();

        // What becomes of the code of a lobby after its match: one rule per server.
        if (lobbies.ReturnsAfterMatch == true)
            builder.Add<LobbyReturns>().As<ILobbyCodeAfterMatch>();
        else
            builder.Add<LobbyCodeRelease>().As<ILobbyCodeAfterMatch>();

        builder.Add<LobbyCodeLeases>()
               .As<ILobbyCodeLeases>()
               .As<ILobbyListings>();

        builder.Add<LobbySessions>()
               .As<ILobbySessions>();

        builder.Add<SessionEndpoints>()
               .As<ICoordinatorSetupCompleted>();

        return builder;
    }
}
