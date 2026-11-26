using System.Text.Json;
using Shared;

namespace Game.MatchMetrics;

// Time - seconds of the run, At - the server time; Fields - the json object of the event (numbers and texts).
public sealed record MatchMetricEventInfo(
    Guid StreamId,
    Guid UserId,
    SharedMatchMetrics.MatchStreamKind Kind,
    int Seq,
    int Index,
    float Time,
    DateTime At,
    string Name,
    JsonElement Fields);
