namespace Backend.Users;

public static class UserExtensions
{
    public static Task SendCachedProjection(this UserGrain source, IProjectionPayload payload)
    {
        var id = source.GetPrimaryKey();
        var projection = source.Grains.GetGrain<IUserProjection>(id);
        return projection.SendCached(payload);
    }
    
    public static Task CacheProjection(this UserGrain source, IProjectionPayload payload)
    {
        var id = source.GetPrimaryKey();
        var projection = source.Grains.GetGrain<IUserProjection>(id);
        return projection.Cache(payload);
    }

    public static Task SendOneTimeProjection(this UserGrain source, IProjectionPayload payload)
    {
        var id = source.GetPrimaryKey();
        var projection = source.Grains.GetGrain<IUserProjection>(id);
        return projection.SendOneTime(payload);
    }
}