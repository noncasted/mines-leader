using System.Diagnostics;
using Infrastructure;

namespace Framework.Tests.Fixtures;

public record BatchExecutionInfo(Guid Id, bool Success, TimeSpan Duration, string? ErrorMessage)
{
    public string? ShortError => ErrorMessage?.Split('\n').FirstOrDefault();
}
