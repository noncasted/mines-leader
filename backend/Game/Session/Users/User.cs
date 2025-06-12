using Common;

namespace Game;

public class User : IUser
{
    public required Guid Id { get; init; }
    public required int Index { get; init; }
    public required ILifetime Lifetime { get; init; }
    public required IChannelReader Reader { get; init; }
    public required IChannelWriter Writer { get; init; }
}