namespace Internal
{
    public interface IRegistration
    {
        IServiceCollection ServiceCollection { get; }
        IServiceRegistration Registration { get; }
        IBuilder Builder { get; }
    }
}
