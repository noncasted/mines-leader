using Infrastructure;

namespace Meta.Users;

public static class UserProjectionExtensions
{
    extension(IMessaging messaging)
    {
        public Task SendOneTimeProjection(Guid userId, IProjectionPayload payload)
        {
            var channelId = new UserProjectionChannelId(userId);
            return messaging.PublishChannel(channelId, payload);
        }
    }
}