namespace Game.MatchMetrics;

// Fields: a json object, a number or a text per key.
internal sealed record ParsedMatchEvent(string Name, float Time, long AgeMs, string Fields);
