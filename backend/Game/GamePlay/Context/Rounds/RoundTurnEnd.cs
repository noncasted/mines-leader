using Game.Session;

namespace Game.GamePlay;

public class RoundTurnEnd
{
    public RoundTurnEnd(
        RoundSnapshots snapshots,
        RoundPlayers players,
        IRoundActionService roundActionService,
        ISessionLogger sessionLogger)
    {
        _snapshots = snapshots;
        _players = players;
        _roundActionService = roundActionService;
        _sessionLogger = sessionLogger;
    }

    private readonly RoundSnapshots _snapshots;
    private readonly RoundPlayers _players;
    private readonly IRoundActionService _roundActionService;
    private readonly ISessionLogger _sessionLogger;

    public void Send(IPlayer player, int maxManaCap)
    {
        _snapshots.SendChecked("round:end", endSnapshot => {
            PlayerBaseBuffs.GrowMana(player, endSnapshot, maxManaCap);

            player.Mana.Restore(endSnapshot);
            _sessionLogger.LogManaChanged(player.User.Id, player.Mana.Current, player.Mana.ResultMax);

            // Played cards lie on the table until the end of the turn; here the server sends them to the stash.
            endSnapshot.RecordCardsStashed(player);

            _players.RestoreCards(player, endSnapshot);

            _roundActionService.Tick(endSnapshot, player.User.Id);
            player.Moves.Lock(endSnapshot);
        });
    }
}
