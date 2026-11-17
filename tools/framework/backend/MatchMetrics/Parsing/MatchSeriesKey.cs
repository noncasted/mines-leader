namespace Game.MatchMetrics;

// A series of the dictionary: the name and the tags as canonical json (keys sorted), the same text for the same tags.
internal readonly record struct MatchSeriesKey(string Name, string Tags);
