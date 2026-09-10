namespace Internal
{
    public class ContainerRegistration : IRegistration
    {
        public ContainerRegistration(IBuilder builder, IServiceRegistration registration)
        {
            Registration = registration;
            Builder = builder;
        }

        public IServiceCollection ServiceCollection => Builder.Services;
        public IServiceRegistration Registration { get; }
        public IBuilder Builder { get; }
    }
}
