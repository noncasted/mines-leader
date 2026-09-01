using System;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;

namespace Internal
{
    public class EntityScopeLoader : IEntityScopeLoader
    {
        public async UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            LifetimeScope parent,
            IScopeEntityView view,
            Func<IEntityBuilder, UniTask> construct)
        {
            var builder = CreateBuilder(parentLifetime, view);

            await construct.Invoke(builder);
            await builder.Events.InvokeBeforeBuild();

            view.CreateViews(builder);

            BuildContainer(builder, parent);

            builder.Events.Bind(builder.Scope.Container);
            await builder.Events.RunConstruct(builder.ScopeLifetime);

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
            await builder.Events.InvokeBeforeBuild();
            view.CreateViews(builder);

            BuildContainer(builder, parent);

            builder.Events.Bind(builder.Scope.Container);
            await builder.Events.RunConstruct(builder.ScopeLifetime);

            return new EntityScopeResult(view.Scope, builder.ScopeLifetime);
        }

        private EntityBuilder CreateBuilder(IReadOnlyLifetime parentLifetime, IScopeEntityView view)
        {
            var lifetime = parentLifetime.Child();
            var services = new ServiceCollection();
            var builder = new EntityBuilder(services, view, lifetime, new EventLoop());

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
                builder.RegisterInstance(builder.Events);
                builder.Register<IViewInjector, ViewInjector>(VContainer.Lifetime.Scoped);

                builder.InternalServices.PassRegistrations(container);
            }
        }
    }
}