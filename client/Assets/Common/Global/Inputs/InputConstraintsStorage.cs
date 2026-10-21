using System;
using System.Collections.Generic;
using System.Linq;
using Global.UI;

namespace Global.Inputs
{
    public enum InputConstraints
    {
        Game,
    }

    // Which kinds of input are allowed now. A visible UI state adds its constraints (IUIState.Constraints) and
    // removes them when it is hidden or exits; the kind is allowed while no state disables it.
    public interface IInputConstraintsStorage
    {
        void Add(IUIConstraints uiConstraints);
        void Remove(IUIConstraints uiConstraints);
    }

    public class InputConstraintsStorage : IInputConstraintsStorage
    {
        public InputConstraintsStorage()
        {
            var constraints = Enum.GetValues(typeof(InputConstraints)).Cast<InputConstraints>();

            foreach (var constraint in constraints)
                _constraints.Add(constraint, 0);
        }

        // Number of the visible states that disable each kind of input.
        private readonly Dictionary<InputConstraints, int> _constraints = new();

        public bool this[InputConstraints key] => _constraints[key] <= 0;

        // IUIConstraints.Input: false - the state disables this kind of input, true - leaves it allowed.
        public void Add(IUIConstraints uiConstraints)
        {
            foreach (var (key, isAllowed) in uiConstraints.Input)
            {
                if (isAllowed == true)
                    continue;

                _constraints[key]++;
            }
        }

        public void Remove(IUIConstraints uiConstraints)
        {
            foreach (var (key, isAllowed) in uiConstraints.Input)
            {
                if (isAllowed == true)
                    continue;

                if (_constraints[key] == 0)
                    throw new InvalidOperationException($"[InputConstraints] {key} is removed more times than added");

                _constraints[key]--;
            }
        }
    }
}