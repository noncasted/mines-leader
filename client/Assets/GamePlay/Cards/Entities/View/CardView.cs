using Internal;
using UnityEngine;
using VContainer.Unity;

namespace GamePlay.Cards
{
    public interface ICardView
    {
        void Destroy();
    }

    [DisallowMultipleComponent]
    public class CardView : MonoBehaviour, ICardView, IEntityComponent
    {
        [SerializeField] private LifetimeScope _scope;

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<ICardView>();
        }

        public void Destroy()
        {
            _scope.DisposeCore();
            
            if (gameObject != null)
                Destroy(gameObject);
        }
    }
}