using Infrastructure.Discovery;

namespace Infrastructure.Messaging;

public interface IMessageOptions
{
    bool IsTarget(MessagingObserverOverview overview);
}

[GenerateSerializer]
public class MessageOptions : IMessageOptions
{
    [Id(0)] public required Guid TargetId { get; init; }

    public bool IsTarget(MessagingObserverOverview overview)
    {
        return overview.ServiceId == TargetId;
    }
}

[GenerateSerializer]
public class TagMessageOptions : IMessageOptions
{
    [Id(0)] public required ServiceTag Tag { get; init; }

    public bool IsTarget(MessagingObserverOverview overview)
    {
        return overview.Tag == Tag;
    }
}