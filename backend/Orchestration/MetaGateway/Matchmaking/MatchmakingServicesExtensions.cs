using Common.Extensions;
using Infrastructure;
using MetaGateway.UserFlow;

namespace MetaGateway.Matchmaking;

public static class MatchmakingServicesExtensions
{
    public static IHostApplicationBuilder AddMatchmakingServices(this IHostApplicationBuilder builder)
    {
        builder.Services.Add<Matchmaking>()
            .As<ICoordinatorSetupCompleted>()
            .As<IMatchmaking>();

        builder.AddUserCommand<MatchmakingCommands.SearchLobby>();
        builder.AddUserCommand<MatchmakingCommands.SearchMatch>();
        builder.AddUserCommand<MatchmakingCommands.CancelSearch>();
        builder.AddUserCommand<MatchmakingCommands.Create>();

        return builder;
    }
}