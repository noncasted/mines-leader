using GamePlay.Loop;
using Internal;
using TMPro;
using UnityEngine.UI;

namespace GamePlay.UI
{
    public class RoundTimer : IGameStarted
    {
        public RoundTimer(IGameRound round, RoundOverlayUIBindings roundOverlay)
        {
            _round = round;

            var bindings = roundOverlay.Center.Timer;
            _image = bindings.Image;
            _ownTimeText = bindings.Own.TextMeshProUGUI;
            _opponentTimeText = bindings.Opponent.TextMeshProUGUI;
        }

        private readonly Image _image;
        private readonly TMP_Text _ownTimeText;
        private readonly TMP_Text _opponentTimeText;

        private readonly IGameRound _round;

        public void OnGameStarted(IReadOnlyLifetime lifetime)
        {
            _round.Player.View(lifetime, _ => UpdateTurn());
            _round.RoundTime.View(lifetime, UpdateTime);

            return;

            void UpdateTurn()
            {
                var isOwnTurn = _round.IsTurnAllowed == true;

                _image.sprite = isOwnTurn == true ? Sprites.GameUIPlate.TimerOwn : Sprites.GameUIPlate.TimerOpponent;
                _ownTimeText.color = isOwnTurn == true ? Colors.Game.TextActive : Colors.Game.TextInactive;
                _opponentTimeText.color = isOwnTurn == true ? Colors.Game.TextInactive : Colors.Game.TextActive;
            }

            void UpdateTime(float time)
            {
                var text = ((int)time).ToString();

                if (_round.IsTurnAllowed == true)
                    _ownTimeText.text = text;
                else
                    _opponentTimeText.text = text;
            }
        }
    }
}
