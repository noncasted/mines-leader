using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Infrastructure.State;

namespace Infrastructure;

// An update is a change notification: the receiver re-reads the record from storage (StateCollection.Refresh),
// so Value and UpdatedAt are not used for ordering. Kept in the message for compatibility with queued side effects.
[GenerateSerializer]
public class StateCollectionUpdate<TKey, TValue>
{
    [Id(0)] public required TKey Key { get; init; }
    [Id(1)] public required TValue Value { get; init; }
    [Id(2)] public DateTime UpdatedAt { get; init; }
}
