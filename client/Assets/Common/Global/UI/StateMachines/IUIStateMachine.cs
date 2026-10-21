using Internal;

namespace Global.UI
{
    public interface IUIStateMachine
    {
        IUIState Base { get; }
        IUIState StackHead { get; }

        // The machine is global: lifetime is the owner of the handle (the scope or screen that opens it), the handle
        // exits when either it or the parent state ends.
        IUIStateHandle CreateChild(IReadOnlyLifetime lifetime, IUIState parent, IUIState state);
        IUIStateHandle CreateStackChild(IReadOnlyLifetime lifetime, IUIState parent, IUIState state);
        void ClearStack(IUIState state);
        void Exit(IUIState state);
    }
}