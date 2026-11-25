#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using UnityEngine;

namespace GamePlay.Agent
{
    // What the agent reads from the live boards of the client: a cell and whether the own board still animates.
    public class AgentBoardView
    {
        public AgentBoardView(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        private readonly IGameContext _gameContext;

        public CellInspect Inspect(int x, int y, bool opponent)
        {
            var player = opponent ? _gameContext.Other : _gameContext.Self;

            if (player.Board.Cells.TryGetValue(new Vector2Int(x, y), out var cell) == false)
                return CellInspect.Missing(x, y);

            var effects = cell.Effects.Active.Select(type => type.ToString()).ToList();

            return cell.State.Value switch
            {
                ICellFreeState free => new CellInspect(
                    x, y, true, "free", false, free.MinesAround.Value, effects, cell.PlayingAnimation),
                ICellTakenState taken => new CellInspect(
                    x, y, true, "taken", taken.IsFlagged.Value, null, effects, cell.PlayingAnimation),
                _ => throw new InvalidOperationException($"Cell ({x}, {y}) has no state")
            };
        }

        public async UniTask<bool> WaitIdle(int timeoutMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                if (IsOwnBoardAnimating() == false)
                    return true;

                await UniTask.Delay(50);
            }

            return false;
        }

        private bool IsOwnBoardAnimating()
        {
            return _gameContext.Self.Board.Cells.Values.Any(cell => cell.PlayingAnimation != null);
        }
    }
}
#endif
