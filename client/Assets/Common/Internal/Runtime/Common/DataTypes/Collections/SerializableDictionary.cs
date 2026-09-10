using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>,
                                                        ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector] private TKey[] _keys;
        [SerializeField, HideInInspector] private TValue[] _values;

        [ShowInInspector, HideLabel]
        private Dictionary<TKey, TValue> _dictionary = new();

        public int Count => _dictionary.Count;

        public TValue this[TKey key]
        {
            get => _dictionary[key];
            set => _dictionary[key] = value;
        }

        public IEnumerable<TKey> Keys => _dictionary.Keys;
        public IEnumerable<TValue> Values => _dictionary.Values;

        public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);
        public bool TryGetValue(TKey key, out TValue value) => _dictionary.TryGetValue(key, out value);

        public void Add(TKey key, TValue value) => _dictionary.Add(key, value);
        public bool Remove(TKey key) => _dictionary.Remove(key);
        public void Clear() => _dictionary.Clear();

        public Dictionary<TKey, TValue>.Enumerator GetEnumerator() => _dictionary.GetEnumerator();

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() =>
            _dictionary.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _dictionary.GetEnumerator();

        public void OnAfterDeserialize()
        {
            _dictionary.Clear();

            if (_values == null || _keys == null)
                return;

            for (var i = 0; i < _keys.Length && i < _values.Length; i++)
                _dictionary[_keys[i]] = _values[i];
        }

        public void OnBeforeSerialize()
        {
            var count = _dictionary.Count;

            _keys = new TKey[count];
            _values = new TValue[count];

            var i = 0;

            foreach (var item in _dictionary)
            {
                _keys[i] = item.Key;
                _values[i] = item.Value;

                i++;
            }
        }
    }
}