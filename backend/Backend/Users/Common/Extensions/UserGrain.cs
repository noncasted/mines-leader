namespace Backend.Users;

public class UserGrain : Grain
{
    public IGrainFactory Grains => GrainFactory;
}