using System;
using System.Collections.Generic;
using Shared;

namespace Internal
{
    public interface INetworkCommandsCollection
    {
        void Add(INetworkCommand command);
        // Throws when there is no command for the type of the context.
        INetworkCommand Get(INetworkContext context);
        bool TryGet(INetworkContext context, out INetworkCommand command);
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
            if (_commands.TryGetValue(context.GetType(), out var command) == false)
                throw new InvalidOperationException($"[Network] No command for {context.GetType().FullName}");

            return command;
        }

        public bool TryGet(INetworkContext context, out INetworkCommand command)
        {
            return _commands.TryGetValue(context.GetType(), out command);
        }
    }
}