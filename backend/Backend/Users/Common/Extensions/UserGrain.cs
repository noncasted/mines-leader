namespace Backend.Users;

public interface IUserGrain : IGrainWithGuidKey
{
}

public class UserGrain : Grain
{
    public IGrainFactory Grains => GrainFactory;
}