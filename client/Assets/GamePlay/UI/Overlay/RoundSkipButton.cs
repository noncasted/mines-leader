using GamePlay.Loop;
using Internal;
using TMPro;
using UnityEngine.UI;

namespace GamePlay.UI
{
    public class RoundSkipButton : IGameStarted
    {
        public RoundSkipButton(IGameRound round, RoundOverlayUIBindings roundOverlay)
        {
            _round = round;
            
            var bindings = roundOverlay.Center.Skip;
            _image = bindings.Image;
            _text = bindings.TextTMP.TextMeshProUGUI;
            _button = bindings.Button;
        }

        private readonly Image _image;
        private readonly TMP_Text _text;
        private readonly Button _button;

        private readonly IGameRound _round;

        public void OnGameStarted(IReadOnlyLifetime lifetime)
        {
            _round.Player.View(lifetime, _ => UpdateImage());
            _button.ListenClick(lifetime, () => _round.TrySkip());

            return;

            void UpdateImage()
            {
                _image.sprite = _round.IsTurnAllowed == true
                    ? Sprites.GameUIPlate.SkipOwn
                    : Sprites.GameUIPlate.SkipOpponent;

                _text.color = _round.IsTurnAllowed == true ? Colors.Game.TextActive : Colors.Game.TextInactive;
            }
        }
    }
}