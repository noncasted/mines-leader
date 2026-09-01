using Cysharp.Threading.Tasks;

namespace Internal {
    public static class SpriteBuilderExtensions {
        public static IScopeBuilder LoadSpriteGroup(this IScopeBuilder builder, SpriteGroup group) {
            return builder.LoadAssetGroup(group, "Sprites");
        }

        public static UniTask LoadSpriteGroupNow(this IScopeBuilder builder, SpriteGroup group) {
            return builder.LoadAssetGroupNow(group, "Sprites");
        }
    }
}
