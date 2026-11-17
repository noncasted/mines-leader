using System;
using System.Collections.Generic;

namespace Internal
{
    /// <summary>
    /// A list that can be changed during iteration: Add/Remove/Clear called from inside
    /// foreach are deferred until the end of the outermost pass. The pass completes in
    /// <see cref="Enumerator.Dispose"/>, which foreach calls from finally, so
    /// a break or an exception inside the loop does not leave the list in the "iterating" state.
    /// Nested passes are tracked by a depth counter.
    ///
    /// IEnumerable is intentionally not implemented: LINQ and passing it as IEnumerable would box
    /// the enumerator and bypass the list rules.
    /// </summary>
    public sealed class ModifiableList<T>
    {
        private readonly List<T> _items = new();
        private readonly List<T> _pendingAdd = new(0);
        private readonly List<T> _pendingRemove = new(0);

        private int _iterationDepth;
        private bool _clearRequested;

        /// <summary>Number of items, not counting deferred changes.</summary>
        public int Count => _items.Count;

        public void Add(T item)
        {
            if (_iterationDepth > 0)
                _pendingAdd.Add(item);
            else
                _items.Add(item);
        }

        public void Remove(T item)
        {
            if (_iterationDepth == 0)
            {
                _items.Remove(item);
                return;
            }

            // Added and removed within one pass: it never gets into the list.
            if (_pendingAdd.Remove(item) == true)
                return;

            _pendingRemove.Add(item);
        }

        public void Clear()
        {
            _pendingAdd.Clear();
            _pendingRemove.Clear();

            if (_iterationDepth > 0)
                _clearRequested = true;
            else
                _items.Clear();
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        private void EndIteration()
        {
            if (--_iterationDepth > 0)
                return;

            if (_clearRequested == true)
            {
                _items.Clear();
                _clearRequested = false;
            }

            foreach (var item in _pendingRemove)
                _items.Remove(item);

            _items.AddRange(_pendingAdd);

            _pendingAdd.Clear();
            _pendingRemove.Clear();
        }

        public struct Enumerator : IDisposable
        {
            private readonly ModifiableList<T> _owner;
            private int _index;

            internal Enumerator(ModifiableList<T> owner)
            {
                _owner = owner;
                _index = -1;
                owner._iterationDepth++;
            }

            public T Current => _owner._items[_index];

            public bool MoveNext()
            {
                return ++_index < _owner._items.Count;
            }

            public void Dispose()
            {
                _owner.EndIteration();
            }
        }
    }
}