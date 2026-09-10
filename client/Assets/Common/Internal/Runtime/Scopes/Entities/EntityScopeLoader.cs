using System;
using System.Reflection;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IEntityScopeLoader
    {
        UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            IContainer parent,
            IScopeEntityView view,
            Func<IEntityBuilder, UniTask> construct);

        UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            IContainer parent,
            IScopeEntityView view,
            Action<IEntityBuilder> construct);
    }
    
    public class EntityScopeLoader : IEntityScopeLoader
    {
        public async UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            IContainer parent,
            IScopeEntityView view,
            Func<IEntityBuilder, UniTask> construct)
        {
            var builder = CreateBuilder(parentLifetime, parent, view);

            await construct.Invoke(builder);
            await builder.Events.InvokeBeforeBuild();

            view.CreateViews(builder);

            return await CreateContainer(builder, view, construct.Method);
        }

        public async UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            IContainer parent,
            IScopeEntityView view,
            Action<IEntityBuilder> construct)
        {
            var builder = CreateBuilder(parentLifetime, parent, view);

            construct.Invoke(builder);
            await builder.Events.InvokeBeforeBuild();

            view.CreateViews(builder);

            return await CreateContainer(builder, view, construct.Method);
        }

        private EntityBuilder CreateBuilder(IReadOnlyLifetime parentLifetime, IContainer parent, IScopeEntityView view)
        {
            var lifetime = parentLifetime.Child();
            var containerBuilder = new ContainerBuilder(view.GetType().Name, parent, lifetime);

            return new EntityBuilder(containerBuilder, view, lifetime, new EventLoop());
        }

        // Класс скоупа выбирается по корню и конкретному типу вьюхи: у варианта свой тип (locked 14).
        private async UniTask<IEntityScopeResult> CreateContainer(EntityBuilder builder, IScopeEntityView view, MethodInfo root)
        {
            builder.RegisterInstance(builder.Events);

            var container = ScopeContainer.CreateEntity(
                GeneratedScopes.RootId(root),
                view.GetType(),
                builder.ContainerBuilder); 

            builder.ScopeLifetime.Listen(container.Dispose);
            view.Bind(container);

            builder.Events.Bind(container);
            await builder.Events.RunConstruct(builder.ScopeLifetime);

            return new EntityScopeResult(container, builder.ScopeLifetime);
        }
    }
}
