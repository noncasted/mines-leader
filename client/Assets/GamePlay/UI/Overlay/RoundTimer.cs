using GamePlay.Loop;
using Internal;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class RoundTimer : MonoBehaviour, ISceneService, IGameStarted
    {
        [SerializeField] private Image _image;
        [SerializeField] private TMP_Text _ownTimeText;
        [SerializeField] private TMP_Text _opponentTimeText;
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
            _round.Player.View(lifetime, _ => UpdateTurn());
            _round.RoundTime.View(lifetime, UpdateTime);

            return;
            
            void UpdateTurn()
            {
                var isOwnTurn = _round.IsTurnAllowed == true;

                _image.sprite = isOwnTurn == true ? _ownSprite : _opponentSprite;
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
