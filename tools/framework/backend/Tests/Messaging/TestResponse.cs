using Infrastructure;

namespace Framework.Tests.Messaging;

[GenerateSerializer]
public class TestResponse
{
    [Id(0)] public string Answer { get; init; } = string.Empty;
}
