using System.Collections.Frozen;

namespace Game;

public class CommandsCollection : ICommandsCollection
{
    public CommandsCollection(IEnumerable<ICommand> commands, IEnumerable<IResponseCommand> responseCommands)
    {
        _commands = commands.ToFrozenDictionary(command => command.RequestType);
        _responseCommands = responseCommands.ToFrozenDictionary(command => command.RequestType);
    }

    private readonly FrozenDictionary<Type, ICommand> _commands;
    private readonly FrozenDictionary<Type, IResponseCommand> _responseCommands;

    public IReadOnlyDictionary<Type, ICommand> EmptyCommands => _commands;
    public IReadOnlyDictionary<Type, IResponseCommand> FullCommands => _responseCommands;
}