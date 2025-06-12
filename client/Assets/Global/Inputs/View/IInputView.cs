using Internal;

namespace Global.Inputs
{
    public interface IInputView
    {
        IViewableDelegate<IUserInput> UserConnected { get; }
    }
}