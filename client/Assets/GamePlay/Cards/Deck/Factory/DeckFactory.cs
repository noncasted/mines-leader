using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface IDeckFactory
    {
        UniTask CreateLocal(PlayerBuildContext context);
        UniTask CreateRemote(PlayerBuildContext context);
    }
    
    [DisallowMultipleComponent]
    public class DeckFactory : MonoBehaviour, IDeckFactory
    {
        [SerializeField] private DeckView _view;

        public UniTask CreateLocal(PlayerBuildContext context)
        {
            var builder = context.Builder;

            builder.RegisterProperty<DeckState>();
            
            builder.RegisterComponent(_view)
                .As<IDeckView>();

            builder.Register<LocalDeck>()
                .As<IDeck>()
                .As<IScopeSetup>();
            
            return UniTask.CompletedTask;
        }

        public UniTask CreateRemote(PlayerBuildContext context)
        {
            var builder = context.Builder;

            builder.RegisterProperty<DeckState>();
            
            builder.RegisterComponent(_view)
                .As<IDeckView>();

            builder.Register<RemoteDeck>()
                .As<IDeck>()
                .As<IScopeLoaded>();
            
            return UniTask.CompletedTask;
        }
    }
}