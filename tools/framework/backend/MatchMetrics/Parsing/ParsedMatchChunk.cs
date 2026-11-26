namespace Game.MatchMetrics;

// Values: NaN only in a gauge (unchanged in its bucket).
internal sealed record ParsedMatchChunk(int Slot, int Start, long AgeMs, float[] Values);
