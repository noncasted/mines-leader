using System.Collections.Frozen;

namespace Backend.Gateway;

public interface IUserCommandsCollection
{
    IReadOnlyDictionary<Type, IUserCommand> Entries { get; }
}

public class UserCommandsCollection : IUserCommandsCollection
{
    public UserCommandsCollection(IEnumerable<IUserCommand> commands)
    {
        _entries = commands.ToFrozenDictionary(command => command.RequestType);
    }

    private readonly FrozenDictionary<Type, IUserCommand> _entries;

    public IReadOnlyDictionary<Type, IUserCommand> Entries => _entries;
}