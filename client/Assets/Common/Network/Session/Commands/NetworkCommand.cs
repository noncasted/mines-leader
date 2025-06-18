using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Common.Network
{
    public abstract class NetworkCommand<TContext> : INetworkCommand where TContext : INetworkContext
    {
        public Type ContextType { get; } = typeof(TContext);

        public UniTask Execute(IReadOnlyLifetime lifetime, INetworkContext context)
        {
            return Execute(lifetime, (TContext)context);
        }

        protected abstract UniTask Execute(IReadOnlyLifetime lifetime, TContext context);
    }
}