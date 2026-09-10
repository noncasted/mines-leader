using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public static class GeneratedScopes
    {
        private static readonly Dictionary<string, Func<GeneratedScopeRequest, IContainer>> _factories =
            new Dictionary<string, Func<GeneratedScopeRequest, IContainer>>(StringComparer.Ordinal);

        public static void Register(string rootId, Func<GeneratedScopeRequest, IContainer> factory)
        {
            if (string.IsNullOrEmpty(rootId) == true)
                throw new ArgumentException("Root id is required.", nameof(rootId));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _factories[rootId] = factory;
        }

        public static bool IsRegistered(string rootId)
        {
            if (string.IsNullOrEmpty(rootId) == true)
                return false;

            return _factories.ContainsKey(rootId);
        }

        public static bool Unregister(string rootId)
        {
            if (string.IsNullOrEmpty(rootId) == true)
                return false;

            return _factories.Remove(rootId);
        }

        internal static IContainer Create(string rootId, ContainerBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (_factories.TryGetValue(rootId, out var factory) == false)
            {
                throw new InvalidOperationException(
                    $"No generated container for '{rootId}'.");
            }

            var lifetime = builder.CreateLifetime();
            IContainer created = null;
            try
            {
                var request = new GeneratedScopeRequest(rootId, builder, builder.Parent, lifetime);
                created = factory.Invoke(request);
                if (created == null)
                {
                    throw new InvalidOperationException(
                        $"Generated factory for '{rootId}' returned null.");
                }

                builder.MarkBuilt();
                AttachCreated(created, builder.Parent);
                return created;
            }
            catch
            {
                if (created != null)
                    created.Dispose();
                else
                    lifetime.Terminate();

                throw;
            }
        }

        internal static void Clear()
        {
            _factories.Clear();
        }

        internal static Dictionary<Type, object> ReadExports(IContainer container)
        {
            if (container == null)
                return null;

            if (container is GeneratedContainer generated)
                return generated.Exports;

            var field = container.GetType().GetField(
                "_exports",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return null;

            return field.GetValue(container) as Dictionary<Type, object>;
        }

        internal static void AttachCreated(IContainer created, IContainer parent)
        {
            if (created == null)
                return;
            if (created is GeneratedContainer)
                return;

            var diagnostics = created.Diagnostics;
            if (parent is IContainerTree tree)
            {
                tree.AttachChild(created);
                if (created.Lifetime != null)
                    created.Lifetime.Listen(() => tree.DetachChild(created));
                return;
            }

            if (diagnostics == null)
                return;

            ContainerRegistryDebug.AddRoot(diagnostics);
            if (created.Lifetime != null)
                created.Lifetime.Listen(() => ContainerRegistryDebug.RemoveRoot(diagnostics));
        }
    }

    public sealed class GeneratedScopeRequest
    {
        internal GeneratedScopeRequest(
            string rootId,
            ContainerBuilder builder,
            IContainer parent,
            ILifetime lifetime)
        {
            RootId = rootId ?? string.Empty;
            _builder = builder;
            Parent = parent;
            Lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            Name = builder != null ? builder.Name : rootId;
        }

        private readonly ContainerBuilder _builder;

        public string RootId { get; }
        public string Name { get; }
        public IContainer Parent { get; }
        public ILifetime Lifetime { get; }

        public T Get<T>()
        {
            if (TryGet(typeof(T), out var instance) == false)
            {
                throw new InvalidOperationException(
                    $"No hole or instance for type {typeof(T).FullName}.");
            }

            return (T)instance;
        }

        public bool TryGet(Type type, out object instance)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (_builder != null && _builder.TryGetHole(type, out instance) == true)
                return true;

            if (Parent != null && Parent.TryResolve(type, out instance) == true)
                return true;

            instance = null;
            return false;
        }

        internal IReadOnlyList<LoadedAssetInfo> TakeLoadedAssets()
        {
            if (_builder == null)
                return Array.Empty<LoadedAssetInfo>();

            return _builder.TakeLoadedAssets(Lifetime);
        }
    }
}
