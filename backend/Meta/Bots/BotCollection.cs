using Infrastructure;

namespace Meta.Bots;


public interface IBotCollection : IStateCollection<Guid, BotState>
{
}

public class BotCollection(StateCollectionUtils<Guid, BotState> utils)
    : StateCollection<Guid, BotState>(utils), IBotCollection;