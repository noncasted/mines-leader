namespace Backend.Users;

[GenerateSerializer]
public class ProjectionPayloadValue
{
    [Id(0)]
    public required Guid UserId { get; init; }
    
    [Id(1)]
    public required IProjectionPayload Value { get; init; }
}