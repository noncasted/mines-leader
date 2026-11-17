using Infrastructure;

namespace Framework.Tests.Messaging;

public class TestPipeId : IRuntimePipeId
{
    public TestPipeId(string name)
    {
        _name = name;
    }

    private readonly string _name;
    public string ToRaw() => $"test-pipe-{_name}";
}
