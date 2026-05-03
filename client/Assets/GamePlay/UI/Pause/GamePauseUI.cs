using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.Settings;
using Global.UI;
using Global.UI.Toolkit;
using Internal;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace GamePlay.UI
{
    public interface IGamePause
    {
        void Open();
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class GamePauseUI : MonoBehaviour, IScopeSetup, ISceneService, IGamePause
    {
        private ISettings _settings;
        private IGameState _gameState;

        [Inject]
        private void Construct(ISettings settings, IGameState gameState)
        {
            _gameState = gameState;
            _settings = settings;
            gameObject.SetActive(true);
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IGamePause>()
                   .As<IScopeSetup>();
        }

        public void Open()
        {
            gameObject.SetActive(true);
            var document = GetComponent<UnityEngine.UIElements.UIDocument>();
            document.rootVisualElement.style.display = DisplayStyle.Flex;
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            var document = GetComponent<UIDocument>();
            var root = document.rootVisualElement;
            root.style.display = DisplayStyle.None;

            var continueBtn = root.Q<Button>("btn-continue");
            var settingsBtn = root.Q<Button>("btn-settings");
            var leaveBtn = root.Q<Button>("btn-leave");

            continueBtn.ListenClick(lifetime, () =>
            {
                root.style.display = DisplayStyle.None;
            });
            settingsBtn.ListenClick(lifetime, () => _settings.Open());
            leaveBtn.ListenClick(lifetime, () => ProcessLeave(lifetime).Forget());
        }

        private async UniTask ProcessLeave(IReadOnlyLifetime lifetime)
        {
            var document = GetComponent<UIDocument>();
            var root = document.rootVisualElement;

            var overlay = new VisualElement();
            overlay.AddToClassList("leave-overlay");

            var panel = new VisualElement();
            panel.AddToClassList("leave-panel");

            var label = new Label("are you sure?");
            label.AddToClassList("leave-label");
            panel.Add(label);

            var buttonsRow = new VisualElement();
            buttonsRow.AddToClassList("leave-buttons");

            var completion = new UniTaskCompletionSource<bool>();
            var menuLifetime = lifetime.Child();

            var yesBtn = new NavButton { text = "yes" };
            yesBtn.ListenClick(menuLifetime, () => completion.TrySetResult(true));

            var noBtn = new NavButton { text = "no" };
            noBtn.ListenClick(menuLifetime, () => completion.TrySetResult(false));

            buttonsRow.Add(yesBtn);
            buttonsRow.Add(noBtn);
            panel.Add(buttonsRow);
            overlay.Add(panel);
            root.Add(overlay);

            var result = await completion.Task;

            menuLifetime.Terminate();
            root.Remove(overlay);

            if (result == true)
                _gameState.OnLeave();
        }
    }
}
