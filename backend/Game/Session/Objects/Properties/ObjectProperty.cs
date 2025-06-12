namespace Game;

public interface IObjectProperty
{
    int Id { get; }
    bool IsDirty { get; }
    IReadOnlyList<byte> Value { get; }

    void Update(IReadOnlyList<byte> value);
    void MarkClean();
}

public class ObjectProperty : IObjectProperty
{
    public ObjectProperty(int id, IReadOnlyList<byte> value)
    {
        Id = id;
        _value = value;
    }

    private IReadOnlyList<byte> _value;
    private bool _isDirty;

    public int Id { get; }
    public bool IsDirty => _isDirty;
    public IReadOnlyList<byte> Value => _value;

    public void Update(IReadOnlyList<byte> value)
    {
        _isDirty = true;
        _value = value;
    }

    public void MarkClean()
    {
        _isDirty = false;
    }
}