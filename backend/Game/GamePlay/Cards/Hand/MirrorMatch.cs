using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Copies the last card played by the opponent and applies its effect for the owner.
/// </summary>
public class MirrorMatch : ICard<CardUsePayload.MirrorMatch>
{
    public MirrorMatch(IGameContext gameContext, IServiceProvider serviceProvider, ICardConfigs configs)
    {
        _gameContext = gameContext;
        _serviceProvider = serviceProvider;
        _configs = configs;
    }

    private readonly IGameContext _gameContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ICardConfigs _configs;

    public CardUseResult Use(CardUseContext context, CardUsePayload.MirrorMatch payload)
    {
        var invoker = context.Invoker;
        var opponent = _gameContext.GetOpponent(invoker);
        var lastCard = opponent.Actions.LastUsedCardType;
        var lastPayload = opponent.Actions.LastUsedPayload;

        if (lastCard == null || lastPayload == null)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("Opponent has not used any card yet")
            };
        }

        if (lastCard.Value == CardType.MirrorMatch)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("Cannot copy Mirror Match")
            };
        }

        var copiedType = lastCard.Value;

        // Скопированная атака бьёт уже по доске оппонента: если её ещё нет, карта
        // сгенерировала бы поле за него.
        if (_configs.Value.All.TryGetValue(copiedType, out var copiedConfig) == true &&
            copiedConfig.Target == CardTarget.OpponentBoard &&
            opponent.Board.IsGenerated == false)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("Opponent board is not generated yet")
            };
        }

        var prefix = context.Snapshot.Count;
        var copiedUse = _serviceProvider.Use(context, lastPayload);

        if (copiedUse.Result.HasError == true)
            return copiedUse;

        // Inner card records CardUse against this card id. Fold it into MirrorMatch so the
        // client receives one CardUse whose payload type matches the card in hand.
        var copiedAction = context.Snapshot.TakeCardUseFrom(prefix);

        context.Snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.MirrorMatch()
        {
            TargetPlayer = invoker.User.Id,
            CopiedCard = copiedType,
            CopiedAction = copiedAction
        });

        return new CardUseResult
        {
            Result = copiedUse.Result
        };
    }
}