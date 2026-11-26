using Infrastructure;

namespace Framework.Tests.Messaging;

public class TestChannelId : IRuntimeChannelId
{
    public TestChannelId(string name)
    {
        _name = name;
    }

    private readonly string _name;
    public string ToRaw() => $"test-channel-{_name}";
}
