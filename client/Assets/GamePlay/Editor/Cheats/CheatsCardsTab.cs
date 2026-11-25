using System;
using GamePlay.Cheats;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GamePlay.Editor
{
    // Tab of the cheats window: every card by name with a search, a click adds the card to the hand.
    internal static class CheatsCardsTab
    {
        public static void Build(VisualElement content)
        {
            if (GameCheatsAccess.IsActive == false)
            {
                content.Add(CheatsUi.SectionLabel("Not in game - cards unavailable"));
                return;
            }

            var cards = GameCheatsAccess.Current.GetCards();

            var search = new ToolbarSearchField();
            search.style.flexShrink = 0;
            search.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
            search.style.marginBottom = 6;
            content.Add(search);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            content.Add(scroll);

            void Rebuild(string filter)
            {
                scroll.Clear();

                VisualElement currentRow = null;
                var col = 0;

                foreach (var card in cards)
                {
                    if (!Matches(card, filter))
                        continue;

                    if (col % 4 == 0)
                    {
                        currentRow = new VisualElement();
                        currentRow.style.flexDirection = FlexDirection.Row;
                        currentRow.style.marginBottom = 4;
                        scroll.Add(currentRow);
                    }

                    currentRow.Add(BuildCardTile(card));
                    col++;
                }

                if (col == 0)
                    scroll.Add(CheatsUi.SectionLabel("No cards match the search"));
            }

            search.RegisterValueChangedCallback(evt => Rebuild(evt.newValue));
            Rebuild(string.Empty);
        }

        private static bool Matches(CheatCardInfo card, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            return card.Name != null
                   && card.Name.IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Button BuildCardTile(CheatCardInfo card)
        {
            var typeId = card.TypeId;
            var tile = new Button(() => GameCheatsAccess.Require().AddCard(typeId));
            tile.style.flexGrow = 0;
            tile.style.width = new StyleLength(new Length(25f, LengthUnit.Percent));
            tile.style.height = 72;
            tile.style.marginLeft = 2;
            tile.style.marginRight = 2;
            tile.style.paddingTop = 4;
            tile.style.paddingBottom = 4;
            tile.style.paddingLeft = 2;
            tile.style.paddingRight = 2;
            tile.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            tile.style.borderTopLeftRadius = 4;
            tile.style.borderTopRightRadius = 4;
            tile.style.borderBottomLeftRadius = 4;
            tile.style.borderBottomRightRadius = 4;
            tile.style.borderTopWidth = 0;
            tile.style.borderBottomWidth = 0;
            tile.style.borderLeftWidth = 0;
            tile.style.borderRightWidth = 0;
            tile.style.flexDirection = FlexDirection.Column;
            tile.style.alignItems = Align.Center;
            tile.style.justifyContent = Justify.Center;

            var bg = new Color(0.22f, 0.22f, 0.22f);
            var hover = new Color(0.3f, 0.3f, 0.3f);
            tile.RegisterCallback<MouseEnterEvent>(_ => tile.style.backgroundColor = hover);
            tile.RegisterCallback<MouseLeaveEvent>(_ => tile.style.backgroundColor = bg);

            var icon = new VisualElement();
            icon.style.width = 32;
            icon.style.height = 32;
            icon.style.flexShrink = 0;
            icon.style.marginBottom = 4;
            if (card.Icon != null)
                icon.style.backgroundImage = new StyleBackground(card.Icon);
            tile.Add(icon);

            var label = new Label(card.Name);
            label.style.fontSize = 10;
            label.style.color = new Color(0.75f, 0.75f, 0.75f);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.maxWidth = new StyleLength(new Length(100f, LengthUnit.Percent));
            tile.Add(label);

            return tile;
        }
    }
}
