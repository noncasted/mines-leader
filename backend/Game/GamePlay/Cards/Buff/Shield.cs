using Shared;

namespace Game.GamePlay;

/// <summary>
/// Increments the Shield modifier, absorbing the next mine hit and preventing HP damage.
/// </summary>
public class Shield : ICard<CardUsePayload.Shield>
{
    public CardUseResult Use(CardUseContext context, CardUsePayload.Shield payload)
    {
        var invoker = context.Invoker;

        var source = new DurationModifierSource(PlayerModifier.Shield, 1, "shield", -1);
        invoker.Modifiers.Add(context.Snapshot, source);

        context.Snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Shield()
        {
            TargetPlayer = invoker.User.Id
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}