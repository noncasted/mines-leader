using System;

namespace Internal
{
    public static class ScopeContainer
    {
        // Single choice: generated class if G registered this root, else runtime-plan Build().
        public static IContainer Create(
            string rootId,
            IContainer parent,
            IReadOnlyLifetime hostLifetime,
            Action<IContainerBuilderScope> configure)
        {
            ContainerThread.Assert();
            if (string.IsNullOrEmpty(rootId) == true)
                throw new ArgumentException("Root id is required.", nameof(rootId));

            var builder = parent != null
                ? new ContainerBuilder(rootId, parent)
                : new ContainerBuilder(rootId, hostLifetime);

            if (configure != null)
                configure.Invoke(builder);

            if (GeneratedScopes.IsRegistered(rootId) == true)
                return GeneratedScopes.Create(rootId, builder);

            return builder.Build();
        }

        public static IContainer Create(string rootId, Action<IContainerBuilderScope> configure)
        {
            return Create(rootId, parent: null, hostLifetime: null, configure);
        }

        public static IContainerBuilderScope CreateChild(IContainer parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            var name = "child";
            try
            {
                if (parent.Diagnostics != null && string.IsNullOrEmpty(parent.Diagnostics.Name) == false)
                    name = parent.Diagnostics.Name + "/child";
            }
            catch (Exception)
            {
                name = "child";
            }

            return new ContainerBuilder(name, parent);
        }
    }
}
