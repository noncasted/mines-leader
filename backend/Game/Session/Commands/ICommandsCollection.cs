namespace Game;

public interface ICommandsCollection
{
    IReadOnlyDictionary<Type, ICommand> EmptyCommands { get; }
    IReadOnlyDictionary<Type, IResponseCommand> FullCommands { get; }
}