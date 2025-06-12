using Internal;

namespace Global.Inputs
{
    public class InputView : IInputView, IScopeSetupCompletion
    {
        private readonly ViewableDelegate<IUserInput> _userConnected = new();

        public IViewableDelegate<IUserInput> UserConnected => _userConnected;

        public void OnSetupCompletion(IReadOnlyLifetime lifetime)
        {
            var controls = new Controls();
            controls.Enable();
            var userInput = new UserInput(controls, lifetime);
            
            _userConnected.Invoke(userInput);
            lifetime.Listen(userInput.Dispose);
        }
    }
}