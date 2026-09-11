using System;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;
using Lifetime = Internal.Lifetime;

namespace Flow.Startup
{
    public class InternalScopeLoader
    {
        public async UniTask<ILoadedScope> Load()
        {
            using var stage = GameProfiler.Scope("Startup");

            // Каталог ассетов лежит в Resources целиком, поэтому грузится один раз здесь,
            // до первого обращения к любой группе.
            using (GameProfiler.Scope("Assets catalog"))
                AssetCatalog.Load();

            // Эти группы живут всё время работы приложения: глобальные префабы и спрайты меты,
            // которые из памяти уже не выгружаются. Ретейним один раз здесь и пачкой — бандлы
            // качаются параллельно, а не по очереди скоупов.
            using (GameProfiler.Scope("Assets"))
            {
                await UniTask.WhenAll(
                    RetainGroup(GlobalPrefabs.Group, "Prefabs"),
                    RetainGroup(Sprites.CardsIcons, "Sprites"),
                    RetainGroup(Sprites.CardBuffs, "Sprites"),
                    RetainGroup(Sprites.MenuPlay, "Sprites"),
                    RetainGroup(Sprites.Portraits, "Sprites"));
            }

            var lifetime = new Lifetime();
            IContainer container;

            using (GameProfiler.Scope("Container"))
            {
                Action<IBuilder> construct = InternalScopeExtensions.Construct;
                var rootId = GeneratedScopes.RootId(construct.Method);
                var containerBuilder = new ContainerBuilder(rootId, lifetime);
                var builder = new RootBuilder(containerBuilder, new EventLoop(), lifetime);

                construct.Invoke(builder);
                builder.RegisterInstance(builder.Events);

                container = ScopeContainer.Create(rootId, containerBuilder);
                builder.Events.Bind(container);
            }

            var result = new InternalLoadedScope(container, lifetime);

            Application.quitting += () => {
                Debug.Log("Internal scope lifetime terminated, disposing loaded scope.");
                result.Dispose();
            };

            return result;
        }

        private static UniTask RetainGroup(AssetGroup group, string label)
        {
            return GameProfiler.Concurrent($"{label}: {group.Name}").Track(group.Retain());
        }
    }
}