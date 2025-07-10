using System;
using Cysharp.Threading.Tasks;
using Global.Backend;
using Internal;
using UnityEngine;

namespace Common.Network
{
    public interface INetworkCommandsDispatcher
    {
        UniTask Run(IReadOnlyLifetime lifetime);
    }

    public class NetworkCommandsDispatcher : INetworkCommandsDispatcher
    {
        public NetworkCommandsDispatcher(
            INetworkCommandsCollection commands,
            ISocketReceiver receiver)
        {
            _commands = commands;
            _receiver = receiver;
        }

        private readonly INetworkCommandsCollection _commands;
        private readonly ISocketReceiver _receiver;

        public async UniTask Run(IReadOnlyLifetime lifetime)
        {
            _receiver.Empty.Advise(lifetime, response =>
            {
                var context = response.Context;
                var commands = _commands.Get(context);

                try
                {
                    commands.Execute(lifetime, context);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }
    }
}