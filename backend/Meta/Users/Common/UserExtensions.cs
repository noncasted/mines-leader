using Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Meta.Users;

public static class UserExtensions
{
    extension(UserGrain source)
    {
        public Task SendProjection(IProjectionPayload payload)
        {
            var messaging = source.Services.GetRequiredService<IMessaging>();
            var channelId = new UserProjectionChannelId(source.UserId);
            return messaging.PublishChannel(channelId, payload);
        }
    }
}