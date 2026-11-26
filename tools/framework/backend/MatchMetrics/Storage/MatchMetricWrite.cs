namespace Game.MatchMetrics;

// Stored: the batch is the next one of its stream. Duplicate: the stream already has it. Gap: a batch is missing before it.
internal enum MatchMetricWrite
{
    Stored,
    Duplicate,
    Gap
}
