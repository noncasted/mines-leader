using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Internal
{
    internal sealed class Container : IContainer, IResolvePlan
    {
        internal Container(
            string name,
            Container parent,
            ILifetime lifetime,
            ContainerSlot[] slots,
            object[] instances,
            Dictionary<Type, int> typeToSlot,
            Dictionary<Type, List<int>> typeToAllSlots,
            int[] buildOrder)
        {
            _name = name;
            _parent = parent;
            _lifetime = lifetime;
            _slots = slots;
            _instances = instances;
            _typeToSlot = typeToSlot;
            _typeToAllSlots = typeToAllSlots;
            _buildOrder = buildOrder ?? Array.Empty<int>();
            _constructed = new bool[slots.Length];
            _diagnostics = new ContainerDiagnostics(
                name,
                parent == null ? null : parent._diagnostics,
                Array.Empty<RegistrationInfo>(),
                Array.Empty<int>());
        }

        private readonly string _name;
        private readonly Container _parent;
        private readonly ILifetime _lifetime;
        private readonly ContainerSlot[] _slots;
        private readonly object[] _instances;
        private readonly Dictionary<Type, int> _typeToSlot;
        private readonly Dictionary<Type, List<int>> _typeToAllSlots;
        private readonly int[] _buildOrder;
        private readonly bool[] _constructed;
        private readonly Dictionary<Type, Array> _collections = new();
        private readonly Dictionary<Type, int[]> _transientCollections = new();
        private readonly Dictionary<Type, IInjector> _injectorsByType = new();
        private readonly List<Container> _children = new();

        private ContainerDiagnostics _diagnostics;
        private int _childSerial;
        private bool _disposed;

        internal Dictionary<Type, int> TypeToSlot => _typeToSlot;
        internal Dictionary<Type, List<int>> TypeToAllSlots => _typeToAllSlots;
        internal ContainerSlot[] Slots => _slots;
        internal object[] Instances => _instances;
        internal bool IsDisposed => _disposed;

        public IContainerDiagnostics Diagnostics => _diagnostics;
        public IReadOnlyLifetime Lifetime => _lifetime;

        public object Resolve(Type type)
        {
            ContainerThread.Assert();
            ThrowIfDisposed();
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (_typeToSlot.TryGetValue(type, out var slot) == false)
                throw new InvalidOperationException($"No registration for type {type.FullName}.");

            return ResolveSlot(slot);
        }

        public T Resolve<T>()
        {
            return (T)Resolve(typeof(T));
        }

        public bool TryResolve(Type type, out object instance)
        {
            ContainerThread.Assert();
            ThrowIfDisposed();
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (_typeToSlot.TryGetValue(type, out var slot) == false)
            {
                instance = null;
                return false;
            }

            instance = ResolveSlot(slot);
            return true;
        }

        public IReadOnlyList<T> ResolveAll<T>()
        {
            ContainerThread.Assert();
            ThrowIfDisposed();

            if (_collections.TryGetValue(typeof(T), out var array) == true)
                return (T[])array;

            if (_transientCollections.TryGetValue(typeof(T), out var slots) == true)
            {
                var result = new T[slots.Length];
                for (var i = 0; i < slots.Length; i++)
                    result[i] = (T)Get(slots[i]);

                return result;
            }

            return Array.Empty<T>();
        }

        public void Inject(object target)
        {
            ContainerThread.Assert();
            ThrowIfDisposed();
            InjectCore(target);
        }

        public void InjectGameObject(GameObject target)
        {
            ContainerThread.Assert();
            ThrowIfDisposed();
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var behaviours = target.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                InjectCore(behaviour);
            }
        }

        public IContainerBuilderScope CreateChild()
        {
            ContainerThread.Assert();
            ThrowIfDisposed();

            var name = $"{_name}/{_childSerial}";
            _childSerial++;
            return new ContainerBuilder(name, this);
        }

        public void Dispose()
        {
            ContainerThread.Assert();
            if (_disposed == true)
                return;

            _disposed = true;

            while (_children.Count > 0)
                _children[_children.Count - 1].Dispose();

            DisposeOwned();
            _lifetime.Terminate();

            if (_parent == null)
                ContainerRegistryDebug.RemoveRoot(_diagnostics);
            else
                _parent.DetachChild(this);
        }

        public object Get(int slot)
        {
            if (slot >= _slots.Length)
                return _instances[slot];

            var instance = _instances[slot];
            if (instance != null)
                return instance;

            instance = CreateNew(slot);
            if (_slots[slot].Lifetime != ServiceLifetime.Transient)
                _instances[slot] = instance;

            _constructed[slot] = true;
            return instance;
        }

        public T Get<T>(int slot)
        {
            return (T)Get(slot);
        }

        internal void ConstructGraph(int[] topo, IReadOnlyList<object> injections, IReadOnlyList<int> selfResolvableSlots)
        {
            for (var i = 0; i < topo.Length; i++)
            {
                var index = topo[i];
                var slot = _slots[index];

                if (slot.Lifetime == ServiceLifetime.Transient)
                    continue;

                if (_instances[index] != null)
                {
                    if (slot.SuppressConstruct == false && _constructed[index] == false)
                        ConstructExisting(index);
                    else
                        _constructed[index] = true;

                    continue;
                }

                if (slot.SuppressCreate == true)
                    continue;

                var instance = CreateNew(index);
                _instances[index] = instance;
                _constructed[index] = true;
            }

            for (var i = 0; i < selfResolvableSlots.Count; i++)
                Get(selfResolvableSlots[i]);

            for (var i = 0; i < injections.Count; i++)
            {
                var target = injections[i];
                if (target == null)
                    continue;
                if (IsSlotInstance(target) == true)
                    continue;

                InjectCore(target);
            }
        }

        internal void BuildCollections()
        {
            foreach (var pair in _typeToAllSlots)
            {
                var elementType = pair.Key;
                var slotList = pair.Value;
                var hasTransient = false;
                for (var i = 0; i < slotList.Count; i++)
                {
                    var index = slotList[i];
                    if (index < _slots.Length &&
                        _slots[index].Lifetime == ServiceLifetime.Transient)
                    {
                        hasTransient = true;
                        break;
                    }
                }

                if (hasTransient == true)
                {
                    _transientCollections[elementType] = slotList.ToArray();
                    continue;
                }

                var array = Array.CreateInstance(elementType, slotList.Count);
                for (var i = 0; i < slotList.Count; i++)
                    array.SetValue(Get(slotList[i]), i);

                _collections[elementType] = array;
            }
        }

        internal void CompleteDiagnostics(IReadOnlyList<LoadedAssetInfo> loadedAssets)
        {
            var registrations = new RegistrationInfo[_slots.Length];
            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                registrations[i] = new RegistrationInfo(
                    slot.Index,
                    slot.ImplementationType,
                    slot.ServiceTypes,
                    slot.Lifetime,
                    slot.Dependencies ?? Array.Empty<int>(),
                    _instances[i] != null,
                    slot.IsGenerated,
                    slot.IsExternal);
            }

            _diagnostics.SetSnapshot(registrations, _buildOrder, loadedAssets);
        }

        internal void AttachChild(Container child)
        {
            _children.Add(child);
            _diagnostics.AddChild(child._diagnostics);
        }

        internal void DetachChild(Container child)
        {
            _children.Remove(child);
            _diagnostics.RemoveChild(child._diagnostics);
        }

        internal void Abandon()
        {
            if (_disposed == true)
                return;

            _disposed = true;
            DisposeOwned();
        }

        private object ResolveSlot(int slot)
        {
            if (_diagnostics != null && _diagnostics.IsHistoryEnabled == true)
            {
                var start = Stopwatch.GetTimestamp();
                var instance = Get(slot);
                var milliseconds = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                _diagnostics.Record(new ResolveRecord(slot, milliseconds, Time.frameCount));
                return instance;
            }

            return Get(slot);
        }

        private object CreateNew(int slot)
        {
            var definition = _slots[slot];
            var injector = definition.Injector;
            if (injector == null)
            {
                throw new InvalidOperationException(
                    $"No injector for {definition.ImplementationType.FullName}.");
            }

            return injector.Create(this);
        }

        private void ConstructExisting(int slot)
        {
            if (_constructed[slot] == true)
                return;

            var injector = _slots[slot].Injector;
            if (injector == null)
            {
                _constructed[slot] = true;
                return;
            }

            injector.Construct(_instances[slot], this);
            _constructed[slot] = true;
        }

        private void InjectCore(object target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var injector = GetInjectorForType(target.GetType());
            injector.Construct(target, this);
        }

        private IInjector GetInjectorForType(Type type)
        {
            if (_injectorsByType.TryGetValue(type, out var injector) == true)
                return injector;

            var signature = InjectionAnalyzer.Analyze(type);
            var constructorSlots = signature.ConstructorParameters.Length == 0
                ? Array.Empty<int>()
                : new int[signature.ConstructorParameters.Length];
            var constructSlots = InjectionAnalyzer.BindParameters(
                signature.ConstructParameters,
                type,
                _typeToSlot,
                null,
                null,
                null,
                0,
                null);
            var combined = InjectionAnalyzer.CombineSlots(constructorSlots, constructSlots);
            injector = ContainerInjectors.Create(type, combined);
            _injectorsByType[type] = injector;
            return injector;
        }

        private bool IsSlotInstance(object target)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (ReferenceEquals(_instances[i], target) == true)
                    return true;
            }

            return false;
        }

        private void DisposeOwned()
        {
            for (var i = _buildOrder.Length - 1; i >= 0; i--)
            {
                var slot = _buildOrder[i];
                var instance = _instances[slot];
                _instances[slot] = null;
                if (instance is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed == true)
                throw new ObjectDisposedException(nameof(IContainer));
        }
    }
}
