namespace Game;

public interface IObjectProperty
{
    int Id { get; }
    bool IsDirty { get; }
    byte[] Value { get; }

    void Update(byte[] value);
    void MarkClean();
}

public class ObjectProperty : IObjectProperty
{
    public ObjectProperty(int id, byte[] value)
    {
        Id = id;
        _value = value;
    }

    private byte[] _value;
    private bool _isDirty;

    public int Id { get; }
    public bool IsDirty => _isDirty;
    public byte[] Value => _value;

    public void Update(byte[] value)
    {
        _isDirty = true;
        _value = value;
    }

    public void MarkClean()
    {
        _isDirty = false;
    }
}