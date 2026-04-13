using Infrastructure;
using Meta.Users;
using MetaGateway.UserFlow.Commands;
using MetaGateway.UserFlow.Connection;
using Shared;

namespace MetaGateway.UserFlow;

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
            return _orleans.InTransaction(async () => {
                var deck = _orleans.GetGrain<IUserDeck>(session.UserId);
                var cards = _orleans.GetGrain<IUserCards>(session.UserId);
                var ownedCards = await cards.GetAll();
                var ownedSet = new HashSet<CardType>(ownedCards);

                var update = new Dictionary<int, IReadOnlyList<CardType>>();

                foreach (var (index, entry) in request.Projection.Entries)
                {
                    foreach (var card in entry.Cards)
                    {
                        if (!ownedSet.Contains(card))
                            throw new InvalidOperationException($"Card {card} not owned");
                    }

                    update[index] = entry.Cards;
                }

                await deck.Update(update, request.Projection.SelectedIndex);
            }).FromResult();
        }
    }
}