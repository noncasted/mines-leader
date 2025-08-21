using Backend.Gateway;
using Infrastructure.Orleans;
using Microsoft.Extensions.Hosting;
using Shared;

namespace Backend.Users;

public static class UserEntityCommands
{
    public static IHostApplicationBuilder AddUserCommands(this IHostApplicationBuilder builder)
    {
        builder.AddUserCommand<UpdateDeck>();

        return builder;
    }

    public class UpdateDeck : UserCommand<SharedBackendUser.UpdateDeckRequest>
    {
        public UpdateDeck(IOrleans orleans)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        protected override Task<INetworkContext> Execute(
            IUserSession session,
            SharedBackendUser.UpdateDeckRequest request)
        {
            return _orleans.InTransaction(() =>
            {
                var deck = _orleans.GetGrain<IUserDeck>(session.UserId);

                var update = new Dictionary<int, IReadOnlyList<CardType>>();

                foreach (var (index, entry) in request.Projection.Entries)
                    update[index] = entry.Cards;

                return deck.Update(update, request.Projection.SelectedIndex);
            }).FromResult();
        }
    }
}