using Shared;

namespace Game.GamePlay;

/// <summary>
/// Flips a coin: heads grants extra moves, tails costs moves.
/// </summary>
public class CoinToss : ICard {
    public CoinToss(IPlayer owner, CardConfigOptions.CoinToss config, IGameRandom gameRandom) {
        _owner = owner;
        _config = config;
        _gameRandom = gameRandom;
    }

    private readonly IPlayer _owner;
    private readonly CardConfigOptions.CoinToss _config;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use() {
        var isHeads = _gameRandom.FlipCoin(_owner);

        if (isHeads) {
            _owner.Moves.SetCurrent(_owner.Moves.Left + _config.WinMoves);
        } else {
            var newMoves = _owner.Moves.Left - _config.LoseMoves;
            if (newMoves < 0) newMoves = 0;
            _owner.Moves.SetCurrent(newMoves);
        }

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.CoinToss() {
                TargetPlayer = _owner.User.Id,
                IsHeads = isHeads
            }
        };
    }
}
