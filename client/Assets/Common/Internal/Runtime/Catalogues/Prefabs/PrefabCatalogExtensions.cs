using Cysharp.Threading.Tasks;

namespace Internal {
    public static class PrefabCatalogExtensions {
        public static IScopeBuilder RequestPrefabGroup(this IScopeBuilder builder, PrefabGroup group) {
            return builder.RequestAssetGroup(group, "Prefabs");
        }

        public static UniTask LoadPrefabGroup(this IScopeBuilder builder, PrefabGroup group) {
            return builder.LoadAssetGroup(group, "Prefabs");
        }
    }
}
