using System;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace Internal
{
    public interface IEntityScopeLoader
    {
        UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            LifetimeScope parent,
            IScopeEntityView view,
            Func<IEntityBuilder, UniTask> construct);
        
        UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            LifetimeScope parent,
            IScopeEntityView view,
            Action<IEntityBuilder> construct);
    }
}