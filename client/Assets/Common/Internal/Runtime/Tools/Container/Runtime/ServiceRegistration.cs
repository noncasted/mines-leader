using System;
using System.Collections.Generic;

namespace Internal
{
    internal sealed class ServiceRegistration : IServiceRegistration
    {
        public ServiceRegistration(ContainerBuilder owner, Type implementationType)
        {
            _owner = owner;
            ImplementationType = implementationType;
        }

        // Время жизни и признак готового экземпляра генератор читает из исходника, рантайму они не нужны.
        // Заморозка — флаг владельца: после сборки скоупа менять нельзя ни одну его регистрацию.
        private readonly ContainerBuilder _owner;

        // Регистрации пишутся на каждой сборке скоупа, а читает их только поиск дырок
        // (ContainerBuilder.TryGetHole): список типов — у готового экземпляра, параметры — после
        // WithParameter. Остальным коллекции не создаются: это ~180 байт на Register<T>().
        private List<Type> _serviceTypes;
        private Dictionary<Type, object> _parameters;

        public IBuilder Builder => _owner.Builder;
        public Type ImplementationType { get; }

        internal object ExistingInstance;

        public IServiceRegistration AddServiceType(Type serviceType)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            // Экземпляр выставляется до первого AddServiceType (ContainerBuilder.AddInstance/AddComponent),
            // и тип без экземпляра дыркой не станет: генератор знает его сам.
            if (ExistingInstance == null)
                return this;

            _serviceTypes ??= new List<Type>();
            if (_serviceTypes.Contains(serviceType) == false)
                _serviceTypes.Add(serviceType);

            return this;
        }

        public IServiceRegistration SetParameter(Type type, object value)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            _parameters ??= new Dictionary<Type, object>();
            _parameters[type] = value;
            return this;
        }

        internal bool HasServiceType(Type type)
        {
            return _serviceTypes != null && _serviceTypes.Contains(type);
        }

        internal bool TryGetParameter(Type type, out object value)
        {
            if (_parameters == null)
            {
                value = null;
                return false;
            }

            return _parameters.TryGetValue(type, out value);
        }

        private void ThrowIfBuilt()
        {
            if (_owner.IsBuilt == true)
                throw new InvalidOperationException("Cannot modify registration after Build.");
        }
    }
}
