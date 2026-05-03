using UnityEngine.AddressableAssets;

namespace Tools.Runtime.SceneBuilder
{
    public class StaticScene
    {
        private readonly AssetReference _reference;

        public StaticScene(string guid)
        {
            _reference = new AssetReference(guid);
        }

        public AssetReference Value => _reference;
    }
}