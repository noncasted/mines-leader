using System;
using Internal;
using UnityEngine;

namespace Global.Backend
{
    public interface INetworkCommandsDispatcher
    {
        void Run(IReadOnlyLifetime lifetime);
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

        public void Run(IReadOnlyLifetime lifetime)
        {
            var reader = _connection.Reader;
            var writer = _connection.Writer;

            reader.OneWay.Advise(lifetime, response =>
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

            reader.Request.Advise(lifetime, request =>
            {
                var context = request.Context;
                var commands = _commands.Get(context);

                try
                {
                    var response = commands.Execute(lifetime, context);
                    writer.WriteResponse(response, request.RequestId);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
            
            reader.Response.Advise(lifetime, response =>
            {
                var context = response.Context;
                writer.OnRequestHandled(context, response.RequestId);
            });
        }
    }
}