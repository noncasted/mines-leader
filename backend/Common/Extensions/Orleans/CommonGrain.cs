namespace Common;

public interface ICommonGrain
{
    IGrainFactory Grains { get; }
}

public class CommonGrain : Grain, ICommonGrain
{
    public IGrainFactory Grains => GrainFactory;
}