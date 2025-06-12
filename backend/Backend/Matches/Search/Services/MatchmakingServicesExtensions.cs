using Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Shared;

namespace Backend.Matches;

public static class MatchmakingServicesExtensions
{
    public static IHostApplicationBuilder AddMatchmakingServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHostedSingleton<IMatchmaking, Matchmaking>();

        return builder;
    }

    public static IEndpointRouteBuilder AddMatchmakingEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost(MatchmakingContexts.SearchEndpoint, Search);
        builder.MapPost(MatchmakingContexts.CancelEndpoint, CancelSearch);
        builder.MapPost(MatchmakingContexts.CreateEndpoint, Create);

        return builder;

        async Task Search([FromBody] MatchmakingContexts.Search request, [FromServices] IMatchmaking matchmaking)
        {
            var userId = request.UserId;
            await matchmaking.Search(userId, request.Type);
        }

        async Task CancelSearch(
            [FromBody] MatchmakingContexts.CancelSearch request,
            [FromServices] IMatchmaking matchmaking)
        {
            await matchmaking.CancelSearch(request.UserId);
        }

        async Task Create([FromBody] MatchmakingContexts.Create request, [FromServices] IMatchmaking matchmaking)
        {
            var userId = request.UserId;
            await matchmaking.Create(userId);
        }
    }
}