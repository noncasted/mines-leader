using System;
using System.Collections.Generic;

namespace Internal
{
    public sealed class ContainerBuilder : IContainerBuilderScope
    {
        public ContainerBuilder(string name = "Root", IReadOnlyLifetime hostLifetime = null)
        {
            Name = name;
            _runtimeParent = null;
            _generatedParent = null;
            _exportParent = null;
            _hostLifetime = hostLifetime;
        }

        public ContainerBuilder(string name, IContainer parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            Name = name;
            _hostLifetime = parent.Lifetime;
            if (parent is Container runtime)
            {
                _runtimeParent = runtime;
                _generatedParent = null;
                _exportParent = null;
            }
            else if (parent is GeneratedContainer generated)
            {
                _runtimeParent = null;
                _generatedParent = generated;
                _exportParent = generated;
            }
            else
            {
                _runtimeParent = null;
                _generatedParent = null;
                _exportParent = parent;
            }
        }

        internal ContainerBuilder(string name, Container parent)
        {
            Name = name;
            _runtimeParent = parent;
            _generatedParent = null;
            _exportParent = null;
            _hostLifetime = parent != null ? parent.Lifetime : null;
        }

        private readonly Container _runtimeParent;
        private readonly GeneratedContainer _generatedParent;
        private readonly IContainer _exportParent;
        private readonly IReadOnlyLifetime _hostLifetime;
        private readonly List<ServiceRegistration> _registrations = new();
        private readonly List<object> _injections = new();
        private readonly List<LoadedAssetInfo> _loadedAssets = new();

        private bool _built;
        private bool _building;

        public string Name { get; }

        internal IContainer Parent
        {
            get
            {
                if (_runtimeParent != null)
                    return _runtimeParent;
                if (_generatedParent != null)
                    return _generatedParent;
                return _exportParent;
            }
        }

        public IServiceRegistration Add(Type implementation, ServiceLifetime lifetime)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();
            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));

            var registration = new ServiceRegistration(implementation, lifetime);
            _registrations.Add(registration);
            return registration;
        }

        public IServiceRegistration AddInstance(Type serviceType, object instance)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var registration = new ServiceRegistration(instance.GetType(), ServiceLifetime.Singleton);
            registration.ExistingInstance = instance;
            registration.IsExisting = true;
            registration.As(serviceType);
            _registrations.Add(registration);
            return registration;
        }

        public IServiceRegistration AddComponent(
            Type serviceType,
            UnityEngine.Object component,
            ServiceLifetime lifetime)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            var registration = new ServiceRegistration(component.GetType(), lifetime);
            registration.ExistingInstance = component;
            registration.IsExisting = true;
            registration.As(serviceType);
            _registrations.Add(registration);
            return registration;
        }

        public void AddInjection(object target)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            _injections.Add(target);
        }

        public void AddLoadedAsset(string label, string groupName)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();
            _loadedAssets.Add(new LoadedAssetInfo(label, groupName));
        }

        public void AddSelfResolvable(IServiceRegistration registration)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();
            if (registration == null)
                throw new ArgumentNullException(nameof(registration));

            if (registration is ServiceRegistration serviceRegistration &&
                _registrations.Contains(serviceRegistration) == true)
            {
                serviceRegistration.IsSelfResolvable = true;
                return;
            }

            throw new ArgumentException(
                "Registration does not belong to this container builder.",
                nameof(registration));
        }

        public IContainer Build()
        {
            ContainerThread.Assert();
            if (_built == true)
                throw new InvalidOperationException("ContainerBuilder.Build can only be called once.");
            if (_building == true)
                throw new InvalidOperationException("ContainerBuilder.Build is already running.");
            if (_runtimeParent != null && _runtimeParent.IsDisposed == true)
                throw new ObjectDisposedException(nameof(IContainer));
            if (_generatedParent != null && _generatedParent.IsDisposed == true)
                throw new ObjectDisposedException(nameof(IContainer));
            if (_exportParent != null && _exportParent.Lifetime != null &&
                _exportParent.Lifetime.IsTerminated == true)
                throw new ObjectDisposedException(nameof(IContainer));

            _building = true;
            ILifetime lifetime = null;
            Container container = null;

            try
            {
                lifetime = CreateLifetime();

                var slots = new List<ContainerSlot>();
                var typeToSlot = _runtimeParent != null
                    ? new Dictionary<Type, int>(_runtimeParent.TypeToSlot)
                    : new Dictionary<Type, int>();
                var typeToAllSlots = new Dictionary<Type, List<int>>();

                if (_runtimeParent != null)
                {
                    foreach (var pair in _runtimeParent.TypeToAllSlots)
                        typeToAllSlots[pair.Key] = new List<int>(pair.Value);

                    var parentSlots = _runtimeParent.Slots;
                    for (var i = 0; i < parentSlots.Length; i++)
                        slots.Add(CloneParentSlot(parentSlots[i], _runtimeParent));
                }
                else if (Parent != null)
                {
                    FlattenExportParent(Parent, slots, typeToSlot, typeToAllSlots);
                }

                var selfResolvableSlots = new List<int>();
                for (var i = 0; i < _registrations.Count; i++)
                {
                    var registration = _registrations[i];
                    var slot = CreateLocalSlot(registration, slots.Count);
                    slots.Add(slot);
                    ApplyServiceTypes(slot, typeToSlot, typeToAllSlots);
                    if (slot.IsSelfResolvable == true)
                        selfResolvableSlots.Add(slot.Index);
                }

                var containerSlot = new ContainerSlot
                {
                    Index = slots.Count,
                    ImplementationType = typeof(IContainer),
                    ServiceTypes = new[] { typeof(IContainer) },
                    Lifetime = ServiceLifetime.Singleton,
                    SuppressCreate = true,
                    SuppressConstruct = true,
                    OwnsInstance = false,
                    IsGenerated = false,
                    Dependencies = Array.Empty<int>()
                };
                slots.Add(containerSlot);
                typeToSlot[typeof(IContainer)] = containerSlot.Index;
                AddToAll(typeToAllSlots, typeof(IContainer), containerSlot.Index);

                var extras = new List<object>();
                var extraStart = slots.Count;
                for (var i = 0; i < slots.Count; i++)
                    BindInjector(slots[i], typeToSlot, extras, extraStart);

                DetectCycles(slots);
                var topo = SortTopologically(slots);
                var buildOrder = CreateBuildOrder(slots, topo);

                var instances = new object[slots.Count + extras.Count];
                var slotArray = slots.ToArray();
                container = new Container(
                    Name,
                    Parent,
                    lifetime,
                    slotArray,
                    instances,
                    typeToSlot,
                    typeToAllSlots,
                    buildOrder);

                instances[containerSlot.Index] = container;
                for (var i = 0; i < slotArray.Length; i++)
                {
                    var existing = slotArray[i].ExistingInstance;
                    if (existing != null)
                        instances[slotArray[i].Index] = existing;
                }

                for (var i = 0; i < extras.Count; i++)
                    instances[extraStart + i] = extras[i];

                container.ConstructGraph(topo, _injections, selfResolvableSlots);
                container.BuildCollections();
                container.CompleteDiagnostics(CollectLoadedAssets(lifetime));

                _built = true;
                for (var i = 0; i < _registrations.Count; i++)
                    _registrations[i].Freeze();

                if (Parent == null)
                    ContainerRegistryDebug.AddRoot(container.Diagnostics);
                else if (Parent is IContainerTree tree)
                    tree.AttachChild(container);
                else
                    ContainerRegistryDebug.AddRoot(container.Diagnostics);

                return container;
            }
            catch
            {
                if (container != null)
                {
                    if (Parent is IContainerTree tree)
                        tree.DetachChild(container);
                    else
                        ContainerRegistryDebug.RemoveRoot(container.Diagnostics);

                    container.Abandon();
                }

                lifetime?.Terminate();
                throw;
            }
            finally
            {
                _building = false;
            }
        }

        private void ThrowIfBuilt()
        {
            if (_built == true)
                throw new InvalidOperationException("Cannot register after Build.");
            if (_building == true)
                throw new InvalidOperationException("Cannot register while Build is running.");
        }

        internal ILifetime CreateLifetime()
        {
            if (_runtimeParent != null)
                return _runtimeParent.Lifetime.Child();
            if (Parent != null)
                return Parent.Lifetime.Child();
            if (_hostLifetime != null)
                return _hostLifetime.Child();
            return new Lifetime();
        }

        internal void MarkBuilt()
        {
            _built = true;
            for (var i = 0; i < _registrations.Count; i++)
                _registrations[i].Freeze();
        }

        internal IReadOnlyList<LoadedAssetInfo> TakeLoadedAssets(IReadOnlyLifetime lifetime)
        {
            return CollectLoadedAssets(lifetime);
        }

        internal bool TryGetHole(Type type, out object instance)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            for (var i = _registrations.Count - 1; i >= 0; i--)
            {
                var registration = _registrations[i];
                if (registration.ExistingInstance != null)
                {
                    if (type.IsInstanceOfType(registration.ExistingInstance) == true)
                    {
                        instance = registration.ExistingInstance;
                        return true;
                    }

                    if (registration.ServiceTypesList.Contains(type) == true)
                    {
                        instance = registration.ExistingInstance;
                        return true;
                    }
                }

                if (registration.Parameters.TryGetValue(type, out instance) == true)
                    return true;
            }

            instance = null;
            return false;
        }

        private void FlattenExportParent(
            IContainer parent,
            List<ContainerSlot> slots,
            Dictionary<Type, int> typeToSlot,
            Dictionary<Type, List<int>> typeToAllSlots)
        {
            var exports = GeneratedScopes.ReadExports(parent);
            if (exports != null)
            {
                foreach (var pair in exports)
                    AddExternalExport(slots, typeToSlot, typeToAllSlots, pair.Key, pair.Value);

                if (parent is GeneratedContainer generated)
                    FlattenCollections(generated.Collections, slots, typeToSlot, typeToAllSlots);

                return;
            }

            foreach (var info in parent.Diagnostics.Registrations)
            {
                var serviceTypes = info.ServiceTypes;
                if (serviceTypes == null || serviceTypes.Count == 0)
                {
                    if (info.ImplementationType != null &&
                        parent.TryResolve(info.ImplementationType, out var instance) == true)
                    {
                        AddExternalExport(
                            slots,
                            typeToSlot,
                            typeToAllSlots,
                            info.ImplementationType,
                            instance);
                    }

                    continue;
                }

                for (var i = 0; i < serviceTypes.Count; i++)
                {
                    var serviceType = serviceTypes[i];
                    if (serviceType == null)
                        continue;
                    if (parent.TryResolve(serviceType, out var instance) == false)
                        continue;

                    AddExternalExport(slots, typeToSlot, typeToAllSlots, serviceType, instance);
                }
            }
        }

        private static void FlattenCollections(
            Dictionary<Type, Array> collections,
            List<ContainerSlot> slots,
            Dictionary<Type, int> typeToSlot,
            Dictionary<Type, List<int>> typeToAllSlots)
        {
            if (collections == null)
                return;

            var seen = new Dictionary<object, int>();
            for (var i = 0; i < slots.Count; i++)
            {
                var instance = slots[i].ExistingInstance;
                if (instance != null && seen.ContainsKey(instance) == false)
                    seen.Add(instance, slots[i].Index);
            }

            foreach (var pair in collections)
            {
                var array = pair.Value;
                if (array == null)
                    continue;

                var list = new List<int>(array.Length);
                for (var i = 0; i < array.Length; i++)
                {
                    var instance = array.GetValue(i);
                    if (instance != null && seen.TryGetValue(instance, out var index) == true)
                    {
                        list.Add(index);
                        continue;
                    }

                    var instanceType = instance != null ? instance.GetType() : pair.Key;
                    var slot = CreateExternalSlot(slots.Count, pair.Key, instanceType, instance);
                    slots.Add(slot);
                    typeToSlot[pair.Key] = slot.Index;
                    if (instance != null)
                        seen[instance] = slot.Index;
                    list.Add(slot.Index);
                }

                typeToAllSlots[pair.Key] = list;
            }
        }

        private static void AddExternalExport(
            List<ContainerSlot> slots,
            Dictionary<Type, int> typeToSlot,
            Dictionary<Type, List<int>> typeToAllSlots,
            Type serviceType,
            object instance)
        {
            var implementation = instance != null ? instance.GetType() : serviceType;
            var slot = CreateExternalSlot(slots.Count, serviceType, implementation, instance);
            slots.Add(slot);
            ApplyServiceTypes(slot, typeToSlot, typeToAllSlots);
        }

        private static ContainerSlot CreateExternalSlot(
            int index,
            Type serviceType,
            Type implementationType,
            object instance)
        {
            return new ContainerSlot
            {
                Index = index,
                ImplementationType = implementationType ?? serviceType,
                ServiceTypes = new[] { serviceType },
                Lifetime = ServiceLifetime.Singleton,
                ExistingInstance = instance,
                SuppressCreate = true,
                SuppressConstruct = true,
                OwnsInstance = false,
                IsGenerated = true,
                IsExternal = true,
                Dependencies = Array.Empty<int>()
            };
        }

        private static ContainerSlot CloneParentSlot(ContainerSlot parentSlot, Container parent)
        {
            var clone = new ContainerSlot
            {
                Index = parentSlot.Index,
                ImplementationType = parentSlot.ImplementationType,
                ServiceTypes = parentSlot.ServiceTypes,
                Lifetime = parentSlot.Lifetime,
                Parameters = parentSlot.Parameters,
                IsGenerated = parentSlot.IsGenerated,
                IsExternal = true,
                Dependencies = parentSlot.Dependencies ?? Array.Empty<int>()
            };

            if (parentSlot.SuppressCreate == true || parentSlot.Lifetime == ServiceLifetime.Singleton)
            {
                clone.ExistingInstance = parent.Instances[parentSlot.Index];
                clone.SuppressCreate = true;
                clone.SuppressConstruct = true;
                clone.OwnsInstance = false;
                return clone;
            }

            clone.SuppressCreate = false;
            clone.SuppressConstruct = false;
            clone.OwnsInstance = parentSlot.Lifetime == ServiceLifetime.Scoped;
            return clone;
        }

        private static ContainerSlot CreateLocalSlot(ServiceRegistration registration, int index)
        {
            var serviceTypes = registration.ServiceTypesList.Count == 0
                ? new[] { registration.ImplementationType }
                : registration.ServiceTypesList.ToArray();

            return new ContainerSlot
            {
                Index = index,
                ImplementationType = registration.ImplementationType,
                ServiceTypes = serviceTypes,
                Lifetime = registration.Lifetime,
                Parameters = registration.Parameters.Count == 0 ? null : registration.Parameters,
                ExistingInstance = registration.ExistingInstance,
                SuppressCreate = registration.IsExisting,
                SuppressConstruct = false,
                OwnsInstance = registration.IsExisting == false &&
                               registration.Lifetime != ServiceLifetime.Transient,
                IsSelfResolvable = registration.IsSelfResolvable,
                Dependencies = Array.Empty<int>()
            };
        }

        private static void ApplyServiceTypes(
            ContainerSlot slot,
            Dictionary<Type, int> typeToSlot,
            Dictionary<Type, List<int>> typeToAllSlots)
        {
            var serviceTypes = slot.ServiceTypes;
            for (var i = 0; i < serviceTypes.Length; i++)
            {
                var serviceType = serviceTypes[i];
                typeToSlot[serviceType] = slot.Index;
                AddToAll(typeToAllSlots, serviceType, slot.Index);
            }
        }

        private static void AddToAll(Dictionary<Type, List<int>> typeToAllSlots, Type serviceType, int slot)
        {
            if (typeToAllSlots.TryGetValue(serviceType, out var list) == false)
            {
                list = new List<int>();
                typeToAllSlots[serviceType] = list;
            }

            list.Add(slot);
        }

        private static void BindInjector(
            ContainerSlot slot,
            Dictionary<Type, int> typeToSlot,
            List<object> extras,
            int extraStart)
        {
            if (slot.SuppressCreate == true && slot.SuppressConstruct == true)
            {
                if (slot.Dependencies == null)
                    slot.Dependencies = Array.Empty<int>();
                return;
            }

            var signature = InjectionAnalyzer.Analyze(slot.ImplementationType);
            if (slot.SuppressCreate == false && signature.Constructor == null)
            {
                throw new InvalidOperationException(
                    $"Type {slot.ImplementationType.FullName} has no public constructor.");
            }

            var dependencies = new List<int>();
            var localExtras = new Dictionary<Type, int>();

            int[] constructorSlots;
            if (signature.Constructor == null)
            {
                constructorSlots = Array.Empty<int>();
            }
            else if (slot.SuppressCreate == true)
            {
                constructorSlots = new int[signature.ConstructorParameters.Length];
            }
            else
            {
                constructorSlots = InjectionAnalyzer.BindParameters(
                    signature.ConstructorParameters,
                    slot.ImplementationType,
                    typeToSlot,
                    slot.Parameters,
                    localExtras,
                    extras,
                    extraStart,
                    dependencies);
            }

            var constructSlots = signature.Construct == null
                ? Array.Empty<int>()
                : InjectionAnalyzer.BindParameters(
                    signature.ConstructParameters,
                    slot.ImplementationType,
                    typeToSlot,
                    slot.Parameters,
                    localExtras,
                    extras,
                    extraStart,
                    dependencies);

            slot.Dependencies = dependencies.ToArray();
            slot.IsGenerated = ContainerInjectors.HasFactory(slot.ImplementationType);
            slot.Injector = ContainerInjectors.Create(
                slot.ImplementationType,
                InjectionAnalyzer.CombineSlots(constructorSlots, constructSlots));
        }

        private static void DetectCycles(IReadOnlyList<ContainerSlot> slots)
        {
            var count = slots.Count;
            var state = new int[count];
            var stack = new List<int>();

            for (var i = 0; i < count; i++)
            {
                if (state[i] == 0)
                    Visit(i);
            }

            void Visit(int slot)
            {
                state[slot] = 1;
                stack.Add(slot);

                var dependencies = slots[slot].Dependencies;
                if (dependencies != null)
                {
                    for (var i = 0; i < dependencies.Length; i++)
                    {
                        var dependency = dependencies[i];
                        if (dependency >= count)
                            continue;
                        if (state[dependency] == 1)
                            throw CreateCycleException(slots, stack, dependency);
                        if (state[dependency] == 0)
                            Visit(dependency);
                    }
                }

                stack.RemoveAt(stack.Count - 1);
                state[slot] = 2;
            }
        }

        private static InvalidOperationException CreateCycleException(
            IReadOnlyList<ContainerSlot> slots,
            List<int> stack,
            int start)
        {
            var startIndex = 0;
            for (var i = 0; i < stack.Count; i++)
            {
                if (stack[i] == start)
                {
                    startIndex = i;
                    break;
                }
            }

            var names = new string[stack.Count - startIndex + 1];
            var write = 0;
            for (var i = startIndex; i < stack.Count; i++)
            {
                names[write] = TypeName(slots[stack[i]].ImplementationType);
                write++;
            }

            names[write] = TypeName(slots[start].ImplementationType);
            return new InvalidOperationException(
                "Circular dependency detected: " + string.Join(" -> ", names));
        }

        private static int[] SortTopologically(IReadOnlyList<ContainerSlot> slots)
        {
            var count = slots.Count;
            var order = new List<int>(count);
            var visited = new bool[count];

            for (var i = 0; i < count; i++)
                Visit(i);

            return order.ToArray();

            void Visit(int slot)
            {
                if (visited[slot] == true)
                    return;

                visited[slot] = true;
                var dependencies = slots[slot].Dependencies;
                if (dependencies != null)
                {
                    for (var i = 0; i < dependencies.Length; i++)
                    {
                        var dependency = dependencies[i];
                        if (dependency < count)
                            Visit(dependency);
                    }
                }

                order.Add(slot);
            }
        }

        private static int[] CreateBuildOrder(IReadOnlyList<ContainerSlot> slots, int[] topo)
        {
            var order = new List<int>();
            for (var i = 0; i < topo.Length; i++)
            {
                var index = topo[i];
                var slot = slots[index];
                if (slot.OwnsInstance == true && slot.SuppressCreate == false)
                    order.Add(index);
            }

            return order.ToArray();
        }

        private static string TypeName(Type type)
        {
            if (type == null)
                return "<null>";
            return type.FullName ?? type.Name;
        }

        private IReadOnlyList<LoadedAssetInfo> CollectLoadedAssets(IReadOnlyLifetime lifetime)
        {
            var recorded = MergeAssets(
                ContainerRegistryDebug.TakeLoadedAssets(lifetime),
                ContainerRegistryDebug.TakeLoadedAssets(_hostLifetime));

            if (_loadedAssets.Count == 0)
                return recorded;

            if (recorded.Count == 0)
                return _loadedAssets.ToArray();

            var merged = new LoadedAssetInfo[_loadedAssets.Count + recorded.Count];
            _loadedAssets.CopyTo(merged);
            for (var i = 0; i < recorded.Count; i++)
                merged[_loadedAssets.Count + i] = recorded[i];

            return merged;
        }

        private static IReadOnlyList<LoadedAssetInfo> MergeAssets(
            IReadOnlyList<LoadedAssetInfo> first,
            IReadOnlyList<LoadedAssetInfo> second)
        {
            if (first == null || first.Count == 0)
                return second ?? Array.Empty<LoadedAssetInfo>();

            if (second == null || second.Count == 0)
                return first;

            var merged = new LoadedAssetInfo[first.Count + second.Count];
            for (var i = 0; i < first.Count; i++)
                merged[i] = first[i];
            for (var i = 0; i < second.Count; i++)
                merged[first.Count + i] = second[i];

            return merged;
        }
    }
}
