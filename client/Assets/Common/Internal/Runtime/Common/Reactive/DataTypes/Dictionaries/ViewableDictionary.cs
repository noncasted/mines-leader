using System;
using System.Collections.Generic;

namespace Internal
{
    public class ViewableDictionary<TKey, TSource> : ViewableDictionary<TKey, TSource, TSource>
    {
    }

    public class ViewableDictionary<TKey, TSource, TView> :
        Dictionary<TKey, TSource>,
        IViewableDictionary<TKey, TView>
        where TSource : TView
    {
        private readonly EventSource<IReadOnlyLifetime, TKey, TView> _eventSource = new();
        private readonly Dictionary<TKey, ILifetime> _lifetimes = new();

        public new TView this[TKey key]
        {
            get => base[key];
            set
            {
                if (ContainsKey(key) == false)
                {
                    Add(key, (TSource)value);
                }
                else
                {
                    _lifetimes[key].Terminate();

                    var lifetime = new Lifetime();
                    _lifetimes[key] = lifetime;

                    base[key] = (TSource)value;
                    _eventSource.Invoke(lifetime, key, value);
                }

                OnModified();
            }
        }

        public new IEnumerable<TKey> Keys => base.Keys;

        public new IEnumerable<TView> Values
        {
            get
            {
                IEnumerable<TSource> values = base.Values;

                foreach (var value in values)
                    yield return value;
            }
        }

        public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, TKey, TView> handler)
        {
            _eventSource.Advise(lifetime, handler);
        }

        public IReadOnlyLifetime GetLifetime(TKey value)
        {
            return _lifetimes[value];
        }

        public new IReadOnlyLifetime Add(TKey key, TSource value)
        {
            base.Add(key, value);
            var lifetime = new Lifetime();
            _lifetimes.Add(key, lifetime);

            _eventSource.Invoke(lifetime, key, value);

            OnModified();

            return lifetime;
        }

        public new void Remove(TKey key)
        {
            // The lifetime leaves the map with the entry: a key added again gets a new one (Add threw on the
            // stale entry before), and a removed entry is not referenced any more.
            if (_lifetimes.Remove(key, out var lifetime) == false)
                throw new KeyNotFoundException($"{key} is not in the dictionary");

            base.Remove(key);
            lifetime.Terminate();

            OnModified();
        }


        protected virtual void OnModified()
        {
        }

        // Enumerates through the base Dictionary type: when TSource == TView the interface
        // IEnumerable<KeyValuePair<TKey, TSource>> is implemented by this very method, and going
        // through it recursed until stack overflow.
        private Dictionary<TKey, TSource> Source => this;

        public new IEnumerator<KeyValuePair<TKey, TView>> GetEnumerator()
        {
            foreach (var pair in Source)
                yield return new KeyValuePair<TKey, TView>(pair.Key, pair.Value);
        }

        public IEnumerable<KeyValuePair<TKey, TSource>> GetEnumerable()
        {
            foreach (var pair in Source)
                yield return new KeyValuePair<TKey, TSource>(pair.Key, pair.Value);
        }

        public bool TryGetValue(TKey key, out TView value)
        {
            var result = base.TryGetValue(key, out var source);
            value = source;
            return result;
        }

        public bool TryGetSourceValue(TKey key, out TSource value)
        {
            return base.TryGetValue(key, out value);
        }

        // The entries leave the dictionary before their lifetimes end (as in Remove): their listeners see it empty.
        public void Dispose()
        {
            var lifetimes = new List<ILifetime>(_lifetimes.Values);

            _lifetimes.Clear();
            base.Clear();

            foreach (var lifetime in lifetimes)
                lifetime.Terminate();

            _eventSource.Dispose();
        }
    }
}