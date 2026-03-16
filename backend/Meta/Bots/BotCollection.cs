using Infrastructure;
using Infrastructure.State;

namespace Meta.Bots;

[GenerateSerializer]
public class BotState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
}

public interface IBotCollection : IStateCollection<Guid, BotState>
{
}

public class BotCollection(StateCollectionUtils<Guid, BotState> utils)
    : StateCollection<Guid, BotState>(utils), IBotCollection;