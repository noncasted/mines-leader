using GamePlay.Cards;
using GamePlay.Players;
using Internal;
using UnityEngine;

namespace GamePlay.Prefabs
{
    public class GamePrefabs : EnvAsset
    {
        [SerializeField] private StashCard _stashCard;
        [SerializeField] private CardScopeEntity _cardLocal;
        [SerializeField] private CardScopeEntity _cardRemote;
        [SerializeField] private AvatarTurnPointView _avatarTurnPoint;
        [SerializeField] private PlayerManaPointView _manaPoint;
        [SerializeField] private DeckCard _deckCard;
        
        public StashCard StashCard => _stashCard;
        public CardScopeEntity CardLocal => _cardLocal;
        public CardScopeEntity CardRemote => _cardRemote;
        public AvatarTurnPointView AvatarTurnPoint => _avatarTurnPoint;
        public PlayerManaPointView ManaPoint => _manaPoint;
        public DeckCard DeckCard => _deckCard;
    }
}