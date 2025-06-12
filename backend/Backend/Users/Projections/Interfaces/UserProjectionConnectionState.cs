namespace Backend.Users.Projections;

[GenerateSerializer]
public class UserProjectionConnectionState
{
    [Id(0)]
    public Guid ConnectionServiceId { get; set; }
}