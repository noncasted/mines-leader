using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface IStashFactory
    {
        UniTask CreateLocal(PlayerBuildContext context);
        UniTask CreateRemote(PlayerBuildContext context);
    }
    
    [DisallowMultipleComponent]
    public class StashFactory : MonoBehaviour, IStashFactory
    {
        [SerializeField] private StashView _view;

        public UniTask CreateLocal(PlayerBuildContext context)
        {
            var builder = context.Builder;

            builder.RegisterProperty<StashState>();
            
            builder.RegisterComponent(_view)
                .As<IStashView>();

            builder.Register<LocalStash>()
                .As<IStash>()
                .AsSelfResolvable();
            
            return UniTask.CompletedTask;
        }

        public UniTask CreateRemote(PlayerBuildContext context)
        {
            var builder = context.Builder;

            builder.RegisterProperty<StashState>();
            
            builder.RegisterComponent(_view)
                .As<IStashView>();

            builder.Register<RemoteStash>()
                .As<IStash>()
                .As<IScopeLoaded>();
            
            return UniTask.CompletedTask;
        }
    }
}