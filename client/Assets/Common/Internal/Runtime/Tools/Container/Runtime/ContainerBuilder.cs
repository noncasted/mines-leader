using System;
using System.Collections.Generic;

namespace Internal
{
    // Собирает регистрации и дырки скоупа. Контейнер строит только сгенерированный класс (GeneratedScopes).
    public sealed class ContainerBuilder : IContainerRegistry
    {
        public ContainerBuilder(string name = "Root", IReadOnlyLifetime hostLifetime = null)
        {
            Name = name;
            _hostLifetime = hostLifetime;
        }

        // scopeLifetime — время жизни скоупа у загрузчика: контейнер живёт его ребёнком.
        public ContainerBuilder(string name, IContainer parent, IReadOnlyLifetime scopeLifetime)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            Name = name;
            Parent = parent;
            _hostLifetime = parent.Lifetime;
            _scopeLifetime = scopeLifetime;
        }

        private readonly IReadOnlyLifetime _hostLifetime;
        private readonly IReadOnlyLifetime _scopeLifetime;
        private readonly List<ServiceRegistration> _registrations = new();
        private readonly List<object> _injections = new();

        private bool _built;

        public string Name { get; }

        internal IContainer Parent { get; }

        // Lifetime, который видит installer через IBuilder.Lifetime.
        internal IReadOnlyLifetime ScopeLifetime => _scopeLifetime ?? _hostLifetime;

        // IBuilder, который пишет в этот ContainerBuilder. Регистрации отдают его расширениям цепочки
        // (WithScopeLifetime, AsSessionCallback).
        internal IBuilder Builder { get; private set; }

        public IServiceRegistration Add(Type implementation, ServiceLifetime lifetime)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();

            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));

            var registration = new ServiceRegistration(Builder, implementation, lifetime);
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

            var registration = new ServiceRegistration(Builder, instance.GetType(), ServiceLifetime.Singleton);
            registration.ExistingInstance = instance;
            registration.IsExisting = true;
            registration.AddServiceType(serviceType);
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

            var registration = new ServiceRegistration(Builder, component.GetType(), lifetime);
            registration.ExistingInstance = component;
            registration.IsExisting = true;
            registration.AddServiceType(serviceType);
            _registrations.Add(registration);
            return registration;
        }

        // Зовёт конструктор билдера. Один ContainerBuilder — один IBuilder.
        internal void AttachBuilder(IBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (Builder != null && Builder != builder)
                throw new InvalidOperationException($"Container builder '{Name}' is already attached to another builder.");

            Builder = builder;
        }

        public void AddInjection(object target)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();

            if (target == null)
                throw new ArgumentNullException(nameof(target));

            _injections.Add(target);
        }

        internal ILifetime CreateLifetime()
        {
            if (_scopeLifetime != null)
                return _scopeLifetime.Child();

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

        internal bool HasRegistration(Type implementation)
        {
            for (var i = 0; i < _registrations.Count; i++)
            {
                if (_registrations[i].ImplementationType == implementation)
                    return true;
            }

            return false;
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

                    if (registration.HasServiceType(type) == true)
                    {
                        instance = registration.ExistingInstance;
                        return true;
                    }
                }

                if (registration.TryGetParameter(type, out instance) == true)
                    return true;
            }

            // builder.Inject(x): сгенерированный класс получает экземпляр дыркой и зовёт Construct сам.
            for (var i = _injections.Count - 1; i >= 0; i--)
            {
                if (type.IsInstanceOfType(_injections[i]) == true)
                {
                    instance = _injections[i];
                    return true;
                }
            }

            instance = null;
            return false;
        }

        private void ThrowIfBuilt()
        {
            if (_built == true)
                throw new InvalidOperationException("Cannot register after the container is created.");
        }
    }
}
