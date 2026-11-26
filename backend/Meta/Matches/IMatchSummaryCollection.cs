using Infrastructure;

namespace Meta.Matches;

// The records of all the matches in the memory of a host: the lists of the console and of the read API (MetaGateway).
// The grain of a match publishes every change of its record here.
public interface IMatchSummaryCollection : IStateCollection<Guid, MatchState>;
