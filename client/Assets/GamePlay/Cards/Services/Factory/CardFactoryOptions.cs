using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Cards
{
    [InlineEditor]
    public class CardFactoryOptions : EnvAsset
    {
        [SerializeField] private CardScopeEntity _localPrefab;
        [SerializeField] private CardScopeEntity _remotePrefab;
        
        public CardScopeEntity LocalPrefab => _localPrefab;
        public CardScopeEntity RemotePrefab => _remotePrefab;
    }
}