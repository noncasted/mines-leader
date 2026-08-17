using Internal;
using UnityEngine;
using VContainer.Unity;

namespace Loop.Startup
{
    [DisallowMultipleComponent]
    public class InternalScope : LifetimeScope
    {
        private ILoadedScope _scope;

        public void AttachScope(ILoadedScope scope)
        {
            _scope = scope;
        }

        protected override void OnDestroy()
        {
            _scope.Dispose();
        }
    }
}