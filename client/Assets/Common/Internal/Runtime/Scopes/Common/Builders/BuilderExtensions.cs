using System;
using Object = UnityEngine.Object;

namespace Internal
{
    // Генератор узнаёт эти методы по имени и классу BuilderExtensions: переносить или
    // переименовывать их без правки GraphWalker.IsPrimitiveBuilder нельзя.
    public static class BuilderExtensions
    {
        public static IServiceRegistration Register<T>(
            this IBuilder builder,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            return builder.Registry.Add(typeof(T), lifetime).AsSelf();
        }

        public static IServiceRegistration RegisterInstance<T>(
            this IBuilder builder,
            T instance)
        {
            if (instance == null)
                throw new NullReferenceException();

            return builder.Registry.AddInstance(typeof(T), instance);
        }

        public static IServiceRegistration RegisterComponent<T>(
            this IBuilder builder,
            T component,
            ServiceLifetime lifetime = ServiceLifetime.Singleton) where T : Object
        {
            if (component == null)
                throw new NullReferenceException($"Missing {typeof(T).Name} component");

            return builder.Registry.AddComponent(typeof(T), component, lifetime).AsSelf();
        }

        public static IServiceRegistration As<T>(this IServiceRegistration registration)
        {
            return registration.AddServiceType(typeof(T));
        }

        public static IServiceRegistration As(this IServiceRegistration registration, Type type)
        {
            return registration.AddServiceType(type);
        }

        public static IServiceRegistration WithParameter<T>(this IServiceRegistration registration, T parameter)
        {
            return registration.SetParameter(typeof(T), parameter);
        }

        public static IServiceRegistration AsSelf(this IServiceRegistration registration)
        {
            return registration.AddServiceType(registration.ImplementationType);
        }

        // Создание без зависимых решает сгенерированный класс скоупа, в рантайме отмечать нечего.
        public static IServiceRegistration AsSelfResolvable(this IServiceRegistration registration)
        {
            return registration;
        }

        public static void Inject<T>(this IBuilder builder, T component)
        {
            if (component == null)
                throw new NullReferenceException("No component provided");

            builder.Registry.AddInjection(component);
        }

        // Скоуп умеет Inject(T) для экземпляров из рантайма. Ветку Inject пишет сгенерированный класс скоупа.
        public static void Injectable<T>(this IBuilder builder) where T : class
        {
        }

        public static IServiceRegistration WithScopeLifetime(this IServiceRegistration registration)
        {
            return registration.SetParameter(typeof(IReadOnlyLifetime), registration.Builder.Lifetime);
        }
    }
}
