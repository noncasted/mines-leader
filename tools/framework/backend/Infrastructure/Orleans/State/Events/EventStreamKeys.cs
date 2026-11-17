using Shared;

namespace Infrastructure.State;

// A stream id is "<prefix>:<grain key>": the key of a grain out of it.
internal static class EventStreamKeys
{
    public static TKey Parse<TKey>(string value, GrainKeyType keyType) => keyType switch
    {
        GrainKeyType.Guid or GrainKeyType.GuidAndString => (TKey)(object)Guid.Parse(value),
        GrainKeyType.String => (TKey)(object)value,
        GrainKeyType.Integer or GrainKeyType.IntegerAndString => (TKey)(object)long.Parse(value),
        _ => throw new InvalidOperationException($"[EventStorage] Unsupported key type: {keyType}")
    };

    public static TKey ParseStream<TKey>(string streamId)
    {
        var grainKey = GrainKey(streamId);

        if (typeof(TKey) == typeof(Guid))
            return (TKey)(object)Guid.Parse(grainKey);

        if (typeof(TKey) == typeof(string))
            return (TKey)(object)grainKey;

        if (typeof(TKey) == typeof(long))
            return (TKey)(object)long.Parse(grainKey);

        throw new InvalidOperationException($"[EventStorage] Unsupported batch key type: {typeof(TKey).Name}");
    }

    public static string GrainKey(string streamId)
    {
        var colonIndex = streamId.IndexOf(':');
        return colonIndex >= 0 ? streamId.Substring(colonIndex + 1) : streamId;
    }
}
