using MemoryPack;

namespace Game;

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

    private T _value;

    public T Value => _value;

    public void Set(T value)
    {
        _value = value;
        OnUpdated();
    }

    public void OnUpdated()
    {
        var serializedValue = MemoryPackSerializer.Serialize(_value);
        Update(serializedValue);
        Push();
    }
}

public static class ValuePropertyExtensions
{
    public static void Update<T>(this ValueProperty<T> property, Action<T> action) where T : new()
    {
        var value =  property.Value == null ? new T() : property.Value; 
        action(value);
        property.Set(value);
    }
}