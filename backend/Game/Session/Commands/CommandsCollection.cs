using System.Collections.Frozen;

namespace Game;

public interface ICommandsCollection
{
    IReadOnlyDictionary<Type, ICommand> OneWay { get; }
    IReadOnlyDictionary<Type, IResponseCommand> Responsible { get; }
}

public class CommandsCollection : ICommandsCollection
{
    public CommandsCollection(IEnumerable<ICommand> commands, IEnumerable<IResponseCommand> responseCommands)
    {
        _commands = commands.ToFrozenDictionary(command => command.RequestType);
        _responseCommands = responseCommands.ToFrozenDictionary(command => command.RequestType);
    }

    private readonly FrozenDictionary<Type, ICommand> _commands;
    private readonly FrozenDictionary<Type, IResponseCommand> _responseCommands;

    public IReadOnlyDictionary<Type, ICommand> OneWay => _commands;
    public IReadOnlyDictionary<Type, IResponseCommand> Responsible => _responseCommands;
}