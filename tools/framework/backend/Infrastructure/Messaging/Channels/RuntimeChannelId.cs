namespace Infrastructure;

public interface IRuntimeChannelId
{
    string ToRaw();
}

public class RuntimeChannelId : IRuntimeChannelId
{
    public RuntimeChannelId(string id)
    {
        _id = id;
    }

    private readonly string _id;

    public string ToRaw()
    {
        return _id;
    }
}
