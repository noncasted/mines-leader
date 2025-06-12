using Common;
using Infrastructure.Orleans;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Shared;

namespace Backend.Users;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder AddUserEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost(BackendUserContexts.UpdateDeckEndpoint, UpdateDeck);

        return builder;
    }

    private static Task UpdateDeck(
        [FromBody] BackendUserContexts.UpdateDeckRequest request,
        [FromServices] IOrleans orleans)
    {
        return orleans.Transactions.Create(() =>
        {
            var deck = orleans.Grains.GetGrain<IUserDeck>(request.UserId);

            var update = new Dictionary<int, IReadOnlyList<CardType>>();

            foreach (var (index, entry) in request.Projection.Entries)
                update[index] = entry.Cards;
            
            return deck.Update(update, request.Projection.SelectedIndex);
        });
    }
}