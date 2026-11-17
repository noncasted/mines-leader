namespace Infrastructure.State;

public interface IStateMigrationStep
{
    int Version { get; }
    Type Type { get; }

    IStateValue Migrate(IStateValue value);
    IStateValue Deserialize(string raw);
}
