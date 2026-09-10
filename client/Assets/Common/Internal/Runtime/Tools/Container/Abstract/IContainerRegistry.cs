using System;

namespace Internal
{
    public interface IContainerRegistry
    {
        IServiceRegistration Add(Type implementation, ServiceLifetime lifetime);
        IServiceRegistration AddInstance(Type serviceType, object instance);
        IServiceRegistration AddComponent(Type serviceType, UnityEngine.Object component, ServiceLifetime lifetime);
        void AddInjection(object target);
    }
}