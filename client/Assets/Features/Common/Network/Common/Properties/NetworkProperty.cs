using System;
using Internal;
using MemoryPack;

namespace Common.Network.Common
{
    public interface INetworkProperty
    {
        bool IsDirty { get; }
        int Id { get; }

        void Update(byte[] value);
        byte[] Collect();
    }

    public interface INetworkProperty<T> : INetworkProperty, IViewableProperty<T> where T : new()
    {
    }

    public class NetworkProperty<T> : INetworkProperty<T> where T : new()
    {
        public NetworkProperty(int id)
        {
            Id = id;
        }
        
        private readonly LifetimedValue<T> _lifetimedValue = new(new T());

        private bool _isDirty;

        public bool IsDirty => _isDirty;
        public int Id { get; }
        public T Value => _lifetimedValue.Value;
        public IReadOnlyLifetime ValueLifetime => _lifetimedValue.ValueLifetime;

        public void Set(T value)
        {
            _lifetimedValue.Set(value);
            _isDirty = true;
        }

        public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, T> handler)
        {
            _lifetimedValue.Advise(lifetime, handler);
        }

        public void Dispose()
        {
            _lifetimedValue.Dispose();
        }

        public void Update(byte[] value)
        {
            var deserialized = MemoryPackSerializer.Deserialize<T>(value);
            _lifetimedValue.Set(deserialized);
        }

        public byte[] Collect()
        {
            _isDirty = false;
            var serialized = MemoryPackSerializer.Serialize(Value);
            return serialized;
        }

        public void MarkDirty()
        {
            _isDirty = true;
            _lifetimedValue.Notify();
        }
    }
}