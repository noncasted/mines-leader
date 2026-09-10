using System;
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
}
