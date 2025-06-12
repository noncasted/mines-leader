using System;
using UnityEngine;
using VContainer;
using VContainer.Internal;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Internal
{
    public static class BuilderExtensions
    {
        public static IRegistration Register<T>(
            this IBuilder builder,
            VContainer.Lifetime lifetime = VContainer.Lifetime.Singleton)
        {
            var type = typeof(T);
            var registrationBuilder = new RegistrationBuilder(type, lifetime);
            registrationBuilder.AsSelf();
            var registration = new ContainerRegistration(builder, registrationBuilder);
            builder.Services.AddBuilder(registrationBuilder);

            return registration;
        }
        
        public static IRegistration Register<TInterface, TImplementation>(
            this IBuilder builder,
            VContainer.Lifetime lifetime = VContainer.Lifetime.Singleton)
        {
            var type = typeof(TImplementation);
            var registrationBuilder = new RegistrationBuilder(type, lifetime);
            registrationBuilder.As(typeof(TInterface));
            var registration = new ContainerRegistration(builder, registrationBuilder);
            builder.Services.AddBuilder(registrationBuilder);

            return registration;
        }

        public static IRegistration RegisterInstance<T>(
            this IBuilder builder,
            T instance,
            VContainer.Lifetime lifetime = VContainer.Lifetime.Singleton)
        {
            if (instance == null)
                throw new NullReferenceException();

            var registrationBuilder = new InstanceRegistrationBuilder(instance, lifetime).As(typeof(T));
            var registration = new ContainerRegistration(builder, registrationBuilder);
            builder.Services.AddBuilder(registrationBuilder);

            return registration;
        }

        public static IRegistration RegisterComponent<T>(
            this IBuilder builder,
            T component,
            VContainer.Lifetime lifetime = VContainer.Lifetime.Singleton) where T : Object
        {
            if (component == null)
                throw new NullReferenceException($"Missing {typeof(T).Name} component");

            var registrationBuilder = new ComponentRegistrationBuilder(component, lifetime).As(typeof(T));
            registrationBuilder.AsSelf();
            var registration = new ContainerRegistration(builder, registrationBuilder);
            builder.Services.AddBuilder(registrationBuilder);

            return registration;
        }

        public static IRegistration As<T>(this IRegistration registration)
        {
            registration.Registration.As<T>();
            return registration;
        }
        
        public static IRegistration As(this IRegistration registration, Type type)
        {
            registration.Registration.As(type);
            return registration;
        }

        public static IRegistration WithParameter<T>(this IRegistration registration, T parameter)
        {
            registration.Registration.WithParameter(parameter);
            return registration;
        }

        public static IRegistration AsSelf(this IRegistration registration)
        {
            registration.Registration.AsSelf();
            return registration;
        }

        public static IRegistration AsSelfResolvable(this IRegistration registration)
        {
            registration.ServiceCollection.AddSelfResolvable(registration.Registration);
            return registration;
        }
        
        public static void Inject<T>(this IBuilder builder, T component)
        {
            builder.Services.Inject(component);
        }

        public static T GetAsset<T>(this IBuilder builder) where T : ScriptableObject
        {
            return builder.Assets.GetAsset<T>();
        }

        public static T GetOptions<T>(this IBuilder builder) where T : class, IOptionsEntry
        {
            return builder.Assets.GetOptions<T>();
        }

        public static IRegistration RegisterAsset<T>(this IBuilder builder) where T : ScriptableObject
        {
            var asset = builder.GetAsset<T>();
            return builder.RegisterInstance(asset);
        }
        
        public static IRegistration WithAsset<T>(this IRegistration registration) where T : EnvAsset
        {
            var asset = registration.Builder.GetAsset<T>();
            registration.WithParameter(asset);
            return registration;
        }
        
        public static IRegistration WithScopeLifetime(this IRegistration registration)
        {
            registration.Registration.WithParameter(registration.Builder.Lifetime);
            return registration;
        }

        public static IRegistration WithScriptableRegistry<T1, T2>(this IRegistration registration)
            where T2 : EnvAsset
            where T1 : ScriptableRegistry<T2>
        {
            var asset = registration.Builder.GetAsset<T1>();
            registration.WithParameter(asset.Objects);
            return registration;
        }

    }
}