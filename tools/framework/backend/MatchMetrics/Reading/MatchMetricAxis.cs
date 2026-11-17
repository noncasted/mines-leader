namespace Game.MatchMetrics;

// Run - seconds of the clock of the run from its start; Server - unix seconds of the server time (the "at" of a chunk), to
// match the streams of several players and the stages of the header of a match.
public enum MatchMetricAxis
{
    Run,
    Server
}
