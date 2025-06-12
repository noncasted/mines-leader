using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Common.Network
{
    public interface INetworkSessionCallbackEntry
    {
        
    }
    
    public interface INetworkSessionCallbacks
    {
        void Register(INetworkSessionCallbackEntry callback);

        UniTask InvokeSessionSetupCompleted(IReadOnlyLifetime lifetime);
    }

    public static class NetworkSessionCallbacksExtensions
    {
        public static IRegistration AsSessionCallback<TImplementation, TCallback>(this IRegistration registration)
            where TImplementation : class
            where TCallback : class, INetworkSessionCallbackEntry
        {
            Action<INetworkSessionCallbacks, TImplementation> registerCallback =
                (callbacks, target) => callbacks.Register(target as TCallback);

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