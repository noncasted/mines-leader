using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Meta;
using Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Screens
{
    [DisallowMultipleComponent]
    public class LootBoxChoicePanel : MonoBehaviour
    {
        private UniTaskCompletionSource<CardType> _completion;
        private readonly List<GameObject> _cardObjects = new();

        public void Setup(
            List<ICardDefinition> definitions,
            List<CardType> cardTypes,
            UniTaskCompletionSource<CardType> completion)
        {
            _completion = completion;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;

            gameObject.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("Background");
            bg.transform.SetParent(transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.7f);

            var title = new GameObject("Title");
            title.transform.SetParent(transform, false);
            var titleRect = title.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.8f);
            titleRect.anchorMax = new Vector2(0.5f, 0.8f);
            titleRect.sizeDelta = new Vector2(400, 60);
            var titleText = title.AddComponent<TextMeshProUGUI>();
            titleText.text = "Choose a Card";
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontSize = 32;

            var spacing = 220f;
            var startX = -(definitions.Count - 1) * spacing / 2f;

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                var cardType = cardTypes[i];

                var card = CreateCardChoice(definition, cardType, startX + i * spacing);
                _cardObjects.Add(card);
            }
        }

        private GameObject CreateCardChoice(ICardDefinition definition, CardType cardType, float xPos)
        {
            var card = new GameObject($"Card_{cardType}");
            card.transform.SetParent(transform, false);

            var rect = card.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(xPos, 0);
            rect.sizeDelta = new Vector2(180, 250);

            var image = card.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.2f, 1f);

            var button = card.AddComponent<Button>();
            button.onClick.AddListener(() => OnCardChosen(cardType));

            if (definition.Image != null)
            {
                var iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(card.transform, false);
                var iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.1f, 0.4f);
                iconRect.anchorMax = new Vector2(0.9f, 0.9f);
                iconRect.sizeDelta = Vector2.zero;
                var iconImage = iconObj.AddComponent<Image>();
                iconImage.sprite = definition.Image;
                iconImage.preserveAspect = true;
            }

            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(card.transform, false);
            var nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.05f, 0.15f);
            nameRect.anchorMax = new Vector2(0.95f, 0.35f);
            nameRect.sizeDelta = Vector2.zero;
            var nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = definition.Name;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.fontSize = 18;

            return card;
        }

        private void OnCardChosen(CardType cardType)
        {
            _completion.TrySetResult(cardType);
        }
    }
}
