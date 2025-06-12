using Common;
using Microsoft.Extensions.DependencyInjection;

namespace Game;

public interface ISession
{
    Guid Id { get; }
    IReadOnlyLifetime Lifetime { get; }
    ISessionMetadata Metadata { get; }
    ISessionUsers Users { get; }
    IUserFactory UserFactory { get; }
    ISessionEntities Entities { get; }
    IEntityFactory EntityFactory { get; }
    IExecutionQueue ExecutionQueue { get; }

    Task Run();
}

public class SessionCreateOptions
{
    public required int ExpectedUsers { get; init; }
    public required string Type { get; init; }
}

public class SessionContainerData
{
    public required SessionCreateOptions CreateOptions { get; init; }
    public required Guid Id { get; init; }
    public required IServiceScope Scope { get; init; }
    public required ILifetime Lifetime { get; init; }
}