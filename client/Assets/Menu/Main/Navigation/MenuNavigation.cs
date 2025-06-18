using Global.UI;
using Internal;
using Menu.Decks;
using UnityEngine;
using VContainer;

namespace Menu.Main
{
    public interface IMenuNavigation
    {
        
    }
    
    [DisallowMultipleComponent]
    public class MenuNavigation : MonoBehaviour, IMenuNavigation, ISceneService, IScopeSetup
    {
        [SerializeField] private DesignButton _settings;
        [SerializeField] private DesignButton _cards;
        [SerializeField] private DesignButton _shop;

        private IMenuDecks _decks;
        private IUIStateMachine _stateMachine;

        [Inject]
        private void Construct(IMenuDecks decks, IUIStateMachine stateMachine)
        {
            _stateMachine = stateMachine;
            _decks = decks;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IMenuNavigation>()
                .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _cards.Clicked.Advise(lifetime, () => _stateMachine.ProcessChild(_stateMachine.Base, _decks));
        }
    }
}