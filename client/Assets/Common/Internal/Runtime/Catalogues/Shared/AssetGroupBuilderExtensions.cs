using Cysharp.Threading.Tasks;

namespace Internal
{
    public static class AssetGroupBuilderExtensions
    {
        // Группы ретейнятся одной пачкой перед сборкой контейнера, поэтому каждая
        // меряется своим параллельным отрезком: иначе весь этап — один чёрный ящик.
        public static IScopeBuilder RequestAssetGroup(this IScopeBuilder builder, AssetGroup group, string label)
        {
            builder.Events.AddBeforeBuild(() => GameProfiler
                                                .Concurrent($"{label}: {group.Name}")
                                                .Track(group.Retain()));
            builder.Events.AddBeforeDispose(() => ReleaseAsync(group));
            return builder;
        }

        // Construct runs before the build events, so a group used while registering must be loaded right away.
        // Отрезок берётся параллельным: группы грузятся пачкой через WhenAll, и стековый
        // Scope сложил бы соседей друг в друга вместо плоского списка.
        public static async UniTask LoadAssetGroup(this IScopeBuilder builder, AssetGroup group, string label)
        {
            await GameProfiler.Concurrent($"{label}: {group.Name}").Track(group.Retain());

            builder.Events.AddBeforeDispose(() => ReleaseAsync(group));
            ContainerRegistryDebug.RecordLoadedAsset(builder.Lifetime, label, group.Name);
        }

        private static UniTask ReleaseAsync(AssetGroup group)
        {
            group.Release();
            return UniTask.CompletedTask;
        }
    }
}