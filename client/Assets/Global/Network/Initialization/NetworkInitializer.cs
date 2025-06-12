using Internal;

namespace Global.Network.Initialization
{
    public class NetworkInitializer : IScopeBaseSetup
    {
        public NetworkInitializer(IAssetEnvironment assets)
        {
            _assets = assets;
        }

        private readonly IAssetEnvironment _assets;
        
        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            var unions = _assets.GetAsset<UnionInitializerBase>();
            unions.Init();
        }
    }
}