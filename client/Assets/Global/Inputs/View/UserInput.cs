using Internal;

namespace Global.Inputs
{
    public class UserInput : IUserInput
    {
        public UserInput(Controls controls, IReadOnlyLifetime parent)
        {
            _controls = controls;
            _lifetime = new Lifetime(parent);
        }

        private readonly Controls _controls;
        private readonly Lifetime _lifetime;

        public Controls Controls => _controls;
        public IReadOnlyLifetime Lifetime => _lifetime;

        public void Dispose()
        {
            _controls.Disable();
            _controls.Dispose();
            _lifetime.Terminate();
        }
    }
}