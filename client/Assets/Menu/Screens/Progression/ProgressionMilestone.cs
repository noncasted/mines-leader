using System;
using UnityEngine.UIElements;

namespace Menu.Screens
{
    /// <summary>
    /// Wrapper around a cloned ProgressionChest UXML template.
    /// Mirrors the CardElement pattern from MenuDecks.
    /// </summary>
    public class ProgressionMilestone
    {
        public VisualElement Root { get; }
        public Button Button { get; }
        public int RequiredXp { get; private set; }
        public Guid BoxId { get; private set; }
        public bool HasAvailableBox => BoxId != Guid.Empty;

        private bool _reached;
        private bool _claimed;

        public ProgressionMilestone(VisualElement root)
        {
            Root = root;
            Button = root.Q<Button>("chest-hit");
        }

        public void Setup(int requiredXp)
        {
            RequiredXp = requiredXp;
            UpdateVisual();
        }

        public void SetReached(bool reached)
        {
            _reached = reached;
            UpdateVisual();
        }

        public void SetClaimed(bool claimed)
        {
            _claimed = claimed;
            UpdateVisual();
        }

        public void SetAvailableBox(Guid boxId)
        {
            BoxId = boxId;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            Root.RemoveFromClassList("locked");
            Root.RemoveFromClassList("ready");
            Root.RemoveFromClassList("reached");
            Root.RemoveFromClassList("claimed");

            if (!_reached)
                Root.AddToClassList("locked");
            else if (HasAvailableBox)
                Root.AddToClassList("ready");
            else if (_claimed)
                Root.AddToClassList("claimed");
            else
                Root.AddToClassList("reached");
        }
    }
}
