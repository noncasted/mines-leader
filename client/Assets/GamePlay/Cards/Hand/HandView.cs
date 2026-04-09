using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface IHandView
    {
        HandPositions Positions { get; }
    }

    [DisallowMultipleComponent]
    public class HandView : MonoBehaviour, IHandView, IEntityComponent
    {
        [SerializeField] private HandPositions _positions;

        public HandPositions Positions => _positions;

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IHandView>();

            builder.Register<Hand>()
                   .As<IHand>();

            builder.RegisterComponent(_positions)
                   .As<IScopeSetup>()
                   .AsSelfResolvable();
        }
    }
}