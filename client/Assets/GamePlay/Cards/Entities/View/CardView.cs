using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardView
    {
        void Destroy();
    }

    [DisallowMultipleComponent]
    public class CardView : MonoBehaviour, ICardView, IEntityComponent
    {
        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<ICardView>();
        }

        public void Destroy()
        {
            GetComponentInParent<ScopeEntityView>()?.Dispose();

            if (gameObject != null)
                Destroy(gameObject);
        }
    }
}