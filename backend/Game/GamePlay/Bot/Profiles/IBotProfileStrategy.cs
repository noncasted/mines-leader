using Common.Reactive;
using Shared;

namespace Game.GamePlay;

public interface IBotProfileStrategy
{
    BotProfile Profile { get; }

    /// <summary>
    /// How deep constraint-solving goes:
    /// 1 = single-cell (MinesAround == flaggedCount)
    /// 2 = subset/overlap reasoning between two cells
    /// </summary>
    int ConstraintDepth { get; }

    Task ExecuteTurn(IReadOnlyLifetime lifetime);
}
