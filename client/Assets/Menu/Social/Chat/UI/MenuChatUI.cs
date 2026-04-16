using Global.UI.Toolkit;
using Internal;
using Menu.Main;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Menu.Social
{
    public interface IMenuChatUI
    {
        bool IsSelected { get; }
        IViewableDelegate<string> MessageSend { get; }
    }

    [DisallowMultipleComponent]
    public class MenuChatUI : MonoBehaviour, IMenuChatUI, ISceneService, IScopeSetup
    {
        private IMenuNavigation _navigation;

        private readonly ViewableDelegate<string> _messageSend = new();
        private bool _isSelected;

        public bool IsSelected => _isSelected;
        public IViewableDelegate<string> MessageSend => _messageSend;

        [Inject]
        private void Construct(IMenuNavigation navigation)
        {
            _navigation = navigation;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IMenuChatUI>()
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            var root = _navigation.Root;
            var input = root.Q<TextField>("chat-input");

            void OnFocusIn(FocusInEvent evt) => _isSelected = true;
            void OnFocusOut(FocusOutEvent evt) => _isSelected = false;

            input.RegisterCallback<FocusInEvent>(OnFocusIn);
            input.RegisterCallback<FocusOutEvent>(OnFocusOut);

            lifetime.Listen(() => {
                input.UnregisterCallback<FocusInEvent>(OnFocusIn);
                input.UnregisterCallback<FocusOutEvent>(OnFocusOut);
            });

            input.ListenSubmit(lifetime, message => {
                _isSelected = false;
                _messageSend.Invoke(message);
            });
        }
    }
}