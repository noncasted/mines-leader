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

        // Загрузчик сущностей спрашивает id на каждую карту, а для метода и типа вьюхи он не меняется:
        // строки собираются один раз (было ~470 + ~300 байт на загрузку). Ключ корня — хэндл метода:
        // объект MethodInfo у разных делегатов одного метода может быть разным.
        private static readonly Dictionary<IntPtr, string> _rootIds = new Dictionary<IntPtr, string>();
        private static readonly Dictionary<(string, Type), string> _variantKeys = new Dictionary<(string, Type), string>();

        public static void Register(string rootId, Func<GeneratedScopeRequest, IContainer> factory)
        {
            if (string.IsNullOrEmpty(rootId) == true)
                throw new ArgumentException("Root id is required.", nameof(rootId));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            if (_factories.ContainsKey(rootId) == true)
            {
                throw new InvalidOperationException(
                    "Generated scope '" + rootId + "' is already registered.");
            }

            _factories.Add(rootId, factory);
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

        public static void RegisterVariant(
            string rootId,
            Type viewType,
            Func<GeneratedScopeRequest, IContainer> factory)
        {
            if (viewType == null)
                throw new ArgumentNullException(nameof(viewType));

            Register(VariantKey(rootId, viewType), factory);
        }

        public static string VariantKey(string rootId, Type viewType)
        {
            if (_variantKeys.TryGetValue((rootId, viewType), out var key) == false)
            {
                key = rootId + "@" + TypeName(viewType);
                _variantKeys.Add((rootId, viewType), key);
            }

            return key;
        }

        /// <summary>
        /// Id корня в том виде, в каком его пишет генератор: тип через точки и метод,
        /// у локальной функции — "+Имя" после внешнего метода. Лямбда корнем быть не может:
        /// по ней не найти класс скоупа.
        /// </summary>
        public static string RootId(MethodInfo method)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            var handle = method.MethodHandle.Value;
            if (_rootIds.TryGetValue(handle, out var id) == false)
            {
                id = BuildRootId(method);
                _rootIds.Add(handle, id);
            }

            return id;
        }

        private static string BuildRootId(MethodInfo method)
        {
            var type = method.DeclaringType;
            var name = method.Name;
            if (name.Length == 0 || name[0] != '<')
                return TypeName(type) + "." + name;

            var close = name.IndexOf('>');
            var marker = close < 0 ? -1 : name.IndexOf("g__", close, StringComparison.Ordinal);
            var bar = marker < 0 ? -1 : name.IndexOf('|', marker);
            if (close < 0 || marker != close + 1 || bar < 0)
            {
                throw new ArgumentException(
                    "Scope root must be a method group or a local function, not a lambda: " + name,
                    nameof(method));
            }

            var outer = name.Substring(1, close - 1);
            var local = name.Substring(marker + 3, bar - marker - 3);
            while (type != null && type.Name.Length > 0 && type.Name[0] == '<')
                type = type.DeclaringType;

            return TypeName(type) + "." + outer + "+" + local;
        }

        private static string TypeName(Type type)
        {
            if (type == null)
                return string.Empty;

            return (type.FullName ?? type.Name).Replace('+', '.');
        }

        internal static IContainer Create(string rootId, ContainerBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (_factories.TryGetValue(rootId, out var factory) == false)
            {
                throw new InvalidOperationException(
                    $"No generated container for '{rootId}'. Check generator diagnostics (CINGR00*) for this root.");
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
                AttachDebug(created, builder);
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

        private static void AttachDebug(IContainer created, ContainerBuilder builder)
        {
#if UNITY_EDITOR || DEBUG
            // LoadAssetGroup пишет по lifetime билдера скоупа, а не по дочернему lifetime контейнера.
            // Забираем и без диагностики, иначе записи копились бы, пока она выключена.
            var loadedAssets = ContainerRegistryDebug.TakeLoadedAssets(builder.ScopeLifetime);
            var diagnostics = created.Diagnostics;
            if (diagnostics == null)
                return;

            if (diagnostics is ContainerDiagnostics own)
                own.SetLoadedAssets(loadedAssets);

            ContainerRegistryDebug.Add(diagnostics);
            if (created.Lifetime != null)
                created.Lifetime.Listen(() => ContainerRegistryDebug.Remove(diagnostics));
#endif
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

        // Какая из альтернатив installer'а (ветка if/switch) реально попала в этот скоуп.
        public bool IsRegistered(Type implementation)
        {
            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));

            return _builder != null && _builder.HasRegistration(implementation);
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
    }
}
