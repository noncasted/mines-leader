namespace Game;

public interface IObjectProperty
{
    int Id { get; }
    byte[] Value { get; }

    void Update(byte[] value);
}

public class ObjectProperty : IObjectProperty
{
    public ObjectProperty(int id, byte[] value)
    {
        Id = id;
        _value = value;
    }

    private byte[] _value;
    private int _objectId;
    private IPropertyUpdateSender _updateSender;

    public int Id { get; }
    public byte[] Value => _value;

    public void Construct(IPropertyUpdateSender updateSender, int objectId)
    {
        _objectId = objectId;
        _updateSender = updateSender;
    }
    
    public void Update(byte[] value)
    {
        _value = value;
    }
}