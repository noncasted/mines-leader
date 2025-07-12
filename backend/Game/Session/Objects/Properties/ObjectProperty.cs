namespace Game;

public interface IObjectProperty
{
    int Id { get; }
    byte[] RawValue { get; }

    void Construct(IPropertyUpdateSender updateSender, int objectId);
    void Update(byte[] value);
}

public class ObjectProperty : IObjectProperty
{
    public ObjectProperty(int id, byte[] rawValue)
    {
        Id = id;
        _rawValue = rawValue;
    }

    private byte[] _rawValue;
    private int _objectId;
    private IPropertyUpdateSender _updateSender;

    public int Id { get; }
    public byte[] RawValue => _rawValue;

    public void Construct(IPropertyUpdateSender updateSender, int objectId)
    {
        _objectId = objectId;
        _updateSender = updateSender;
    }

    public void Update(byte[] value)
    {
        _rawValue = value;
    }

    public void Push()
    {
        _updateSender.Send(_objectId, this);
    }
}