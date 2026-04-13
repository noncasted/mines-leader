using Infrastructure;
using Meta.Users;
using MetaGateway.UserFlow.Commands;
using MetaGateway.UserFlow.Connection;
using Shared;

namespace MetaGateway.UserFlow;

public static class LootCommandsExtensions
{
    public static IHostApplicationBuilder AddLootCommands(this IHostApplicationBuilder builder)
    {
        builder.AddUserCommand<LootCommands.OpenLootBox>();
        builder.AddUserCommand<LootCommands.ChooseLootReward>();
        return builder;
    }
}

public static class LootCommands
{
    public class OpenLootBox : UserCommand<SharedBackendUser.LootOpenRequest>
    {
        public OpenLootBox(IOrleans orleans)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        protected override async Task<INetworkContext> Execute(
            IUserSession session,
            SharedBackendUser.LootOpenRequest request)
        {
            var loot = _orleans.GetGrain<IUserLoot>(session.UserId);
            var cards = _orleans.GetGrain<IUserCards>(session.UserId);

            var box = await _orleans.Transactions.Run(() => loot.GetBox(request.LootBoxId));

            if (box == null)
                return EmptyResponse.Fail("LootBox not found");

            var ownedCards = await _orleans.Transactions.Run(cards.GetAll);
            var choices = LootRewardPicker.Pick(ownedCards, 3);

            return new SharedBackendUser.LootOpenResponse
            {
                LootBoxId = request.LootBoxId,
                Choices = choices
            };
        }
    }

    public class ChooseLootReward : UserCommand<SharedBackendUser.LootChooseRequest>
    {
        public ChooseLootReward(IOrleans orleans)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        protected override async Task<INetworkContext> Execute(
            IUserSession session,
            SharedBackendUser.LootChooseRequest request)
        {
            var loot = _orleans.GetGrain<IUserLoot>(session.UserId);
            var cards = _orleans.GetGrain<IUserCards>(session.UserId);

            await _orleans.InTransaction(async () => {
                var removed = await loot.TryRemoveBox(request.LootBoxId);

                if (!removed)
                    throw new InvalidOperationException("LootBox not found or already opened");

                await cards.AddCard(request.ChosenCard);
            });

            return EmptyResponse.Ok;
        }
    }
}