using System.Collections.Generic;
using Internal;
using Meta;
using Menu.Common;
using UnityEngine;

namespace Menu.Decks
{
    // The buttons of the decks of the player: a click makes its deck the selected one.
    public class MenuDeckIndexes
    {
        public MenuDeckIndexes(IDecks decks, RectTransform root)
        {
            _decks = decks;
            _root = root;
        }

        private readonly IDecks _decks;
        private readonly RectTransform _root;
        private readonly List<MenuDeckIndexButton> _buttons = new();

        public void Create(IReadOnlyLifetime lifetime, System.Action<int> onSelected)
        {
            var decksCount = _decks.Configurations.Count;

            for (var i = 0; i < decksCount; i++)
            {
                var indexButton = Object.Instantiate(MenuPrefabs.MenuDeckIndex, _root);
                indexButton.Setup(i);
                _buttons.Add(indexButton);

                var index = i;

                indexButton.Clicked.Advise(lifetime, () => {
                    foreach (var button in _buttons)
                        button.Deactivate();

                    indexButton.Activate();
                    onSelected(index);
                });
            }

            _buttons[_decks.SelectedIndex.Value].Activate();
        }
    }
}
