using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Meta.Matches;

public class MatchSummaryCollection(StateCollectionUtils<Guid, MatchState> utils, ILogger<MatchSummaryCollection> logger)
    : StateCollection<Guid, MatchState>(utils, logger), IMatchSummaryCollection;
