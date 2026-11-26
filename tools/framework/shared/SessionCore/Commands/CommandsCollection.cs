using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Session
{
    public interface ICommandsCollection
    {
        IReadOnlyDictionary<Type, ICommand> OneWay { get; }
        IReadOnlyDictionary<Type, IResponseCommand> Responsible { get; }
    }

    public class CommandsCollection : ICommandsCollection
    {
        public CommandsCollection(IEnumerable<ICommand> commands, IEnumerable<IResponseCommand> responseCommands)
        {
            _commands = commands.ToDictionary(command => command.RequestType);
            _responseCommands = responseCommands.ToDictionary(command => command.RequestType);
        }

        private readonly Dictionary<Type, ICommand> _commands;
        private readonly Dictionary<Type, IResponseCommand> _responseCommands;

        public IReadOnlyDictionary<Type, ICommand> OneWay => _commands;
        public IReadOnlyDictionary<Type, IResponseCommand> Responsible => _responseCommands;
    }
}
