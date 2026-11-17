namespace Infrastructure;

public interface IRuntimePipeId
{
    string ToRaw();
}

public class RuntimePipeId : IRuntimePipeId
{
    public RuntimePipeId(string id)
    {
        _id = id;
    }

    private readonly string _id;

    public string ToRaw()
    {
        return _id;
    }
}
