using Cysharp.Threading.Tasks;

namespace Internal {
    public static class PrefabCatalogExtensions {
        public static IScopeBuilder LoadPrefabGroup(this IScopeBuilder builder, PrefabGroup group) {
            return builder.LoadAssetGroup(group, "Prefabs");
        }

        public static UniTask LoadPrefabGroupNow(this IScopeBuilder builder, PrefabGroup group) {
            return builder.LoadAssetGroupNow(group, "Prefabs");
        }
    }
}
