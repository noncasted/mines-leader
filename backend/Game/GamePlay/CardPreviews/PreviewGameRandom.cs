namespace Game.GamePlay.CardPreviews;

/// <summary>
/// Deterministic <see cref="IGameRandom"/> for preview generation — always returns the
/// upper bound so random-sized cards render their maximum footprint in the preview.
/// </summary>
internal sealed class PreviewGameRandom : IGameRandom
{
    public bool FlipCoin(IPlayer player) => true;
    public int RollDice(IPlayer player, int sides) => sides;
    public int Range(IPlayer player, int min, int max) => max;
    public int Index(IPlayer player, int count) => count > 0 ? 0 : 0;
}
