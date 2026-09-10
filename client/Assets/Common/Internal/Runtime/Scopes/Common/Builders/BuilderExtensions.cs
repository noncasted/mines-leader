using System;
using Object = UnityEngine.Object;

namespace Internal
{
    public static class BuilderExtensions
    {
        public static IRegistration Register<T>(
            this IBuilder builder,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            var registration = builder.Services.Registry.Add(typeof(T), lifetime);
            registration.AsSelf();

            return new ContainerRegistration(builder, registration);
        }

        public static IRegistration Register<TInterface, TImplementation>(
            this IBuilder builder,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            var registration = builder.Services.Registry.Add(typeof(TImplementation), lifetime);
            registration.As(typeof(TInterface));

            return new ContainerRegistration(builder, registration);
        }

        public static IRegistration RegisterInstance<T>(
            this IBuilder builder,
            T instance)
        {
            if (instance == null)
                throw new NullReferenceException();

            var registration = builder.Services.Registry.AddInstance(typeof(T), instance);

            return new ContainerRegistration(builder, registration);
        }

        public static IRegistration RegisterComponent<T>(
            this IBuilder builder,
            T component,
            ServiceLifetime lifetime = ServiceLifetime.Singleton) where T : Object
        {
            if (component == null)
                throw new NullReferenceException($"Missing {typeof(T).Name} component");

            var registration = builder.Services.Registry.AddComponent(typeof(T), component, lifetime);
            registration.AsSelf();

            return new ContainerRegistration(builder, registration);
        }

        public static IRegistration As<T>(this IRegistration registration)
        {
            registration.Registration.As(typeof(T));
            return registration;
        }

        public static IRegistration As(this IRegistration registration, Type type)
        {
            registration.Registration.As(type);
            return registration;
        }

        public static IRegistration WithParameter<T>(this IRegistration registration, T parameter)
        {
            registration.Registration.WithParameter(typeof(T), parameter);
            return registration;
        }

        public static IRegistration AsSelf(this IRegistration registration)
        {
            registration.Registration.AsSelf();
            return registration;
        }

        public static IRegistration AsSelfResolvable(this IRegistration registration)
        {
            registration.ServiceCollection.Registry.AddSelfResolvable(registration.Registration);
            return registration;
        }

        public static void Inject<T>(this IBuilder builder, T component)
        {
            if (component == null)
                throw new NullReferenceException("No component provided");

            builder.Services.Registry.AddInjection(component);
        }

        public static IRegistration WithScopeLifetime(this IRegistration registration)
        {
            registration.Registration.WithParameter(typeof(IReadOnlyLifetime), registration.Builder.Lifetime);
            return registration;
        }
    }
}
