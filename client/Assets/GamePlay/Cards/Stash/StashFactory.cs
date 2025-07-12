using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface IStashFactory
    {
        UniTask Create(PlayerBuildContext context);
    }
    
    [DisallowMultipleComponent]
    public class StashFactory : MonoBehaviour, IStashFactory
    {
        [SerializeField] private StashView _view;

        public UniTask Create(PlayerBuildContext context)
        {
            var builder = context.Builder;

            builder.RegisterProperty<PlayerStashState>(PlayerStateIds.Stash);
            
            builder.RegisterComponent(_view)
                .As<IStashView>();

            builder.Register<Stash>()
                .As<IScopeLoaded>()
                .As<IStash>()
                .AsSelfResolvable();
            
            return UniTask.CompletedTask;
        }
    }
}