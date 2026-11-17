using System;
using System.Collections;
using System.Collections.Generic;

namespace Internal
{
    public class ViewableDictionary<TKey, TSource> : ViewableDictionary<TKey, TSource, TSource>
    {
    }

    // The entries are kept in a private dictionary, not inherited from Dictionary<,>: the mutators of the base (or of
    // ICollection<KeyValuePair> / IDictionary) would change the entries without their lifetimes.
    public class ViewableDictionary<TKey, TSource, TView> : IViewableDictionary<TKey, TView> where TSource : TView
    {
        private readonly Dictionary<TKey, TSource> _items = new();
        private readonly EventSource<IReadOnlyLifetime, TKey, TView> _eventSource = new();
        private readonly Dictionary<TKey, ILifetime> _lifetimes = new();

        public int Count => _items.Count;

        public TView this[TKey key] => _items[key];

        public IEnumerable<TKey> Keys => _items.Keys;

        // A struct: a foreach over it in a loop of every frame allocates nothing (an iterator took 64 B a pass).
        public ValueCollection Values => new(_items);

        IEnumerable<TView> IReadOnlyDictionary<TKey, TView>.Values => Values;

        public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, TKey, TView> handler)
        {
            _eventSource.Advise(lifetime, handler);
        }

        public IReadOnlyLifetime GetLifetime(TKey value)
        {
            return _lifetimes[value];
        }

        public bool ContainsKey(TKey key)
        {
            return _items.ContainsKey(key);
        }

        public IReadOnlyLifetime Add(TKey key, TSource value)
        {
            _items.Add(key, value);
            var lifetime = new Lifetime();
            _lifetimes.Add(key, lifetime);

            _eventSource.Invoke(lifetime, key, value);

            OnModified();

            return lifetime;
        }

        // Adds the key or replaces its value: the entry of a replaced value ends and a new one starts.
        public void Set(TKey key, TSource value)
        {
            if (_items.ContainsKey(key) == false)
            {
                // Add reports the modification itself.
                Add(key, value);
                return;
            }

            _lifetimes[key].Terminate();

            var lifetime = new Lifetime();
            _lifetimes[key] = lifetime;

            _items[key] = value;
            _eventSource.Invoke(lifetime, key, value);

            OnModified();
        }

        public void Remove(TKey key)
        {
            // The lifetime leaves the map with the entry: a key added again gets a new one (Add threw on the
            // stale entry before), and a removed entry is not referenced any more.
            if (_lifetimes.Remove(key, out var lifetime) == false)
                throw new KeyNotFoundException($"{key} is not in the dictionary");

            _items.Remove(key);
            lifetime.Terminate();

            OnModified();
        }

        public void Clear()
        {
            TerminateEntries();
            OnModified();
        }

        protected virtual void OnModified()
        {
        }

        public IEnumerator<KeyValuePair<TKey, TView>> GetEnumerator()
        {
            foreach (var pair in _items)
                yield return new KeyValuePair<TKey, TView>(pair.Key, pair.Value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerable<KeyValuePair<TKey, TSource>> GetEnumerable()
        {
            return _items;
        }

        public bool TryGetValue(TKey key, out TView value)
        {
            var result = _items.TryGetValue(key, out var source);
            value = source;
            return result;
        }

        public bool TryGetSourceValue(TKey key, out TSource value)
        {
            return _items.TryGetValue(key, out value);
        }

        public void Dispose()
        {
            TerminateEntries();
            _eventSource.Dispose();
        }

        // The entries leave the dictionary before their lifetimes end (as in Remove): their listeners see it empty
        // and may add keys again without breaking the pass.
        private void TerminateEntries()
        {
            var lifetimes = new List<ILifetime>(_lifetimes.Values);

            _lifetimes.Clear();
            _items.Clear();

            foreach (var lifetime in lifetimes)
                lifetime.Terminate();
        }

        public readonly struct ValueCollection : IEnumerable<TView>
        {
            internal ValueCollection(Dictionary<TKey, TSource> items)
            {
                _items = items;
            }

            private readonly Dictionary<TKey, TSource> _items;

            public int Count => _items.Count;

            public Enumerator GetEnumerator()
            {
                return new Enumerator(_items.Values.GetEnumerator());
            }

            IEnumerator<TView> IEnumerable<TView>.GetEnumerator()
            {
                return GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        public struct Enumerator : IEnumerator<TView>
        {
            internal Enumerator(Dictionary<TKey, TSource>.ValueCollection.Enumerator inner)
            {
                _inner = inner;
            }

            private Dictionary<TKey, TSource>.ValueCollection.Enumerator _inner;

            public TView Current => _inner.Current;
            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                return _inner.MoveNext();
            }

            void IEnumerator.Reset()
            {
                throw new NotSupportedException();
            }

            public void Dispose()
            {
                _inner.Dispose();
            }
        }
    }
}
