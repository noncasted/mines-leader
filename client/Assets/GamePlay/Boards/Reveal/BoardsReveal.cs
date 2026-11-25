using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.Audio;
using Internal;
using Shared;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GamePlay.Boards
{
    public interface IBoardsReveal
    {
        UniTask Play(IReadOnlyLifetime lifetime, MatchCompletedData data);
    }

    // End of the match: the boards are brought to the server state, after which all remaining mines
    // on the loser's board are detonated along the curve from the config.
    public class BoardsReveal : IBoardsReveal
    {
        public BoardsReveal(IGameContext gameContext, IAudioPlayer audioPlayer)
        {
            _gameContext = gameContext;
            _audioPlayer = audioPlayer;
        }

        private readonly IGameContext _gameContext;
        private readonly IAudioPlayer _audioPlayer;

        public async UniTask Play(IReadOnlyLifetime lifetime, MatchCompletedData data)
        {
            // The player left the match or it failed: there are no results and no boards to reveal.
            if (data.Type is MatchResultType.Leave or MatchResultType.Failed)
                return;

            List<IBoardCell> mines = null;

            foreach (var boardState in data.Boards)
            {
                var board = _gameContext.GetPlayer(boardState.OwnerId).Board;
                var boardMines = Sync(board, boardState);

                if (boardState.OwnerId == data.LoserId)
                    mines = boardMines;
            }

            if (mines == null)
                throw new InvalidOperationException($"The match results have no board of the loser {data.LoserId}");

            // The board of the loser has no mines when it was never generated: the loser made no move.
            if (mines.Count == 0)
                return;

            Shuffle(mines);

            var options = GamePlayAssets.BoardsRevealOptions;
            await Explode(lifetime, mines, options);
            await UniTask.Delay(TimeSpan.FromSeconds(options.DelayBeforeResults), cancellationToken: lifetime.Token);
        }

        private static List<IBoardCell> Sync(IBoard board, BoardRevealState state)
        {
            var mines = new List<IBoardCell>();

            foreach (var cellState in state.Cells)
            {
                var cell = board.GetSyncedCell(state.OwnerId, cellState.Position);

                if (cellState.IsFree == true)
                {
                    cell.EnsureFree().OnMinesUpdated(cellState.MinesAround);
                    continue;
                }

                cell.EnsureTaken().OnFlagUpdated(cellState.IsFlagged);

                if (cellState.HasMine == true)
                    mines.Add(cell);
            }

            return mines;
        }

        private async UniTask Explode(IReadOnlyLifetime lifetime, IReadOnlyList<IBoardCell> mines, BoardsRevealOptions options)
        {
            var curve = options.ExplodedShareCurve;
            var animations = new List<UniTask>(mines.Count);
            var exploded = 0;
            var elapsed = 0f;

            while (exploded < mines.Count)
            {
                var progress = options.ExplosionTime > 0f ? Mathf.Clamp01(elapsed / options.ExplosionTime) : 1f;

                // On the last frame finish the remainder, even if the curve did not reach one.
                var target = progress >= 1f
                    ? mines.Count
                    : Mathf.Clamp(Mathf.CeilToInt(curve.Evaluate(progress) * mines.Count), exploded, mines.Count);

                if (target > exploded)
                {
                    for (; exploded < target; exploded++)
                        animations.Add(mines[exploded].Explode(CellExplosionType.Mine));

                    // One batch - one sound: otherwise simultaneous explosions clog the audio.
                    _audioPlayer.PlayRandomFromGroup(GamePlayAudio.GameMineNormal);
                }

                if (exploded >= mines.Count)
                    break;

                await UniTask.Yield(PlayerLoopTiming.Update, lifetime.Token);
                elapsed += Time.deltaTime;
            }

            await UniTask.WhenAll(animations);
        }

        private static void Shuffle(List<IBoardCell> cells)
        {
            for (var i = cells.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }
        }
    }
}
