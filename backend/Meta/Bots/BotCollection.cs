using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Meta.Bots;


public interface IBotCollection : IStateCollection<Guid, BotState>
{
}

public class BotCollection(StateCollectionUtils<Guid, BotState> utils, ILogger<BotCollection> logger)
    : StateCollection<Guid, BotState>(utils, logger), IBotCollection;