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
            INetworkConnection connection)
        {
            _commands = commands;
            _connection = connection;
        }

        private readonly INetworkCommandsCollection _commands;
        private readonly INetworkConnection _connection;

        public async UniTask Run(IReadOnlyLifetime lifetime)
        {
            _connection.Receiver.Empty.Advise(lifetime, response =>
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