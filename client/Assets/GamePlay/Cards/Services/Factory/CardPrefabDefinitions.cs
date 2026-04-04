#if UNITY_EDITOR
using TMPro;
using Tools;
using UnityEngine;
using UnityEngine.Rendering;

namespace GamePlay.Cards.Options.Prefabs
{
    [PrefabDefinition]
    public static class CardLocalPrefab
    {
        private const string FontIthaca = "Assets/Common/Artwork/Ithaca-LVB75.asset";
        private const string FontBitach = "Assets/Common/Artwork/BITACH SDF.asset";
        private const string CardSprite = "Assets/GamePlay/Cards/Artwork/Alliance/card.psd";
        private const string OutlineSprite = "Assets/GamePlay/Cards/Artwork/Alliance/outline.psd";

        public static void Define(PrefabBuilder builder)
        {
            SortingGroup sortingGroup = null;
            CardScope cardScope = null;
            CardRenderer cardRenderer = null;

            builder
                .WithName("Card_Local")
                .WithComponent<CardScope>(c => cardScope = c)
                .WithComponent<CardScopeEntity>()
                .WithComponent<SortingGroup>(sg =>
                {
                    sg.sortingLayerName = "Cards";
                    sg.sortingOrder = 0;
                    sortingGroup = sg;
                })
                .WithComponent<CardRenderer>(cr => cardRenderer = cr)
                .WithComponent<CardView>();

            builder.SetSerialized<CardRenderer>("_sortingGroup", sortingGroup);
            builder.SetSerialized<CardView>("_scope", cardScope);

            TMP_Text nameText = null;
            TMP_Text descriptionText = null;
            TMP_Text manaCostText = null;
            SpriteRenderer imageRenderer = null;

            builder.WithChildObject("View", view =>
            {
                view.WithComponent<CardTransform>();
                view.WithComponent<CardAvailabilityView>();

                view.SetSerialized<CardAvailabilityView>("_renderer", cardRenderer);
                view.SetSerialized<CardAvailabilityView>("_availableSpriteColor",
                    new Color(1f, 1f, 1f, 1f));
                view.SetSerialized<CardAvailabilityView>("_lockedSpriteColor",
                    new Color(0.594f, 0.594f, 0.594f, 1f));
                view.SetSerialized<CardAvailabilityView>("_availableNameColor",
                    new Color(0.929f, 0.808f, 0.678f, 1f));
                view.SetSerialized<CardAvailabilityView>("_availableDescriptionColor",
                    new Color(0.518f, 0.263f, 0.169f, 1f));
                view.SetSerialized<CardAvailabilityView>("_lockedNameColor",
                    new Color(0.435f, 0.341f, 0.275f, 1f));
                view.SetSerialized<CardAvailabilityView>("_lockedDescriptionColor",
                    new Color(0.227f, 0.090f, 0.051f, 1f));

                view.WithChildObject("Body", body =>
                {
                    body.WithComponent<SpriteRenderer>(sr =>
                    {
                        sr.sprite = PrefabBuilder.LoadAsset<Sprite>(CardSprite);
                        sr.color = Color.white;
                        sr.sortingLayerName = "UI";
                        sr.sortingOrder = 0;
                    });
                    body.WithComponent<CardDataView>();

                    body.WithChildObject("Image", image =>
                    {
                        image
                            .WithPosition(0f, 0.958f, 0f)
                            .WithScale(8.3333f, 8.3333f, 1f);

                        image.WithComponent<SpriteRenderer>(sr =>
                        {
                            sr.sprite = PrefabBuilder.LoadAsset<Sprite>(
                                "Assets/Resources/Cards/Trebuchet.psd");
                            sr.color = Color.white;
                            sr.sortingLayerName = "UI";
                            sr.sortingOrder = -1;
                            imageRenderer = sr;
                        });
                    });

                    body.WithChildObject("SelectionHighlight", selection =>
                    {
                        selection.WithComponent<SpriteRenderer>(sr =>
                        {
                            sr.sprite = PrefabBuilder.LoadAsset<Sprite>(OutlineSprite);
                            sr.color = Color.white;
                            sr.sortingLayerName = "UI";
                            sr.sortingOrder = -2;
                        });
                        selection.WithComponent<CardSelectionSwitcher>();
                        selection.SetSerialized<CardSelectionSwitcher>(
                            "_selectionHighlight", selection.GameObject);
                    });

                    ConfigureTextChild(body, "Name", -0.0006f, -0.2495f,
                        2.6702f, 0.5008f, FontIthaca,
                        new Color(0.929f, 0.808f, 0.678f, 1f),
                        6.1f, 3f, 72f, 0f,
                        HorizontalAlignmentOptions.Center,
                        VerticalAlignmentOptions.Middle,
                        tmp => nameText = tmp);

                    ConfigureTextChild(body, "Description", -0.0006f, -1.2063f,
                        2.6702f, 1.2534f, FontIthaca,
                        new Color(0.518f, 0.263f, 0.169f, 1f),
                        3.6f, 0f, 5f, -8f,
                        HorizontalAlignmentOptions.Center,
                        VerticalAlignmentOptions.Top,
                        tmp => descriptionText = tmp);

                    ConfigureTextChild(body, "ManaCost", 1.0804f, 1.5215f,
                        0.6249f, 0.6249f, FontBitach,
                        new Color(0.294f, 0.635f, 0.914f, 1f),
                        5.95f, 3f, 72f, 0f,
                        HorizontalAlignmentOptions.Center,
                        VerticalAlignmentOptions.Middle,
                        tmp => manaCostText = tmp);

                    body.SetSerialized<CardDataView>("_name", nameText);
                    body.SetSerialized<CardDataView>("_description", descriptionText);
                    body.SetSerialized<CardDataView>("_manaCost", manaCostText);
                    body.SetSerialized<CardDataView>("_image", imageRenderer);
                });

                view.WithChildObject("PointerHandler", pointer =>
                {
                    pointer.WithComponent<BoxCollider2D>(bc =>
                    {
                        bc.size = new Vector2(3f, 4f);
                    });
                    pointer.WithComponent<CardPointerHandler>();
                });
            });
        }

        private static void ConfigureTextChild(
            PrefabBuilder parent,
            string name,
            float posX,
            float posY,
            float width,
            float height,
            string fontPath,
            Color color,
            float fontSize,
            float fontSizeMin,
            float fontSizeMax,
            float lineSpacingAdjustment,
            HorizontalAlignmentOptions hAlign,
            VerticalAlignmentOptions vAlign,
            System.Action<TMP_Text> capture)
        {
            parent.WithChildObject(name, text =>
            {
                text.WithComponent<TextMeshPro>(tmp =>
                {
                    tmp.font = PrefabBuilder.LoadAsset<TMP_FontAsset>(fontPath);
                    tmp.color = color;
                    tmp.fontSize = fontSize;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = fontSizeMin;
                    tmp.fontSizeMax = fontSizeMax;
                    tmp.horizontalAlignment = hAlign;
                    tmp.verticalAlignment = vAlign;
                    tmp.textWrappingMode = TextWrappingModes.Normal;
                    tmp.lineSpacingAdjustment = lineSpacingAdjustment;
                    tmp.sortingLayerID = SortingLayer.NameToID("UI");
                    tmp.sortingOrder = 1;
                    capture(tmp);
                });

                var rt = text.GameObject.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(posX, posY);
                    rt.sizeDelta = new Vector2(width, height);
                }

                var meshRenderer = text.GameObject.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.sortingLayerName = "UI";
                    meshRenderer.sortingOrder = 1;
                }
            });
        }
    }

    [PrefabDefinition]
    public static class CardRemotePrefab
    {
        public static void Define(PrefabBuilder builder)
        {
            SortingGroup sortingGroup = null;
            CardScope cardScope = null;

            builder
                .WithName("Card_Remote")
                .WithComponent<CardScope>(c => cardScope = c)
                .WithComponent<CardScopeEntity>()
                .WithComponent<SortingGroup>(sg =>
                {
                    sg.sortingLayerName = "Cards";
                    sg.sortingOrder = 0;
                    sortingGroup = sg;
                })
                .WithComponent<CardRenderer>()
                .WithComponent<CardView>();

            builder.SetSerialized<CardRenderer>("_sortingGroup", sortingGroup);
            builder.SetSerialized<CardView>("_scope", cardScope);

            builder.WithChildObject("View", view =>
            {
                view.WithComponent<CardTransform>();

                view.WithChildObject("Body", body =>
                {
                    body.WithComponent<SpriteRenderer>(sr =>
                    {
                        sr.sprite = PrefabBuilder.LoadAsset<Sprite>("Assets/GamePlay/Boards/Artwork/Alliance/discard_cards.psd");
                        sr.color = new Color(0.751f, 0.751f, 0.751f, 1f);
                        sr.flipY = true;
                    });
                });
            });
        }
    }
}
#endif
