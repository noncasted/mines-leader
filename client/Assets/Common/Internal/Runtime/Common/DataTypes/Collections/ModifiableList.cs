using System;
using System.Collections.Generic;

namespace Internal
{
    /// <summary>
    /// Список, который можно менять во время обхода: Add/Remove/Clear, вызванные изнутри
    /// foreach, откладываются до конца самого внешнего прохода. Обход завершается в
    /// <see cref="Enumerator.Dispose"/>, который foreach вызывает из finally, поэтому
    /// break и исключение внутри цикла не оставляют список в состоянии «итерируется».
    /// Вложенные проходы учитываются счётчиком глубины.
    ///
    /// IEnumerable намеренно не реализован: LINQ и передача как IEnumerable боксили бы
    /// энумератор и обходили бы правила списка.
    /// </summary>
    public sealed class ModifiableList<T>
    {
        private readonly List<T> _items = new();
        private readonly List<T> _pendingAdd = new(0);
        private readonly List<T> _pendingRemove = new(0);

        private int _iterationDepth;
        private bool _clearRequested;

        /// <summary>Количество элементов без учёта отложенных изменений.</summary>
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

            // Добавили и сняли в одном проходе: в список он так и не попадёт.
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
