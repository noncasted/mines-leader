using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface IDeckFactory
    {
        UniTask Create(PlayerBuildContext context);
    }
    
    [DisallowMultipleComponent]
    public class DeckFactory : MonoBehaviour, IDeckFactory
    {
        [SerializeField] private DeckView _view;

        public UniTask Create(PlayerBuildContext context)
        {
            var builder = context.Builder;

            builder.RegisterProperty<PlayerDeckState>(PlayerStateIds.Deck);
            
            builder.RegisterComponent(_view)
                .As<IDeckView>();

            builder.Register<Deck>()
                .As<IScopeLoaded>();
            
            return UniTask.CompletedTask;
        }
    }
}