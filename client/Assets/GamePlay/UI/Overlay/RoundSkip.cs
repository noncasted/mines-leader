using GamePlay.Loop;
using Internal;
using TMPro;
using Tools;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class RoundSkip : MonoBehaviour, ISceneService, IGameStarted
    {
        [SerializeField] private Image _image;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Button _button;
        [SerializeField] private Sprite _ownSprite;
        [SerializeField] private Sprite _opponentSprite;

        private IGameRound _round;

        [Inject]
        internal void Construct(IGameRound round)
        {
            _round = round;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IGameStarted>();
        }

        public void OnGameStarted(IReadOnlyLifetime lifetime)
        {
            _round.Player.View(lifetime, _ => UpdateImage());
            _button.ListenClick(lifetime, () => _round.TrySkip());

            return;

            void UpdateImage()
            {
                _image.sprite = _round.IsTurnAllowed == true ? _ownSprite : _opponentSprite;
                _text.color = _round.IsTurnAllowed == true ? Colors.Game.TextActive : Colors.Game.TextInactive;
            }
        }
    }
}