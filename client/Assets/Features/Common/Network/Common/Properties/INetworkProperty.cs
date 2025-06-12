using Internal;

namespace Common.Network
{
    public interface INetworkProperty
    {
        bool IsDirty { get; }
        INetworkEntity Entity { get; }

        void Construct(INetworkEntity entity);
        void Update(byte[] value);
        byte[] Collect();
    }
    
    public interface INetworkProperty<T> : INetworkProperty, IViewableProperty<T> where T : new()
    {
    }
}