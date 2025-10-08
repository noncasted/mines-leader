using Backend.Gateway;
using Common;
using Microsoft.Extensions.Hosting;
using ServiceLoop;

namespace Backend.Matches;

public static class MatchmakingServicesExtensions
{
    public static IHostApplicationBuilder AddMatchmakingServices(this IHostApplicationBuilder builder)
    {
        builder.Services.Add<Matchmaking>()
            .As<ICoordinatorSetupCompleted>()
            .As<IMatchmaking>();

        builder.AddUserCommand<MatchmakingCommands.Search>();
        builder.AddUserCommand<MatchmakingCommands.CancelSearch>();
        builder.AddUserCommand<MatchmakingCommands.Create>();
        
        return builder;
    }
}