using System;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;

namespace Internal
{
    public class EntityScopeLoader : IEntityScopeLoader
    {
        public EntityScopeLoader(IAssetEnvironment assets)
        {
            _assets = assets;
        }

        private readonly IAssetEnvironment _assets;

        public async UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            LifetimeScope parent,
            IScopeEntityView view,
            Func<IEntityBuilder, UniTask> construct)
        {
            var builder = CreateBuilder(parentLifetime, view);

            await construct.Invoke(builder);
            
            view.CreateViews(builder);

            BuildContainer(builder, parent);

            var eventLoop = builder.Scope.Container.Resolve<IEventLoop>();
            await eventLoop.RunConstruct(builder.ScopeLifetime);

            return new EntityScopeResult(view.Scope, builder.ScopeLifetime);
        }

        public async UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            LifetimeScope parent,
            IScopeEntityView view,
            Action<IEntityBuilder> construct)
        {
            var builder = CreateBuilder(parentLifetime, view);

            construct.Invoke(builder);
            view.CreateViews(builder);

            BuildContainer(builder, parent);

            var eventLoop = builder.Scope.Container.Resolve<IEventLoop>();
            await eventLoop.RunConstruct(builder.ScopeLifetime);

            return new EntityScopeResult(view.Scope, builder.ScopeLifetime);
        }

        private EntityBuilder CreateBuilder(IReadOnlyLifetime parentLifetime, IScopeEntityView view)
        {
            var lifetime = parentLifetime.Child();
            var services = new ServiceCollection();
            var builder = new EntityBuilder(services, view, lifetime, _assets, new ScopeEventListeners());

            return builder;
        }

        private void BuildContainer(EntityBuilder builder, LifetimeScope parent)
        {
            using (LifetimeScope.EnqueueParent(parent))
            {
                using (LifetimeScope.Enqueue(Register))
                {
                    builder.Scope.Build();
                }
            }

            builder.InternalServices.Resolve(builder.Scope.Container);

            return;

            void Register(IContainerBuilder container)
            {
                builder.AddEvents();
                builder.Register<IViewInjector, ViewInjector>(VContainer.Lifetime.Scoped);

                builder.InternalServices.PassRegistrations(container);
            }
        }
    }
}