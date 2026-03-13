using Common.Extensions;

namespace Tests;

public interface IClusterTest
{
    string Group { get; }
    string Title { get; }
    object Payload { get; set; }
    Task Start(IOperationProgress progress);
}