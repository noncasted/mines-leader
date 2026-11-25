using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.Audio;
using Internal;
using Shared;

namespace GamePlay.Boards
{
    public class BoardCellsAnimator : IBoardCellsAnimator
    {
        public BoardCellsAnimator(IGameContext gameContext, IAudioPlayer audioPlayer)
        {
            _gameContext = gameContext;
            _audioPlayer = audioPlayer;
        }

        private readonly IGameContext _gameContext;
        private readonly IAudioPlayer _audioPlayer;

        public async UniTask PlayTargetAnimation(IReadOnlyLifetime lifetime, Guid targetPlayer, IReadOnlyList<Position> positions)
        {
            var board = _gameContext.GetPlayer(targetPlayer).Board;
            var tasks = new List<UniTask>();

            foreach (var position in positions)
            {
                var cell = board.GetSyncedCell(targetPlayer, position);
                tasks.Add(cell.Visuals.PlaySelection(lifetime));
            }

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks);
        }

        public async UniTask PlayActionAnimation(IReadOnlyLifetime lifetime, Guid targetPlayer, IReadOnlyList<Position> positions)
        {
            var board = _gameContext.GetPlayer(targetPlayer).Board;
            var tasks = new List<UniTask>();

            foreach (var position in positions)
            {
                var cell = board.GetSyncedCell(targetPlayer, position);
                tasks.Add(cell.Visuals.PlayCellAction(lifetime));
            }

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks);
        }

        public UniTask OpenCells(IReadOnlyLifetime lifetime, Guid targetPlayer, IReadOnlyList<OpenedCell> cells)
        {
            var board = _gameContext.GetPlayer(targetPlayer).Board;

            foreach (var opened in cells)
            {
                var cell = board.GetSyncedCell(targetPlayer, opened.Position);
                cell.EnsureFree().OnMinesUpdated(opened.MinesAround);
            }

            return UniTask.CompletedTask;
        }

        public void AddEffect(Guid effectId, CellEffectType type, Guid targetPlayer, Position position)
        {
            var board = _gameContext.GetPlayer(targetPlayer).Board;
            var cell = board.GetSyncedCell(targetPlayer, position);

            cell.Effects.Add(effectId, type);
        }

        public void FlagCell(Guid targetPlayer, Position position)
        {
            var board = _gameContext.GetPlayer(targetPlayer).Board;
            var cell = board.GetSyncedCell(targetPlayer, position);

            cell.EnsureTaken().OnFlagUpdated(true);
        }

        public void UnflagCell(Guid targetPlayer, Position position)
        {
            var board = _gameContext.GetPlayer(targetPlayer).Board;
            var cell = board.GetSyncedCell(targetPlayer, position);

            cell.EnsureTaken().OnFlagUpdated(false);
        }

        public void EnsureTaken(Guid targetPlayer, Position position)
        {
            var board = _gameContext.GetPlayer(targetPlayer).Board;
            var cell = board.GetSyncedCell(targetPlayer, position);

            cell.EnsureTaken();
        }

        public UniTask ExplodeCell(Guid targetPlayer, Position position, CellExplosionType type)
        {
            var board = _gameContext.GetPlayer(targetPlayer).Board;
            var cell = board.GetSyncedCell(targetPlayer, position);

            var sound = type == CellExplosionType.ZipZap
                ? GamePlayAudio.GameMineElectric
                : GamePlayAudio.GameMineNormal;

            _audioPlayer.PlayRandomFromGroup(sound);

            return cell.Explode(type);
        }
    }
}
