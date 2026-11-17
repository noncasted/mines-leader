using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Internal;
using Infrastructure;

namespace Tests.Meta.Matches;

// The records in memory of a host, filled by a test.
public class StateCollectionFake<TValue> : IStateCollection<Guid, TValue>
{
    private readonly Dictionary<Guid, TValue> _matches = new();

    public IViewableDelegate Updated => throw new NotSupportedException();

    public TValue this[Guid key] => _matches[key];
    public IEnumerable<Guid> Keys => _matches.Keys;
    public IEnumerable<TValue> Values => _matches.Values;
    public int Count => _matches.Count;

    public void Add(Guid id, TValue match)
    {
        _matches.Add(id, match);
    }

    public bool ContainsKey(Guid key)
    {
        return _matches.ContainsKey(key);
    }

    public bool TryGetValue(Guid key, [MaybeNullWhen(false)] out TValue value)
    {
        return _matches.TryGetValue(key, out value);
    }

    public IEnumerator<KeyValuePair<Guid, TValue>> GetEnumerator()
    {
        return _matches.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public Task OnUpdated(Guid key, TValue value)
    {
        throw new NotSupportedException();
    }

    public Task OnUpdatedTransactional(Guid key, TValue value)
    {
        throw new NotSupportedException();
    }
}
