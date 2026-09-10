namespace Internal
{
    public interface IInjector
    {
        object Create(IResolvePlan plan);
        void Construct(object instance, IResolvePlan plan);
    }
}
