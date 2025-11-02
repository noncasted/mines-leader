namespace Meta.Users;

public static class UserProjectionExtensions
{
    extension(IGrainFactory grains)
    {
        public Task SendCachedProjection(Guid id, IProjectionPayload payload)
        {
            var projection = grains.GetGrain<IUserProjection>(id);
            return projection.SendCached(payload);
        }

        public Task SendOneTimeProjection(Guid id, IProjectionPayload payload)
        {
            var projection = grains.GetGrain<IUserProjection>(id);
            return projection.SendOneTime(payload);
        }
    }
}