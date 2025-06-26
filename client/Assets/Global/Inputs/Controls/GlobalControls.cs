using Internal;

namespace Global.Inputs
{
    public interface IGlobalControls
    {
        Controls Controls { get; }
    }

    public class GlobalControls : IGlobalControls, IScopeSetup
    {
        public GlobalControls()
        {
            _controls = new Controls();
        }

        private readonly Controls _controls;

        public Controls Controls => _controls;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _controls.Enable();
            
            lifetime.Listen(() =>
            {
                _controls.Disable();
                _controls.Dispose();
            });
        }
    }
}