using GamePlay.Players;
using GamePlay.UI;
using Global.Audio;
using Internal;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.Loop
{
    // Смена хода и победа приходят из ивент лупа: к одному снапшоту они не привязаны.
    public class GameRoundVisuals : IRoundChanged, IMatchCompleted
    {
        public GameRoundVisuals(
            IGameContext gameContext,
            IAudioPlayer audioPlayer,
            PlayerLocalPvPBindings ownPlayer,
            PlayerRemoteBaseBindings opponentPlayer,
            PlayersOverlayUIBindings playersOverlay)
        {
            _gameContext = gameContext;
            _audioPlayer = audioPlayer;

            _ownFrame = ownPlayer.BoardLocal.Frame.SpriteRenderer;
            _opponentFrame = opponentPlayer.BoardRemote.Frame.SpriteRenderer;

            _ownPlate = playersOverlay.Player.Image;
            _opponentPlate = playersOverlay.Opponent.Image;
        }

        private readonly IGameContext _gameContext;
        private readonly IAudioPlayer _audioPlayer;

        private readonly SpriteRenderer _ownFrame;
        private readonly SpriteRenderer _opponentFrame;
        private readonly Image _ownPlate;
        private readonly Image _opponentPlate;

        public void OnRoundChanged(IReadOnlyLifetime lifetime, IGamePlayer player)
        {
            Apply(_ownFrame, _ownPlate, player != null && player == _gameContext.Self);
            Apply(_opponentFrame, _opponentPlate, player != null && player == _gameContext.Other);

            if (player == null)
                return;

            var sound = player == _gameContext.Self
                ? GamePlayAudio.GameRoundOwn
                : GamePlayAudio.GameRoundOpponent;

            _audioPlayer.PlaySound(sound);
        }

        public void OnMatchCompleted(IReadOnlyLifetime lifetime, MatchCompletedData data)
        {
            if (data is { Type: MatchResultType.Win })
                _audioPlayer.PlaySound(GamePlayAudio.GameRoundWin);
        }

        // Подложка оверлея гаснет вместе с рамкой поля.
        private static void Apply(SpriteRenderer frame, Image plate, bool isActive)
        {
            frame.sprite = isActive == true ? Sprites.GameField.Active : Sprites.GameField.Invactive;
            plate.color = isActive == true ? Colors.Game.PlayerActive : Colors.Game.PlayerInactive;
        }
    }
}
