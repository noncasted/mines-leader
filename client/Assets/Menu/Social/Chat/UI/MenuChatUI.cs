using Internal;
using TMPro;
using UnityEngine;

namespace Menu.Social
{
    public interface IMenuChatUI
    {
        bool IsSelected { get; }
        IViewableDelegate<string> MessageSend { get; }
    }
    
    [DisallowMultipleComponent]
    public class MenuChatUI : MonoBehaviour, IMenuChatUI, ISceneService
    {
        [SerializeField] private TMP_InputField _input;
        
        private readonly ViewableDelegate<string> _messageSend = new();

        private bool _isSelected;

        public bool IsSelected => _isSelected;
        public IViewableDelegate<string> MessageSend => _messageSend;

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IMenuChatUI>();
        }
        
        private void OnEnable()
        {
            var lifetime = this.GetObjectLifetime();
            _input.onSubmit.Listen(lifetime, OnSubmit);
            _input.onSelect.Listen(lifetime, aa => _isSelected = true);
            _input.onDeselect.Listen(lifetime, aa => _isSelected = false);
        }

        private void OnSubmit(string message)
        {
            if (message == string.Empty || message == " ")
                return;

            _input.text = string.Empty;
            _isSelected = false;
            
            _messageSend.Invoke(message);
        }
    }
}