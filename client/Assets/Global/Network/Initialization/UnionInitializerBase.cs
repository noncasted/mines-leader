using Internal;

namespace Global.Network.Initialization
{
    public abstract class UnionInitializerBase : EnvAsset, IEnvAssetKeyOverride
    {
        public abstract void Init();

        public string GetKeyOverride()
        {
            return typeof(UnionInitializerBase).FullName;
        }
    }
}