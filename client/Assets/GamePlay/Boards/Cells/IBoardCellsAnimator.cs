using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Boards
{
    public interface IBoardCellsAnimator
    {
        UniTask PlayTargetAnimation(IReadOnlyLifetime lifetime, Guid targetPlayer, IReadOnlyList<Position> positions);
        UniTask PlayActionAnimation(IReadOnlyLifetime lifetime, Guid targetPlayer, IReadOnlyList<Position> positions);
        UniTask OpenCells(IReadOnlyLifetime lifetime, Guid targetPlayer, IReadOnlyList<OpenedCell> cells);
        void AddEffect(Guid effectId, CellEffectType type, Guid targetPlayer, Position position);
        void FlagCell(Guid targetPlayer, Position position);
        void EnsureTaken(Guid targetPlayer, Position position);
        void UnflagCell(Guid targetPlayer, Position position);
        UniTask ExplodeCell(Guid targetPlayer, Position position, CellExplosionType type);
    }
}
