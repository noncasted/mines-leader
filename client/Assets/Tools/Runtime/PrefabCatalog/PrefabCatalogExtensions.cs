using Cysharp.Threading.Tasks;
using Internal;

namespace Tools {
    public static class PrefabCatalogExtensions {
        public static IScopeBuilder LoadPrefabGroup(this IScopeBuilder builder, PrefabGroup group) {
            builder.Events.AddBeforeBuild(group.Retain);
            builder.Events.AddBeforeDispose(() => {
                group.Release();
                return UniTask.CompletedTask;
            });

            return builder;
        }
    }
}
