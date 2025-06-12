using GamePlay.Boards;
using GamePlay.Cards;
using Internal;
using UnityEngine;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class GamePlayerEntityView : ScopeEntityView
    {
        [SerializeField] private DeckFactory _deckFactory;
        [SerializeField] private BoardFactory _boardFactory;
        [SerializeField] private HandFactory _handFactory;
        [SerializeField] private StashFactory _stashFactory;
        [SerializeField] private AvatarFactory _avatarFactory;
        
        public IDeckFactory DeckFactory => _deckFactory;
        public IBoardFactory BoardFactory => _boardFactory;
        public IHandFactory HandFactory => _handFactory;
        public IStashFactory StashFactory => _stashFactory;
        public IAvatarFactory AvatarFactory => _avatarFactory;
    }
}