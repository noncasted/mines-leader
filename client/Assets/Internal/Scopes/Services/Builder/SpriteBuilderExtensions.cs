using Cysharp.Threading.Tasks;
using Tools;

namespace Internal {
    public static class SpriteBuilderExtensions {
        public static async UniTask LoadSpriteGroup(this IScopeBuilder builder, SpriteGroup group) {
            await group.Retain();
            builder.Lifetime.Listen(() => group.Release());
        }
    }
}
