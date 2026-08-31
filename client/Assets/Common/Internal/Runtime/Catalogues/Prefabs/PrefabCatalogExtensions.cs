using Cysharp.Threading.Tasks;
using Internal;

namespace Internal {
    public static class PrefabCatalogExtensions {
        public static IScopeBuilder LoadPrefabGroup(this IScopeBuilder builder, PrefabGroup group) {
            // Группы ретейнятся одной пачкой перед сборкой контейнера, поэтому каждая
            // меряется своим параллельным отрезком: иначе весь этап — один чёрный ящик.
            builder.Events.AddBeforeBuild(() => GameProfiler
                                                .Concurrent($"Prefabs: {group.GetType().Name}")
                                                .Track(group.Retain()));
            builder.Events.AddBeforeDispose(() => {
                group.Release();
                return UniTask.CompletedTask;
            });

            return builder;
        }

        // Construct runs before the build events, so a group used while registering must be loaded right away.
        public static async UniTask LoadPrefabGroupNow(this IScopeBuilder builder, PrefabGroup group) {
            using (GameProfiler.Scope($"Prefabs: {group.GetType().Name}"))
                await group.Retain();

            builder.Events.AddBeforeDispose(() => {
                group.Release();
                return UniTask.CompletedTask;
            });
        }
    }
}
