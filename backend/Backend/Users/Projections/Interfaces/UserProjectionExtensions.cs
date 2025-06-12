namespace Backend.Users.Projections;

public static class UserProjectionExtensions
{
    public static Task SendCachedProjection(this IGrainFactory grains, Guid id, IProjectionPayload payload)
    {
        var projection = grains.GetGrain<IUserProjection>(id);
        return projection.SendCached(payload);
    }
    
    public static Task SendOneTimeProjection(this IGrainFactory grains, Guid id, IProjectionPayload payload)
    {
        var projection = grains.GetGrain<IUserProjection>(id);
        return projection.SendOneTime(payload);
    }
}