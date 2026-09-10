using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Internal
{
    public class GeneratedContainer : IContainer, IContainerTree
    {
        protected GeneratedContainer(GeneratedScopeRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            ContainerThread.Assert();

            _request = request;
            _name = string.IsNullOrEmpty(request.Name) == false ? request.Name : request.RootId;
            _parent = request.Parent;
            _lifetime = request.Lifetime ?? throw new ArgumentException("Lifetime is required.", nameof(request));
            _diagnostics = new ContainerDiagnostics(
                _name,
                _parent == null ? null : _parent.Diagnostics,
                Array.Empty<RegistrationInfo>(),
                Array.Empty<int>());
            _diagnostics.IsGenerated = true;
        }

        public GeneratedContainer(
            GeneratedScopeRequest request,
            Dictionary<Type, object> exports,
            Dictionary<Type, Array> collections = null,
            IDisposable[] disposables = null)
            : this(request)
        {
            Complete(exports, collections, disposables);
        }

        private readonly GeneratedScopeRequest _request;
        private readonly string _name;
        private readonly IContainer _parent;
        private readonly ILifetime _lifetime;
        private readonly ContainerDiagnostics _diagnostics;
        private readonly List<IContainer> _children = new();
        private readonly Dictionary<Type, int> _typeToSlot = new();

        private Dictionary<Type, object> _exports = new();
        private Dictionary<Type, Array> _collections = new();
        private IDisposable[] _disposables = Array.Empty<IDisposable>();
        private int _childSerial;
        private bool _completed;
        private bool _disposed;

        internal bool IsDisposed => _disposed;
        internal Dictionary<Type, object> Exports => _exports;
        internal Dictionary<Type, Array> Collections => _collections;

        public IContainerDiagnostics Diagnostics => _diagnostics;
        public IReadOnlyLifetime Lifetime => _lifetime;

        public object Resolve(Type type)
        {
            ContainerThread.Assert();
            ThrowIfDisposed();
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (_diagnostics.IsHistoryEnabled == true)
            {
                var start = Stopwatch.GetTimestamp();
                var instance = ReadExport(type);
                var milliseconds = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                _diagnostics.Record(new ResolveRecord(SlotOf(type), milliseconds, Time.frameCount));
                return instance;
            }

            return ReadExport(type);
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

            EnsureCompleted();
            return _exports.TryGetValue(type, out instance);
        }

        public IReadOnlyList<T> ResolveAll<T>()
        {
            ContainerThread.Assert();
            ThrowIfDisposed();
            EnsureCompleted();

            if (_collections.TryGetValue(typeof(T), out var array) == true)
                return (T[])array;

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
            else if (_parent is IContainerTree tree)
                tree.DetachChild(this);
        }

        void IContainerTree.AttachChild(IContainer child)
        {
            AttachChild(child);
        }

        void IContainerTree.DetachChild(IContainer child)
        {
            DetachChild(child);
        }

        internal void AttachChild(IContainer child)
        {
            if (child == null)
                return;

            _children.Add(child);
            _diagnostics.AddChild(child.Diagnostics);
        }

        internal void DetachChild(IContainer child)
        {
            if (child == null)
                return;

            _children.Remove(child);
            _diagnostics.RemoveChild(child.Diagnostics);
        }

        protected void Complete(
            Dictionary<Type, object> exports,
            Dictionary<Type, Array> collections,
            IDisposable[] disposables)
        {
            ContainerThread.Assert();
            if (_completed == true)
                throw new InvalidOperationException("Generated container is already completed.");
            if (exports == null)
                throw new ArgumentNullException(nameof(exports));

            EnsureExport(exports, typeof(IContainer), this);
            EnsureExport(exports, typeof(IReadOnlyLifetime), _lifetime);
            EnsureExport(exports, typeof(ILifetime), _lifetime);

            _exports = exports;
            _collections = collections ?? new Dictionary<Type, Array>();
            _disposables = disposables ?? Array.Empty<IDisposable>();
            _typeToSlot.Clear();

            var registrations = new RegistrationInfo[exports.Count];
            var buildOrder = new int[exports.Count];
            var slot = 0;
            foreach (var pair in exports)
            {
                _typeToSlot[pair.Key] = slot;
                var implementation = pair.Value != null ? pair.Value.GetType() : pair.Key;
                registrations[slot] = new RegistrationInfo(
                    slot,
                    implementation,
                    new[] { pair.Key },
                    ServiceLifetime.Singleton,
                    Array.Empty<int>(),
                    pair.Value != null,
                    isGenerated: true,
                    isExternal: false);
                buildOrder[slot] = slot;
                slot++;
            }

            _diagnostics.SetSnapshot(registrations, buildOrder, _request.TakeLoadedAssets());
            _completed = true;

            if (_parent == null)
                ContainerRegistryDebug.AddRoot(_diagnostics);
            else if (_parent is IContainerTree tree)
                tree.AttachChild(this);
        }

        private object ReadExport(Type type)
        {
            EnsureCompleted();
            if (_exports.TryGetValue(type, out var instance) == true)
                return instance;

            throw new InvalidOperationException($"No registration for type {type.FullName}.");
        }

        private int SlotOf(Type type)
        {
            if (_typeToSlot.TryGetValue(type, out var slot) == true)
                return slot;

            return -1;
        }

        private void InjectCore(object target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var signature = InjectionAnalyzer.Analyze(target.GetType());
            if (signature.Construct == null)
                return;

            var parameters = signature.ConstructParameters;
            var arguments = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
                arguments[i] = Resolve(parameters[i].ParameterType);

            signature.Construct.Invoke(target, arguments);
        }

        private void DisposeOwned()
        {
            for (var i = 0; i < _disposables.Length; i++)
            {
                var disposable = _disposables[i];
                if (disposable == null)
                    continue;

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

        private void EnsureCompleted()
        {
            if (_completed == false)
                throw new InvalidOperationException("Generated container is not completed.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed == true)
                throw new ObjectDisposedException(nameof(IContainer));
        }

        private static void EnsureExport(Dictionary<Type, object> exports, Type type, object instance)
        {
            if (exports.ContainsKey(type) == false)
                exports.Add(type, instance);
        }
    }
}
