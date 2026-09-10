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

            // The whole Global prefab group lives for the application lifetime, so it is retained once here.
            using (GameProfiler.Scope("Prefabs: Global"))
                await GlobalPrefabs.Group.Retain();

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
    }
}