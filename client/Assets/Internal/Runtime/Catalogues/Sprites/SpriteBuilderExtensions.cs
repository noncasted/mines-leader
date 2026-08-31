using Cysharp.Threading.Tasks;
using Internal;

namespace Internal {
    public static class SpriteBuilderExtensions {
        public static IScopeBuilder LoadSpriteGroup(this IScopeBuilder builder, SpriteGroup group) {
            builder.Events.AddBeforeBuild(group.Retain);
            builder.Events.AddBeforeDispose(() => {
                group.Release();
                return UniTask.CompletedTask;
            });

            return builder;
        }
    }
}
