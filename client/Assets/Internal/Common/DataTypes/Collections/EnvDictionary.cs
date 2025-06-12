using System.Collections.Generic;

namespace Internal
{
    public interface IEnvDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
    }

    public interface IEnvDictionaryKeyProvider<TKey>
    {
        TKey EnvKey { get; }
    }

    public class EnvDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IEnvDictionary<TKey, TValue>
    {
    }
}