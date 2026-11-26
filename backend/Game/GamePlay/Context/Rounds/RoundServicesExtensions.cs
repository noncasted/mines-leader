using Common.Extensions;
using Game.Session;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace Game.GamePlay;

public static class RoundServicesExtensions
{
    public static IServiceCollection AddRound(this IServiceCollection services, GameMatchType type)
    {
        services.Add<RoundPlayers>();
        services.Add<RoundSnapshots>();
        services.Add<RoundReadiness>();
        services.Add<RoundTurn>();
        services.Add<RoundTurnEnd>();
        services.Add<RoundOutcome>();

        switch (type)
        {
            case GameMatchType.Single:
                break;
            case GameMatchType.TimeLimited:
                services.Add<TimeLimitedRound>()
                        .As<IService>()
                        .As<IGameRound>();
                break;
            case GameMatchType.LastManStanding:
                services.Add<LastManStandingRound>()
                        .As<IService>()
                        .As<IGameRound>();
                break;
            case GameMatchType.LastManStandingTurnBased:
                services.Add<LastManStandingTurnBasedRound>()
                        .As<IService>()
                        .As<IGameRound>();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return services;
    }
}
