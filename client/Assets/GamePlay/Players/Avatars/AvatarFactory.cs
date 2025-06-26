using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace GamePlay.Players
{
    public interface IAvatarFactory
    {
        UniTask CreateLocal(PlayerBuildContext context);
        UniTask CreateRemote(PlayerBuildContext context);
    }
    
    [DisallowMultipleComponent]
    public class AvatarFactory : MonoBehaviour, IAvatarFactory
    {
        [SerializeField] private AvatarView _view;

        public UniTask CreateLocal(PlayerBuildContext context)
        {
            var builder = context.Builder;

            builder.RegisterComponent(_view)
                .As<IScopeSetup>()
                .AsSelfResolvable();

            builder.RegisterComponent(_view.TurnsView)
                .As<IScopeLoaded>()
                .AsSelfResolvable();
            
            return UniTask.CompletedTask;
        }

        public UniTask CreateRemote(PlayerBuildContext context)
        {
            var builder = context.Builder;

            builder.RegisterComponent(_view)
                .As<IScopeSetup>()
                .AsSelfResolvable();

            builder.RegisterComponent(_view.TurnsView)
                .As<IScopeLoaded>()
                .AsSelfResolvable();
            
            return UniTask.CompletedTask;
        }
    }
}