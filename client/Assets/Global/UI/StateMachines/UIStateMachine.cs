using System.Collections.Generic;
using Global.Inputs;
using Internal;

namespace Global.UI
{
    public class UIStateMachine : IUIStateMachine
    {
        public UIStateMachine(IInputConstraintsStorage constraintsStorage, IReadOnlyLifetime lifetime)
        {
            _constraintsStorage = constraintsStorage;

            var state = new BaseUIState(lifetime);
            Base = state;

            _handles = new Dictionary<IUIState, IInternalUIStateHandle>()
            {
                { state, state }
            };
        }

        private readonly IInputConstraintsStorage _constraintsStorage;
        private readonly Dictionary<IUIState, IInternalUIStateHandle> _handles;

        public IUIState Base { get; }

        public IUIStateHandle CreateChild(IUIState parent, IUIState state)
        {
            var headHandle = _handles[parent];
            var childHandle = new UIStateHandle(headHandle, state, _constraintsStorage);
            _handles[state] = childHandle;
            childHandle.OnChild();

            return childHandle;
        }

        public IUIStateHandle CreateStackChild(IUIState parent, IUIState state)
        {
            var headHandle = _handles[parent];
            var childHandle = new UIStateHandle(headHandle, state, _constraintsStorage);
            _handles[state] = childHandle;
            headHandle.OnStacked(childHandle);

            return childHandle;
        }

        public void ClearStack(IUIState state)
        {
            _handles[state].ClearStack();
        }

        public void Exit(IUIState state)
        {
            _handles[state].Exit();
        }
    }
}