using MemoryPack;

namespace Game;

public class ValueProperty<T> : ObjectProperty where T : new()
{
    public ValueProperty(int id) : base(id, MemoryPackSerializer.Serialize(new T()))
    {
    }
    
    public ValueProperty(int id, T value) : base(id, MemoryPackSerializer.Serialize(value))
    {
    }

    public void Set(T value)
    {
        
    }
}