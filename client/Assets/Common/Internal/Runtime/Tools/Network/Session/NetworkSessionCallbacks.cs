using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface INetworkSessionCallbackEntry
    {
    }

    public interface INetworkSessionCallbacks
    {
        void Register(INetworkSessionCallbackEntry callback);

        UniTask InvokeSessionSetupCompleted(IReadOnlyLifetime lifetime);
    }
    
    public interface INetworkSessionSetupCompleted : INetworkSessionCallbackEntry
    {
        UniTask OnSessionSetupCompleted(IReadOnlyLifetime lifetime);
    }
    
    public class NetworkSessionCallbacks : INetworkSessionCallbacks
    {
        private readonly List<INetworkSessionSetupCompleted> _setupCompleted = new();

        public void Register(INetworkSessionCallbackEntry callback)
        {
            switch (callback)
            {
                case INetworkSessionSetupCompleted completed:
                {
                    _setupCompleted.Add(completed);
                    break;
                }
            }
        }

        public async UniTask InvokeSessionSetupCompleted(IReadOnlyLifetime lifetime)
        {
            foreach (var callback in _setupCompleted)
                await callback.OnSessionSetupCompleted(lifetime);
        }
    }
    
    public static class NetworkSessionCallbacksExtensions
    {
        public static IRegistration AsSessionCallback<TImplementation, TCallback>(this IRegistration registration)
            where TImplementation : class
            where TCallback : class, INetworkSessionCallbackEntry
        {
            Action<INetworkSessionCallbacks, TImplementation> registerCallback = (callbacks, target) =>
                callbacks.Register(target as TCallback);

            registration.Builder.Register<NetworkSessionCallbackRegister<TImplementation>>()
                        .WithParameter(registerCallback)
                        .AsSelfResolvable();

            return registration;
        }
    }

    public class NetworkSessionCallbackRegister<IImplementation>
    {
        public NetworkSessionCallbackRegister(
            IImplementation target,
            INetworkSessionCallbacks callbacks,
            Action<INetworkSessionCallbacks, IImplementation> registerCallback)
        {
            registerCallback.Invoke(callbacks, target);
        }
    }
}