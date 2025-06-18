using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Players
{
    [InlineEditor]
    public class GamePlayerFactoryOptions : EnvAsset
    {
        [SerializeField] private GamePlayerEntityView _localPrefab;
        [SerializeField] private GamePlayerEntityView _remotePrefab;
        
        public GamePlayerEntityView LocalPrefab => _localPrefab;
        public GamePlayerEntityView RemotePrefab => _remotePrefab;
    }
}