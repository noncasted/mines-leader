namespace Backend.Users;

[GenerateSerializer]
public class UserProjectionConnectionState
{
    [Id(0)]
    public Guid ConnectionServiceId { get; set; }
}