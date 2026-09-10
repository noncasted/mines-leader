using System;
using System.Collections;
using System.Collections.Generic;

namespace ContainerGenerator {
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
        where T : IEquatable<T> {
        public static readonly EquatableArray<T> Empty = new EquatableArray<T>(Array.Empty<T>());

        private readonly T[]? _array;

        public EquatableArray(T[] array) {
            _array = array ?? Array.Empty<T>();
        }

        public int Count => _array == null ? 0 : _array.Length;

        public T this[int index] => _array![index];

        public bool Equals(EquatableArray<T> other) {
            var left = _array ?? Array.Empty<T>();
            var right = other._array ?? Array.Empty<T>();
            if (left.Length != right.Length)
                return false;

            for (var i = 0; i < left.Length; i++) {
                if (left[i].Equals(right[i]) == false)
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj) {
            return obj is EquatableArray<T> other && Equals(other);
        }

        public override int GetHashCode() {
            if (_array == null || _array.Length == 0)
                return 0;

            var hash = 17;
            for (var i = 0; i < _array.Length; i++)
                hash = HashCodes.Combine(hash, _array[i].GetHashCode());

            return hash;
        }

        public IEnumerator<T> GetEnumerator() {
            return ((IEnumerable<T>)(_array ?? Array.Empty<T>())).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
