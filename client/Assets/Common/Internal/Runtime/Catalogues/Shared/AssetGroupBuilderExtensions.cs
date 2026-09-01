using Cysharp.Threading.Tasks;

namespace Internal {
    public static class AssetGroupBuilderExtensions {
        // Группы ретейнятся одной пачкой перед сборкой контейнера, поэтому каждая
        // меряется своим параллельным отрезком: иначе весь этап — один чёрный ящик.
        public static IScopeBuilder LoadAssetGroup(this IScopeBuilder builder, AssetGroup group, string label) {
            builder.Events.AddBeforeBuild(() => GameProfiler
                                                .Concurrent($"{label}: {group.Name}")
                                                .Track(group.Retain()));
            builder.Events.AddBeforeDispose(() => ReleaseAsync(group));
            return builder;
        }

        // Construct runs before the build events, so a group used while registering must be loaded right away.
        public static async UniTask LoadAssetGroupNow(this IScopeBuilder builder, AssetGroup group, string label) {
            using (GameProfiler.Scope($"{label}: {group.Name}"))
                await group.Retain();

            builder.Events.AddBeforeDispose(() => ReleaseAsync(group));
        }

        private static UniTask ReleaseAsync(AssetGroup group) {
            group.Release();
            return UniTask.CompletedTask;
        }
    }
}
