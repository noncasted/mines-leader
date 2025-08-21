using GamePlay.Loop;
using Global.UI;
using Internal;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class RoundButton : MonoBehaviour, ISceneService, IScopeSetup
    {
        [SerializeField] private Sprite _ownRound;
        [SerializeField] private Sprite _opponentRound;

        [SerializeField] private Image _image;
        [SerializeField] private TMP_Text _timeText;

        [SerializeField] private DesignButton _button;

        private IGameRound _round;

        [Inject]
        private void Construct(IGameRound round)
        {
            _round = round;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _round.Player.Advise(lifetime, Update);
            _round.RoundTime.View(lifetime, time => _timeText.text = ((int)time).ToString());
            
            _button.ListenClick(lifetime, () => _round.TrySkip());

            void Update()
            {
                if (_round.IsTurnAllowed == true)
                    _image.sprite = _ownRound;
                else
                    _image.sprite = _opponentRound;
            }
        }
    }
}