using System;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace Common.Network
{
    public class NetworkCommandsDispatcher : INetworkCommandsDispatcher
    {
        public NetworkCommandsDispatcher(
            INetworkCommandsCollection commands,
            INetworkReceiver receiver)
        {
            _commands = commands;
            _receiver = receiver;
        }

        private readonly INetworkCommandsCollection _commands;
        private readonly INetworkReceiver _receiver;

        public async UniTask Run(IReadOnlyLifetime lifetime)
        {
            var reader = _receiver.Empty.Reader;
            var cancellation = lifetime.Token;

            while (await reader.WaitToReadAsync(cancellation) && lifetime.IsTerminated == false)
            {
                while (reader.TryRead(out var response))
                {
                    var context = response.Context;
                    var commands = _commands.Get(context);

                    try
                    {
                        await commands.Execute(lifetime, context);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }
    }
}