using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Meta;
using UnityEngine;
using VContainer;

namespace Menu.Screens
{
    [DisallowMultipleComponent]
    public class MenuProgressionSelection : MonoBehaviour, ISceneService
    {
        [SerializeField] private MenuProgressionCard _cardPrefab;
        [SerializeField] private RectTransform _cardsRoot;
        
        private IViewInjector _injector;

        [Inject]
        private void Construct(IViewInjector injector)
        {
            _injector = injector;
        }
        
        public void Create(IScopeBuilder builder)
        {
            builder.Inject(this);
            gameObject.SetActive(false);
        }
        
        public async UniTask<ICardDefinition> Show(IReadOnlyLifetime lifetime, IReadOnlyList<ICardDefinition> definitions)
        {
            var completion = new UniTaskCompletionSource<ICardDefinition>();
            gameObject.SetActive(true);

            foreach (var definition in definitions)
            {
                var card = Instantiate(_cardPrefab, _cardsRoot);
                _injector.Inject(card);
                card.Setup(definition);
                
                card.Button.ListenClick(lifetime, () => completion.TrySetResult(definition));
            }
            
            var result = await completion.Task;

            gameObject.SetActive(false);

            return result;
        }
    }
}
