using Shared;

namespace Game.MatchMetrics;

public sealed record MatchMetricStreamInfo(
    Guid Id,
    Guid MatchId,
    Guid UserId,
    Guid WriterId,
    SharedMatchMetrics.MatchStreamKind Kind,
    DateTime OpenedAt,
    int LastSeq,
    DateTime LastAt);
