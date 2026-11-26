using Infrastructure;

namespace Framework.Tests.Messaging;

[GenerateSerializer]
public class TestRequest
{
    [Id(0)] public string Question { get; init; } = string.Empty;
}
