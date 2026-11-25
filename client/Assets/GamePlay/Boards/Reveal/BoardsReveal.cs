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

    // Конец матча: доски приводятся к серверному состоянию, после чего на поле
    // проигравшего по кривой из конфига подрываются все оставшиеся мины.
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
            // The player left the match: there are no results and no boards to reveal.
            if (data.Type == MatchResultType.Leave)
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

                // На последнем кадре добиваем остаток, даже если кривая не дошла до единицы.
                var target = progress >= 1f
                    ? mines.Count
                    : Mathf.Clamp(Mathf.CeilToInt(curve.Evaluate(progress) * mines.Count), exploded, mines.Count);

                if (target > exploded)
                {
                    for (; exploded < target; exploded++)
                        animations.Add(mines[exploded].Explode(CellExplosionType.Mine));

                    // Одна пачка — один звук: иначе одновременные взрывы забивают аудио.
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
