using System;
using System.Collections.Generic;
using System.Reflection;

namespace Internal
{
    public static class ContainerInjectors
    {
        private static readonly Dictionary<Type, Func<int[], IInjector>> _factories = new();
        private static readonly HashSet<Assembly> _coveredAssemblies = new();

        // slots = constructor parameters, then Construct parameters, each in declaration order.
        public static void Register(Type implementation, Func<int[], IInjector> factory)
        {
            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _factories[implementation] = factory;
        }

        public static void MarkAssemblyCovered(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            _coveredAssemblies.Add(assembly);
        }

        internal static bool HasFactory(Type implementation)
        {
            return _factories.ContainsKey(implementation);
        }

        internal static IInjector Create(Type implementation, int[] slots)
        {
            if (_factories.TryGetValue(implementation, out var factory) == true)
                return factory.Invoke(slots ?? Array.Empty<int>());

            if (_coveredAssemblies.Contains(implementation.Assembly) == true)
            {
                throw new InvalidOperationException(
                    $"No generated injector for type {implementation.FullName}.");
            }

            return ReflectionInjector.Create(implementation, slots ?? Array.Empty<int>());
        }
    }
}
