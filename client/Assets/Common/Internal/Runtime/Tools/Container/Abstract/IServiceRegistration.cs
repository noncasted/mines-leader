using System;

namespace Internal
{
    public interface IServiceRegistration
    {
        Type ImplementationType { get; }
        ServiceLifetime Lifetime { get; }
        IServiceRegistration As(Type serviceType);
        IServiceRegistration AsSelf();
        IServiceRegistration WithParameter(Type type, object value);
    }
}
