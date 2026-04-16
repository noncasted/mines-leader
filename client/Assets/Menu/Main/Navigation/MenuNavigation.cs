using Global.Settings;
using Global.UI;
using Global.UI.Toolkit;
using Internal;
using Menu.Decks;
using Menu.Screens;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Menu.Main
{
    public interface IMenuNavigation
    {
        VisualElement Root { get; }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class MenuNavigation : MonoBehaviour, IMenuNavigation, ISceneService, IScopeSetup
    {
        private UIDocument _document;

        private IMenuDecks _decks;
        private IMenuProgression _progressionScreen;
        private IMenuHistory _historyScreen;
        private IUIStateMachine _stateMachine;
        private ISettings _settings;

        public VisualElement Root { get; private set; }

        [Inject]
        private void Construct(
            IMenuDecks decks,
            IMenuProgression progressionScreen,
            IMenuHistory historyScreen,
            IUIStateMachine stateMachine,
            ISettings settings)
        {
            _stateMachine = stateMachine;
            _progressionScreen = progressionScreen;
            _historyScreen = historyScreen;
            _decks = decks;
            _settings = settings;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IMenuNavigation>()
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _document = GetComponent<UIDocument>();
            Root = _document.rootVisualElement;

            var btnSettings = Root.Q<Button>("btn-settings");
            var btnCards = Root.Q<Button>("btn-cards");
            var btnProgression = Root.Q<Button>("btn-progression");
            var btnHistory = Root.Q<Button>("btn-history");

            btnSettings.ListenClick(lifetime, () => _settings.Open());
            btnCards.ListenClick(lifetime, () => _stateMachine.ProcessChild(_stateMachine.Base, _decks));

            btnProgression.ListenClick(lifetime,
                () => _stateMachine.ProcessChild(_stateMachine.Base, _progressionScreen));

            btnHistory.ListenClick(lifetime,
                () => _stateMachine.ProcessChild(_stateMachine.Base, _historyScreen));
        }
    }
}