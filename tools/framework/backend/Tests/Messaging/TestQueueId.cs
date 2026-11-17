using Infrastructure;

namespace Framework.Tests.Messaging;

public class TestQueueId : IDurableQueueId
{
    public TestQueueId(string name)
    {
        _name = name;
    }

    private readonly string _name;
    public string ToRaw() => $"test-queue-{_name}";
}
