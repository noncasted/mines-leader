using System;
using System.Collections;
using System.Collections.Generic;

namespace Internal
{
    public class ViewableList<TSource> : ViewableList<TSource, TSource>
    {
    }

    // The entries are kept in a private list, not inherited from List<T>: the mutators of the base (or of
    // ICollection<T> / IList) would change the entries without their lifetimes.
    public class ViewableList<TSource, TView> : IViewableList<TView> where TSource : TView
    {
        private readonly List<TSource> _items = new();
        private readonly EventSource<IReadOnlyLifetime, TView> _eventSource = new();
        private readonly Dictionary<TView, ILifetime> _lifetimes = new();

        private bool _isDisposed;

        public int Count => _items.Count;

        public TView this[int index] => _items[index];

        public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, TView> handler)
        {
            _eventSource.Advise(lifetime, handler);
        }

        public IReadOnlyLifetime GetLifetime(TView value)
        {
            return _lifetimes[value];
        }

        public bool Contains(TSource value)
        {
            return _lifetimes.ContainsKey(value);
        }

        public int IndexOf(TSource value)
        {
            return _items.IndexOf(value);
        }

        public void NotifyChangedAt(int index)
        {
            if (_isDisposed == true)
                return;

            var value = _items[index];

            if (_lifetimes.TryGetValue(value, out var lifetime))
            {
                _eventSource.Invoke(lifetime, value);
                OnModified();
            }
        }

        public IReadOnlyLifetime Add(TSource value)
        {
            return Insert(_items.Count, value);
        }

        // An entry is its own key for its lifetime: a duplicate is refused before the list changes.
        public IReadOnlyLifetime Insert(int index, TSource value)
        {
            if (_isDisposed == true)
                return new TerminatedLifetime();

            if (_lifetimes.ContainsKey(value) == true)
                throw new ArgumentException($"{value} is in the list already", nameof(value));

            var lifetime = new Lifetime();
            _items.Insert(index, value);
            _lifetimes.Add(value, lifetime);

            _eventSource.Invoke(lifetime, value);

            OnModified();

            return lifetime;
        }

        public void Remove(TSource value)
        {
            if (_isDisposed == true)
                return;

            // The entry leaves the list before its lifetime ends: its listeners see the list without it. The lifetime
            // leaves the map too, otherwise every removed entry stays referenced for good.
            if (_lifetimes.Remove(value, out var lifetime) == false)
                throw new KeyNotFoundException($"{value} is not in the list");

            _items.Remove(value);
            lifetime.Terminate();

            OnModified();
        }

        public void AddRange(IEnumerable<TSource> collection)
        {
            foreach (var value in collection)
                Add(value);
        }

        public void RemoveRange(IEnumerable<TSource> collection)
        {
            foreach (var value in collection)
                Remove(value);
        }

        public void RemoveAt(int index)
        {
            Remove(_items[index]);
        }

        public List<TSource>.Enumerator GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator<TView> IEnumerable<TView>.GetEnumerator()
        {
            foreach (var source in _items)
                yield return source;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<TView>)this).GetEnumerator();
        }

        public IEnumerable<TSource> GetEnumerable()
        {
            return _items;
        }

        // The entries leave the list before their lifetimes end (as in Remove): their listeners see it empty and may
        // add entries again without breaking the pass.
        public void Clear()
        {
            var lifetimes = new List<ILifetime>(_lifetimes.Values);

            _lifetimes.Clear();
            _items.Clear();

            foreach (var lifetime in lifetimes)
                lifetime.Terminate();

            OnModified();
        }

        protected virtual void OnModified()
        {
        }

        public void Dispose()
        {
            Clear();
            _eventSource.Dispose();
            _isDisposed = true;
        }
    }
}
