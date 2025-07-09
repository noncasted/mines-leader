using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Common.Network
{
    public interface INetworkCommand
    {
        Type ContextType { get; }
        
        UniTask Execute(IReadOnlyLifetime lifetime, INetworkContext context);
    }
    
    public abstract class NetworkCommand<TContext> : INetworkCommand where TContext : INetworkContext
    {
        public Type ContextType { get; } = typeof(TContext);

        public UniTask Execute(IReadOnlyLifetime lifetime, INetworkContext context)
        {
            return Execute(lifetime, (TContext)context);
        }

        protected abstract UniTask Execute(IReadOnlyLifetime lifetime, TContext context);
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