using VContainer;

namespace Internal
{
    public interface IRegistration
    {
        IServiceCollection ServiceCollection { get; }
        RegistrationBuilder Registration { get; }
        IBuilder Builder { get; }
    }
}