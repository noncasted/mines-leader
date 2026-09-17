using Global.Audio;
using Internal;

namespace GamePlay.Loop
{
    // Смена хода и победа не привязаны к одному снапшоту, поэтому слушаются по состоянию.
    public class GameRoundAudio : IScopeSetup
    {
        public GameRoundAudio(
            IGameRound round,
            IGameState gameState,
            IGameContext gameContext,
            IAudioPlayer audioPlayer)
        {
            _round = round;
            _gameState = gameState;
            _gameContext = gameContext;
            _audioPlayer = audioPlayer;
        }

        private readonly IGameRound _round;
        private readonly IGameState _gameState;
        private readonly IGameContext _gameContext;
        private readonly IAudioPlayer _audioPlayer;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _round.Player.Advise(lifetime, player =>
            {
                if (player == null)
                    return;

                var sound = player == _gameContext.Self
                    ? GamePlayAudio.GameRoundOwn
                    : GamePlayAudio.GameRoundOpponent;

                _audioPlayer.PlaySound(sound);
            });

            _gameState.CompletedData.Advise(lifetime, data =>
            {
                if (data is { Type: MatchResultType.Win })
                    _audioPlayer.PlaySound(GamePlayAudio.GameRoundWin);
            });
        }
    }
}
