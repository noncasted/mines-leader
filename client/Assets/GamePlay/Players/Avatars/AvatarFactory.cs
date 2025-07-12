using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace GamePlay.Players
{
    public interface IAvatarFactory
    {
        UniTask Create(PlayerBuildContext context);
    }
    
    [DisallowMultipleComponent]
    public class AvatarFactory : MonoBehaviour, IAvatarFactory
    {
        [SerializeField] private AvatarView _view;

        public UniTask Create(PlayerBuildContext context)
        {
            var builder = context.Builder;

            builder.RegisterComponent(_view)
                .As<IScopeSetup>()
                .AsSelfResolvable();

            builder.RegisterComponent(_view.MovesView)
                .As<IScopeLoaded>()
                .AsSelfResolvable();
            
            return UniTask.CompletedTask;
        }
    }
}