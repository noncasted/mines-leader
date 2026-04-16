using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.UI.Toolkit;
using Internal;
using Menu.Decks;
using Meta;
using Shared;
using UnityEngine.UIElements;

namespace Menu.Screens
{
    /// <summary>
    /// Modal overlay that lets the player pick one of N reward cards.
    /// Built from LootBoxChoice.uxml and MenuCard.uxml templates.
    /// </summary>
    public class LootBoxChoicePanel
    {
        private readonly VisualTreeAsset _overlayTemplate;
        private readonly VisualTreeAsset _cardTemplate;
        private readonly ICardConfigs _configs;

        public LootBoxChoicePanel(
            VisualTreeAsset overlayTemplate,
            VisualTreeAsset cardTemplate,
            ICardConfigs configs)
        {
            _overlayTemplate = overlayTemplate;
            _cardTemplate = cardTemplate;
            _configs = configs;
        }

        public async UniTask<CardType> ShowAndAwait(
            VisualElement parent,
            IReadOnlyLifetime lifetime,
            IReadOnlyList<ICardDefinition> definitions,
            IReadOnlyList<CardType> cardTypes)
        {
            var overlayContainer = _overlayTemplate.CloneTree();
            var overlay = overlayContainer.Q<VisualElement>("loot-overlay");
            overlay.RemoveFromHierarchy();
            parent.Add(overlay);

            var row = overlay.Q<VisualElement>("loot-card-row");
            var completion = new UniTaskCompletionSource<CardType>();

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                var cardType = cardTypes[i];
                var config = _configs.Value.All[cardType];

                var cardButton = new Button();
                cardButton.AddToClassList("loot-card");

                var inner = _cardTemplate.CloneTree();
                var cardRoot = inner.Q<VisualElement>("card-root");
                cardRoot.RemoveFromHierarchy();
                cardRoot.AddToClassList("loot-card-inner");

                var cardElement = new CardElement(cardRoot);
                cardElement.SetCard(definition, config);

                cardButton.Add(cardRoot);
                cardButton.ListenClick(lifetime, () => completion.TrySetResult(cardType));
                row.Add(cardButton);
            }

            lifetime.Listen(() => overlay.RemoveFromHierarchy());

            var result = await completion.Task;
            overlay.RemoveFromHierarchy();
            return result;
        }
    }
}
