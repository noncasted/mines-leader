using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Infrastructure;

public class StateCollectionDurableQueueId<TKey, TValue> : IDurableQueueId
{
    public string ToRaw()
    {
        return $"state-collection-{typeof(TKey).FullName}-{typeof(TValue).FullName}";
    }
}
