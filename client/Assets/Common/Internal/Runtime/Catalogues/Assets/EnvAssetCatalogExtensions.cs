using Cysharp.Threading.Tasks;

namespace Internal
{
    public static class EnvAssetCatalogExtensions
    {
        public static IScopeBuilder RequestEnvAssetGroup(this IScopeBuilder builder, EnvAssetGroup group)
        {
            return builder.RequestAssetGroup(group, "Assets");
        }

        public static UniTask LoadEnvAssetGroup(this IScopeBuilder builder, EnvAssetGroup group)
        {
            return builder.LoadAssetGroup(group, "Assets");
        }
    }
}
