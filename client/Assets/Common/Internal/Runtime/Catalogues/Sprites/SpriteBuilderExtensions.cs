using Cysharp.Threading.Tasks;
using Internal;

namespace Internal {
    public static class SpriteBuilderExtensions {
        public static IScopeBuilder LoadSpriteGroup(this IScopeBuilder builder, SpriteGroup group) {
            // Спрайт-группы ретейнятся пачкой перед сборкой контейнера — меряем каждую отдельно.
            builder.Events.AddBeforeBuild(() => GameProfiler
                                                .Concurrent($"Sprites: {group.GetType().Name}")
                                                .Track(group.Retain()));
            builder.Events.AddBeforeDispose(() => {
                group.Release();
                return UniTask.CompletedTask;
            });

            return builder;
        }
    }
}
