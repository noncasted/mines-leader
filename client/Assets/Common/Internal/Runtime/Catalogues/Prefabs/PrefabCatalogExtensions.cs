using Cysharp.Threading.Tasks;
using Internal;

namespace Internal {
    public static class PrefabCatalogExtensions {
        public static IScopeBuilder LoadPrefabGroup(this IScopeBuilder builder, PrefabGroup group) {
            builder.Events.AddBeforeBuild(group.Retain);
            builder.Events.AddBeforeDispose(() => {
                group.Release();
                return UniTask.CompletedTask;
            });

            return builder;
        }

        // Construct runs before the build events, so a group used while registering must be loaded right away.
        public static async UniTask LoadPrefabGroupNow(this IScopeBuilder builder, PrefabGroup group) {
            await group.Retain();

            builder.Events.AddBeforeDispose(() => {
                group.Release();
                return UniTask.CompletedTask;
            });
        }
    }
}
