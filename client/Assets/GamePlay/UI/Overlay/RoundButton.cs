using GamePlay.Loop;
using Internal;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using Cursor = UnityEngine.UIElements.Cursor;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class RoundButton : MonoBehaviour, ISceneService, IScopeSetup
    {
        [SerializeField] private Sprite _ownRound;
        [SerializeField] private Sprite _opponentRound;

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
            var document = GetComponent<UIDocument>();
            if (document == null)
            {
                Debug.LogError("[RoundButton] UIDocument not found");
                return;
            }

            var root = document.rootVisualElement;
            var roundButton = root.Q<VisualElement>("round-button");
            var roundTime = root.Q<Label>("round-time");

            if (roundButton == null || roundTime == null)
            {
                Debug.LogError("[RoundButton] Required elements not found in UXML");
                return;
            }

            // Make button clickable with hover cursor
            roundButton.AddManipulator(new Clickable(_ => _round.TrySkip()));
            roundButton.style.cursor = new StyleCursor(new Cursor { texture = null });

            _round.Player.Advise(lifetime, _ => UpdateSprite(roundButton));
            _round.RoundTime.View(lifetime, time => roundTime.text = ((int)time).ToString());
        }

        private void UpdateSprite(VisualElement roundButton)
        {
            try
            {
                if (_round.IsTurnAllowed)
                    roundButton.style.backgroundImage = new StyleBackground(_ownRound);
                else
                    roundButton.style.backgroundImage = new StyleBackground(_opponentRound);
            }
            catch (System.NullReferenceException)
            {
                // _gameContext.Self not yet initialized — skip until first Player update
            }
        }
    }
}