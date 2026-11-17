using Infrastructure;

namespace Framework.Tests.Messaging;

[GenerateSerializer]
public class TestMessage
{
    [Id(0)] public string Text { get; init; } = string.Empty;
    [Id(1)] public int Sequence { get; init; }
}
