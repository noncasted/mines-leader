using System;
using System.Collections.Generic;
using Shared;

namespace Common.Network
{
    public interface INetworkCommandsCollection
    {
        void Add(INetworkCommand command);
        INetworkCommand Get(INetworkContext context);
    }
    
    public class NetworkCommandsCollection : INetworkCommandsCollection
    {
        private readonly Dictionary<Type, INetworkCommand> _commands = new();

        public void Add(INetworkCommand command)
        {
            _commands.Add(command.ContextType, command);
        }

        public INetworkCommand Get(INetworkContext context)
        {
            return _commands[context.GetType()];
        }
    }
}