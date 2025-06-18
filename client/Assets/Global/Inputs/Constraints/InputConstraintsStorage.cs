using System;
using System.Collections.Generic;
using System.Linq;
using Global.UI;

namespace Global.Inputs
{
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

        private readonly Dictionary<InputConstraints, int> _constraints = new();

        public bool this[InputConstraints key] => _constraints[key] <= 0;

        public void Add(IUIConstraints uiConstraints)
        {
            foreach (var (key, value) in uiConstraints.Input)
            {
                if (value == false)
                    continue;

                _constraints[key]++;
            }
        }

        public void Remove(IUIConstraints uiConstraints)
        {
            foreach (var (key, value) in uiConstraints.Input)
            {
                if (value == false)
                    continue;

                _constraints[key]--;

                var count = _constraints[key];

                if (count < 0)
                    _constraints[key] = 0;
            }
        }
    }
}