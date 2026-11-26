using System;
using System.Collections.Generic;
using System.Linq;
using Internal;
using MemoryPack;

namespace Game.Session
{
    public class ValueProperty<T> : ObjectProperty where T : new()
    {
        public ValueProperty(int id) : base(id, MemoryPackSerializer.Serialize(new T()))
        {
            _value = new T();
        }

        public ValueProperty(int id, T value) : base(id, MemoryPackSerializer.Serialize(value))
        {
            _value = value;
        }

        private readonly ViewableDelegate<T> _written = new();

        private T _value;

        public T Value => _value;

        // A client has written the property (SetPropertyCommand): the new value. Writes of the backend itself
        // (Set, OnUpdated) are not reported.
        public IViewableDelegate<T> Written => _written;

        public void Set(T value)
        {
            _value = value;
            OnUpdated();
        }

        public void OnUpdated()
        {
            var serializedValue = MemoryPackSerializer.Serialize(_value);
            SetRaw(serializedValue);
            Push();
        }

        // The value is kept deserialized as well: the backend reads what the client has written.
        public override void Update(byte[] value)
        {
            var deserialized = MemoryPackSerializer.Deserialize<T>(value);

            if (deserialized == null)
                throw new InvalidOperationException($"Property {Id} is written with a null {typeof(T).Name}");

            base.Update(value);
            _value = deserialized;
            _written.Invoke(deserialized);
        }
    }

    public static class ValuePropertyExtensions
    {
        public static void Update<T>(this ValueProperty<T> property, Action<T> action) where T : new()
        {
            var value = property.Value == null ? new T() : property.Value;
            action(value);
            property.Set(value);
        }
    }
}
