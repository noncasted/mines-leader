using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface IHandFactory
    {
        UniTask Create(PlayerBuildContext context);
    }
    
    [DisallowMultipleComponent]
    public class HandFactory : MonoBehaviour, IHandFactory
    {
        [SerializeField] private HandView _view;

        public UniTask Create(PlayerBuildContext context)
        {
            var builder = context.Builder;
    
            builder.RegisterComponent(_view)
                .As<IHandView>();

            builder.Register<Hand>()
                .As<IHand>();
            
            builder.RegisterComponent(_view.Positions)
                .As<IScopeSetup>()
                .AsSelfResolvable();
            
            return UniTask.CompletedTask;
        }
    }
}