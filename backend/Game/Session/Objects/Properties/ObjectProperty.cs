namespace Game.Session;

public interface IObjectProperty
{
    int Id { get; }
    int Version { get; }
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
    private int _version;
    private IPropertyUpdateSender? _updateSender;

    public int Id { get; }
    public int Version => _version;
    public byte[] RawValue => _rawValue;

    public void Construct(IPropertyUpdateSender updateSender, int objectId)
    {
        _objectId = objectId;
        _updateSender = updateSender;
    }

    public void Update(byte[] value)
    {
        _rawValue = value;
        _version++;
    }

    public void Push()
    {
        _updateSender!.Send(_objectId, this);
    }
}