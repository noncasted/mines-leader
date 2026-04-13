using Common.Reactive;
using Shared;

namespace Game.GamePlay;

public interface IBotCommandUtils
{
    ICardUsePayload? LastUsedPayload { get; }
    void WithSnapshot(Action action);
    void WithSnapshot(Action<MoveSnapshot> action);
    bool UseCard(IPlayer bot, Guid cardId, ICardUsePayload payload);
}

public class BotCommandUtils : IBotCommandUtils
{
    public BotCommandUtils(
        ISnapshotSender snapshotSender,
        IGameContext gameContext,
        IServiceProvider serviceProvider,
        MoveSnapshotAccessor snapshotAccessor)
    {
        _snapshotSender = snapshotSender;
        _gameContext = gameContext;
        _serviceProvider = serviceProvider;
        _snapshotAccessor = snapshotAccessor;
    }

    private readonly ISnapshotSender _snapshotSender;
    private readonly IGameContext _gameContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly MoveSnapshotAccessor _snapshotAccessor;

    public ICardUsePayload? LastUsedPayload { get; private set; }

    public void WithSnapshot(Action action)
    {
        var lifetime = new Lifetime();
        var snapshot = new MoveSnapshot();
        snapshot.HandleBoards(lifetime, _gameContext);
        snapshot.HandlePlayers(lifetime, _gameContext);

        action();

        lifetime.Terminate();
        _snapshotSender.Send(snapshot);
    }

    public void WithSnapshot(Action<MoveSnapshot> action)
    {
        var lifetime = new Lifetime();
        var snapshot = new MoveSnapshot();
        snapshot.HandleBoards(lifetime, _gameContext);
        snapshot.HandlePlayers(lifetime, _gameContext);

        action(snapshot);

        lifetime.Terminate();
        _snapshotSender.Send(snapshot);
    }

    public bool UseCard(IPlayer bot, Guid cardId, ICardUsePayload payload)
    {
        LastUsedPayload = payload;
        var wasUsed = false;

        WithSnapshot(snapshot => {
            _snapshotAccessor.Set(snapshot, cardId);

            var use = _serviceProvider.Use(bot, payload);
            wasUsed = use.Result.HasError == false;

            if (wasUsed == false)
                return;

            if (use.ActionData != null)
                snapshot.RecordCardUse(bot.User.Id, cardId, use.ActionData);

            foreach (var (_, board) in _gameContext.Boards)
                board.OnUpdated();
        });

        return wasUsed;
    }
}