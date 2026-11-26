using Game.Session;
using Microsoft.Extensions.DependencyInjection;

namespace Game.Lobbies;

public static class LobbyServiceExtensions
{
    // The container of a lobby session (next to AddSessionServices): the lobby service over the info of the session.
    // The host passes ILobbyMatches and ILobbyListings into the container (ContainerExtensions.Pass).
    public static IServiceCollection AddLobbyService(this IServiceCollection services, LobbySessionInfo info)
    {
        return services.AddLobbyService<LobbyService>(info);
    }

    // A lobby service of the game (its own properties, its own admission of the late players).
    public static IServiceCollection AddLobbyService<TService>(this IServiceCollection services, LobbySessionInfo info)
        where TService : LobbyService
    {
        services.AddSingleton(info);
        services.AddSingleton<IService, TService>();

        return services;
    }

    // The container of a match started by a lobby: the match shows itself in the list of open lobbies by the code of
    // its lobby.
    public static IServiceCollection AddMatchListingService(this IServiceCollection services, MatchListingInfo info)
    {
        services.AddSingleton(info);
        services.AddSingleton<IService, MatchListingService>();

        return services;
    }
}
