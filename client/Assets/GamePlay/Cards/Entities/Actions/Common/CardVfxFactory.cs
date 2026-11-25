using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    [DisallowMultipleComponent]
    public class CardVfxFactory : MonoBehaviour, ICardVfxFactory, ISceneService
    {
        private CardVfxPools _pools;

        [Inject]
        internal void Construct(IViewInjector viewInjector)
        {
            _pools = new CardVfxPools(this.GetObjectLifetime(), transform, viewInjector);
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<ICardVfxFactory>();
        }

        public T Create<T>(IReadOnlyLifetime lifetime, T prefab, Vector2 position, float angle = 0) where T : MonoBehaviour
        {
            return _pools.Get(lifetime, prefab, position, angle);
        }
    }
}
