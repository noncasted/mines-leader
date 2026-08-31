using Global.Publisher.Itch;
using Internal;
using UnityEngine;

namespace Global.Publisher
{
    public class GlobalPublisherOptions : EnvAsset
    {
        [SerializeField] private ItchCallbacks _itchCallbacksPrefab;

        public ItchCallbacks ItchCallbacksPrefab => _itchCallbacksPrefab;
    }
}