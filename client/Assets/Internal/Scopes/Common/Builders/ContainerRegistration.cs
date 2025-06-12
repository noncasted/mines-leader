using VContainer;

namespace Internal
{
    public class ContainerRegistration : IRegistration
    {
        public ContainerRegistration(IBuilder builder, RegistrationBuilder registrationBuilder)
        {
            Registration = registrationBuilder;
            Builder = builder;
        }

        public IServiceCollection ServiceCollection => Builder.Services;
        public RegistrationBuilder Registration { get; }
        public IBuilder Builder { get; }
    }
}