using System;
using Internal;
using Shared;

namespace Global.Backend
{
    public interface INetworkCommand
    {
        Type ContextType { get; }

        INetworkContext Execute(IReadOnlyLifetime lifetime, INetworkContext context);
    }

    public abstract class OneWayCommand<TContext> : INetworkCommand where TContext : INetworkContext
    {
        public Type ContextType { get; } = typeof(TContext);

        public INetworkContext Execute(IReadOnlyLifetime lifetime, INetworkContext context)
        {
            Execute(lifetime, (TContext)context);
            return EmptyResponse.Ok;
        }

        protected abstract void Execute(IReadOnlyLifetime lifetime, TContext context);
    }

    public abstract class ResponseCommand<TContext> : INetworkCommand where TContext : INetworkContext
    {
        public Type ContextType { get; } = typeof(TContext);

        public INetworkContext Execute(IReadOnlyLifetime lifetime, INetworkContext context)
        {
            return Execute(lifetime, (TContext)context);
        }

        protected abstract INetworkContext Execute(IReadOnlyLifetime lifetime, TContext context);
    }

    public static class NetworkCommandExtensions
    {
        public static IRegistration RegisterCommand<T>(this IScopeBuilder builder) where T : INetworkCommand
        {
            builder.Register<T>();

            var registration = builder.Register<CommandResolver<T>>();
            registration.AsSelfResolvable();

            return registration;
        }

        public class CommandResolver<T> where T : INetworkCommand
        {
            public CommandResolver(T command, INetworkCommandsCollection collection)
            {
                collection.Add(command);
            }
        }
    }
}