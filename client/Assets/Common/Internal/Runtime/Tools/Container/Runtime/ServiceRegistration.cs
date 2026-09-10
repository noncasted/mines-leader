using System;
using System.Collections.Generic;

namespace Internal
{
    internal sealed class ServiceRegistration : IServiceRegistration
    {
        public ServiceRegistration(Type implementationType, ServiceLifetime lifetime)
        {
            ImplementationType = implementationType;
            Lifetime = lifetime;
        }

        private bool _frozen;
        private readonly List<Type> _serviceTypes = new();
        private readonly Dictionary<Type, object> _parameters = new();

        public Type ImplementationType { get; }
        public ServiceLifetime Lifetime { get; }

        internal List<Type> ServiceTypesList => _serviceTypes;
        internal Dictionary<Type, object> Parameters => _parameters;
        internal object ExistingInstance;
        internal bool IsExisting;
        internal bool IsSelfResolvable;

        public IServiceRegistration As(Type serviceType)
        {
            ContainerThread.Assert();
            ThrowIfFrozen();
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (_serviceTypes.Contains(serviceType) == false)
                _serviceTypes.Add(serviceType);

            return this;
        }

        public IServiceRegistration AsSelf()
        {
            return As(ImplementationType);
        }

        public IServiceRegistration WithParameter(Type type, object value)
        {
            ContainerThread.Assert();
            ThrowIfFrozen();
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            _parameters[type] = value;
            return this;
        }

        internal void Freeze()
        {
            _frozen = true;
        }

        private void ThrowIfFrozen()
        {
            if (_frozen == true)
                throw new InvalidOperationException("Cannot modify registration after Build.");
        }
    }
}
