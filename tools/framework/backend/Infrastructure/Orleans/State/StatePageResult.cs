using Common.Extensions;
using Shared;

namespace Infrastructure.State;

public class StatePageResult<TKey, TValue>
{
    public required IReadOnlyList<(TKey Key, TValue Value)> Entries { get; init; }
    public required int TotalCount { get; init; }
}
