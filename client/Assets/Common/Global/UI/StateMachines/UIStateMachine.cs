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

        private IUIState _stackHead;

        private readonly IInputConstraintsStorage _constraintsStorage;
        private readonly Dictionary<IUIState, IInternalUIStateHandle> _handles;

        public IUIState Base { get; }
        public IUIState StackHead => _stackHead;

        public IUIStateHandle CreateChild(IReadOnlyLifetime lifetime, IUIState parent, IUIState state)
        {
            var headHandle = _handles[parent];
            var childHandle = new UIStateHandle(lifetime, headHandle, state, _constraintsStorage);
            Add(state, childHandle);
            childHandle.OnChild();

            return childHandle;
        }

        public IUIStateHandle CreateStackChild(IReadOnlyLifetime lifetime, IUIState parent, IUIState state)
        {
            var headHandle = _handles[parent];
            var childHandle = new UIStateHandle(lifetime, headHandle, state, _constraintsStorage);
            Add(state, childHandle);
            headHandle.OnStacked(childHandle);

            var currentHead = _stackHead;
            _stackHead = state;
            childHandle.InnerLifetime.Listen(() => _stackHead = currentHead);

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

        // The handle is known while it lives: a state entered again (the same window reopened) replaces it, and the
        // end of the old one does not remove the new one.
        private void Add(IUIState state, UIStateHandle handle)
        {
            _handles[state] = handle;

            handle.InnerLifetime.Listen(() => {
                if (_handles.TryGetValue(state, out var current) == true && current == handle)
                    _handles.Remove(state);
            });
        }
    }
}