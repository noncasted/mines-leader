using Internal;
using UnityEngine;

namespace Global.UI
{
    public class LoadingScreenOptions : EnvAsset
    {
        [SerializeField] private LoadingScreen _prefab;
        
        public LoadingScreen Prefab => _prefab;
    }
}