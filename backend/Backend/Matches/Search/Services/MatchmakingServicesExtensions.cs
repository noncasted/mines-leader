using Backend.Gateway;
using Common;
using Microsoft.Extensions.Hosting;

namespace Backend.Matches;

public static class MatchmakingServicesExtensions
{
    public static IHostApplicationBuilder AddMatchmakingServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHostedSingleton<IMatchmaking, Matchmaking>();

        builder.AddUserCommand<MatchmakingCommands.Search>();
        builder.AddUserCommand<MatchmakingCommands.CancelSearch>();
        builder.AddUserCommand<MatchmakingCommands.Create>();
        
        return builder;
    }
}