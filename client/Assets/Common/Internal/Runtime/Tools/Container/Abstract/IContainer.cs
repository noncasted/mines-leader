using System;
using System.Collections.Generic;

namespace Internal
{
    public interface IContainer : IDisposable
    {
        IContainerDiagnostics Diagnostics { get; }
        IReadOnlyLifetime Lifetime { get; }

        object Resolve(Type type);
        T Resolve<T>();
        bool TryResolve(Type type, out object instance);
        IReadOnlyList<T> ResolveAll<T>();

        void Inject(object target);
        void InjectGameObject(UnityEngine.GameObject target);
    }
}