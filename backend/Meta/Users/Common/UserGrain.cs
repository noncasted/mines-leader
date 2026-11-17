using Infrastructure;

namespace Meta.Users;

public interface IUserGrain : IGrainWithGuidKey
{
}

public interface IUserProjectionSource : IGrainWithGuidKey
{
    [Transaction]
    Task<IProjectionPayload> GetProjection();
}

public class UserGrain : Grain
{
    public IGrainFactory Grains => GrainFactory;
    public IServiceProvider Services => ServiceProvider;
    public Guid UserId => this.GetPrimaryKey();
}