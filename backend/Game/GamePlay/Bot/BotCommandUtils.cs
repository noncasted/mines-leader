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
        ISnapshotDiffGuard diffGuard,
        IGameContext gameContext,
        IServiceProvider serviceProvider)
    {
        _snapshotSender = snapshotSender;
        _diffGuard = diffGuard;
        _gameContext = gameContext;
        _serviceProvider = serviceProvider;
    }

    private readonly ISnapshotSender _snapshotSender;
    private readonly ISnapshotDiffGuard _diffGuard;
    private readonly IGameContext _gameContext;
    private readonly IServiceProvider _serviceProvider;

    public ICardUsePayload? LastUsedPayload { get; private set; }

    public void WithSnapshot(Action action)
    {
        WithSnapshot(_ => action());
    }

    public void WithSnapshot(Action<MoveSnapshot> action)
    {
        var snapshot = new MoveSnapshot();

        var preState = _diffGuard.IsEnabled == true
            ? GameStateCapture.Capture(_gameContext)
            : null;

        action(snapshot);

        if (preState != null)
        {
            var postState = GameStateCapture.Capture(_gameContext);
            _diffGuard.Validate(preState, snapshot.Collect(), postState, "bot");
        }

        _snapshotSender.Send(snapshot);
    }

    public bool UseCard(IPlayer bot, Guid cardId, ICardUsePayload payload)
    {
        LastUsedPayload = payload;
        var wasUsed = false;

        WithSnapshot(snapshot => {
            var cardContext = new CardUseContext
            {
                Invoker = bot,
                Snapshot = snapshot,
                CardId = cardId
            };

            var use = _serviceProvider.Use(cardContext, payload);
            wasUsed = use.Result.HasError == false;
        });

        return wasUsed;
    }
}