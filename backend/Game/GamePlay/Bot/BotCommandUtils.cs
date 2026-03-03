using Common.Reactive;
using Shared;

namespace Game.GamePlay;

public interface IBotCommandUtils
{
    void WithSnapshot(Action action);
    void WithSnapshot(Action<MoveSnapshot> action);
    bool UseCard(IPlayer bot, ICardUsePayload payload);
}

public class BotCommandUtils : IBotCommandUtils
{
    public BotCommandUtils(ISnapshotSender snapshotSender, IGameContext gameContext, ICardFactory cardFactory)
    {
        _snapshotSender = snapshotSender;
        _gameContext = gameContext;
        _cardFactory = cardFactory;
    }

    private readonly ISnapshotSender _snapshotSender;
    private readonly IGameContext _gameContext;
    private readonly ICardFactory _cardFactory;

    public void WithSnapshot(Action action)
    {
        var lifetime = new Lifetime();
        var snapshot = new MoveSnapshot();
        snapshot.HandleBoards(lifetime, _gameContext);

        action();

        lifetime.Terminate();
        _snapshotSender.Send(snapshot);
    }

    public void WithSnapshot(Action<MoveSnapshot> action)
    {
        var lifetime = new Lifetime();
        var snapshot = new MoveSnapshot();
        snapshot.HandleBoards(lifetime, _gameContext);

        action(snapshot);

        lifetime.Terminate();
        _snapshotSender.Send(snapshot);
    }

    public bool UseCard(IPlayer bot, ICardUsePayload payload)
    {
        var cardUsed = false;

        WithSnapshot(snapshot =>
        {
            var card = _cardFactory.Create(bot, snapshot, payload);
            var result = card.Use();
            cardUsed = result.HasError == false;
        });

        return cardUsed;
    }
}