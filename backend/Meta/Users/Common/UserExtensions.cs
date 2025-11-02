namespace Meta.Users;

public static class UserExtensions
{
    extension(UserGrain source)
    {
        public Task SendCachedProjection(IProjectionPayload payload)
        {
            var id = source.GetPrimaryKey();
            var projection = source.Grains.GetGrain<IUserProjection>(id);
            return projection.SendCached(payload);
        }

        public Task CacheProjection(IProjectionPayload payload)
        {
            var id = source.GetPrimaryKey();
            var projection = source.Grains.GetGrain<IUserProjection>(id);
            return projection.Cache(payload);
        }

        public Task SendOneTimeProjection(IProjectionPayload payload)
        {
            var id = source.GetPrimaryKey();
            var projection = source.Grains.GetGrain<IUserProjection>(id);
            return projection.SendOneTime(payload);
        }
    }
}