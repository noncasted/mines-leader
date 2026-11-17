namespace Infrastructure;

public interface IDurableQueueId
{
    string ToRaw();
}

public class DurableQueueId : IDurableQueueId
{
    public DurableQueueId(string id)
    {
        _id = id;
    }

    private readonly string _id;

    public string ToRaw()
    {
        return _id;
    }
}
