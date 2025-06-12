using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;

namespace Common.Network
{
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
}