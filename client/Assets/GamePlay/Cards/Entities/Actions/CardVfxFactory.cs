using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Cards
{
    public interface ICardVfxFactory
    {
        T Create<T>(T prefab, Vector2 position, float angle = 0) where T : MonoBehaviour;
    }
    
    [DisallowMultipleComponent]
    public class CardVfxFactory : MonoBehaviour, ICardVfxFactory, ISceneService
    {
        private int _index;
        private IViewInjector _viewInjector;

        [Inject]
        private void Construct(IViewInjector viewInjector)
        {
            _viewInjector = viewInjector;
        }
        
        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<ICardVfxFactory>();
        }
        
        public T Create<T>(T prefab, Vector2 position, float angle = 0) where T : MonoBehaviour
        {
            if (prefab == null)
            {
                Debug.LogError("Prefab is null. Cannot create VFX.");
                return null;
            }

            _index++;
            
            var instance = Instantiate(prefab, position, Quaternion.Euler(0, 0, angle), transform);
            instance.name = $"{prefab.name}_{_index}";

            _viewInjector.Inject(instance);
            
            return instance;
        }
    }
}