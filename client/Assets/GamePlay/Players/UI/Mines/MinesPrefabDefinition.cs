#if UNITY_EDITOR
using TMPro;
using Tools;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.Players
{
    [PrefabDefinition]
    public static class MinesPrefab
    {
        private const string PlateSprite = "Assets/GamePlay/Boards/Artwork/Alliance/counter.psd";
        private const string FontBitach = "Assets/Common/Artwork/BITACH SDF.asset";

        public static void Define(PrefabBuilder builder)
        {
            TMP_Text countText = null;

            builder
                .WithName("Mines")
                .WithRectTransform(rt =>
                    {
                        rt.anchoredPosition = new Vector2(16.63f, 9.83f);
                        rt.sizeDelta = new Vector2(100f, 100f);
                        rt.anchorMin = new Vector2(0.5f, 0.5f);
                        rt.anchorMax = new Vector2(0.5f, 0.5f);
                        rt.pivot = new Vector2(0.5f, 0.5f);
                    }
                )
                .WithComponent<BoardMinesCounterView>();

            builder.WithChildObject("Plate", plate =>
                {
                    plate.WithComponent<Image>(img =>
                        {
                            img.sprite = PrefabBuilder.LoadAsset<Sprite>(PlateSprite);
                            img.color = Color.white;
                            img.raycastTarget = true;
                        }
                    );

                    var plateRt = plate.GameObject.GetComponent<RectTransform>();
                    plateRt.anchoredPosition = Vector2.zero;
                    plateRt.sizeDelta = new Vector2(3.08f, 1.33f);
                    plateRt.anchorMin = new Vector2(0.5f, 0.5f);
                    plateRt.anchorMax = new Vector2(0.5f, 0.5f);
                    plateRt.pivot = new Vector2(0.5f, 0.5f);
                }
            );

            builder.WithChildObject("Count", count =>
                {
                    count.WithComponent<TextMeshProUGUI>(tmp =>
                        {
                            tmp.font = PrefabBuilder.LoadAsset<TMP_FontAsset>(FontBitach);
                            tmp.text = "1";
                            tmp.color = new Color(1f, 0.989f, 0.406f, 1f);
                            tmp.fontSize = 0.95f;
                            tmp.enableAutoSizing = true;
                            tmp.fontSizeMin = 0f;
                            tmp.fontSizeMax = 72f;
                            tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
                            tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
                            tmp.textWrappingMode = TextWrappingModes.Normal;
                            tmp.raycastTarget = true;
                            countText = tmp;
                        }
                    );

                    var countRt = count.GameObject.GetComponent<RectTransform>();
                    countRt.anchoredPosition = new Vector2(0.67f, -0.09f);
                    countRt.sizeDelta = new Vector2(1.2f, 1f);
                    countRt.anchorMin = new Vector2(0.5f, 0.5f);
                    countRt.anchorMax = new Vector2(0.5f, 0.5f);
                    countRt.pivot = new Vector2(0.5f, 0.5f);
                }
            );

            builder.SetSerialized<BoardMinesCounterView>("_text", countText);
        }
    }
}
#endif