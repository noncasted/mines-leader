#if UNITY_EDITOR
using Exoa.Responsive;
using MPUIKIT;
using TMPro;
using Tools.PrefabBuilder;
using UnityEngine;

namespace GamePlay.UI.ActionLog
{
    [PrefabDefinition]
    public static class GameActionLogTilePrefab
    {
        private const string FontPath = "Assets/Common/Artwork/Ithaca-LVB75.asset";

        public static void Define(PrefabBuilder builder)
        {
            CanvasGroup canvasGroup = null;
            MPImage background = null;
            TMP_Text messageText = null;

            builder
                .WithName("Game/ActionLogTile")
                .WithRectTransform(rt => {
                    rt.sizeDelta = new Vector2(280f, 50f);
                })
                .WithComponent<CanvasGroup>(cg => canvasGroup = cg)
                .WithComponent<GameActionLogTileUI>();

            builder.WithChildObject("Background", bg => {
                bg.WithRoundedRect(new Color(0.18f, 0.35f, 0.58f, 0.9f), 6f);
                bg.WithRectTransform(rt => rt.StretchFull());

                background = bg.GameObject.GetComponent<MPImage>();
                background.raycastTarget = true;
            });

            builder.WithChildObject("Message", msg => {
                msg.WithComponent<CanvasRenderer>()
                   .WithComponent<TextMeshProUGUI>(tmp => {
                       tmp.font = AssetsBuilderExtensions.LoadAsset<TMP_FontAsset>(FontPath);
                       tmp.text = "Card Action";
                       tmp.color = new Color(0.95f, 0.95f, 0.95f, 1f);
                       tmp.fontSize = 24f;
                       tmp.enableAutoSizing = false;
                       tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
                       tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
                       tmp.textWrappingMode = TextWrappingModes.NoWrap;
                       tmp.overflowMode = TextOverflowModes.Ellipsis;
                       tmp.raycastTarget = false;
                       messageText = tmp;
                   })
                   .WithRectTransform(rt => {
                       rt.StretchFull();
                       rt.WithPadding(10f, 4f);
                   });
            });

            builder.SetSerialized<GameActionLogTileUI>("_canvasGroup", canvasGroup);
            builder.SetSerialized<GameActionLogTileUI>("_background", background);
            builder.SetSerialized<GameActionLogTileUI>("_messageText", messageText);
        }
    }

    [PrefabDefinition]
    public static class GameActionLogPanelPrefab
    {
        private const string FontPath = "Assets/Common/Artwork/Ithaca-LVB75.asset";

        public static void Define(PrefabBuilder builder)
        {
            RectTransform containerRt = null;
            CanvasGroup tooltipGroup = null;
            TMP_Text tooltipCardName = null;
            TMP_Text tooltipCardDescription = null;
            ResponsiveContainer responsiveContainer = null;

            builder
                .WithName("Game/ActionLogPanel")
                .WithRectTransform(rt => {
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 0f);
                    rt.pivot = new Vector2(0f, 0f);
                    rt.anchoredPosition = new Vector2(12f, 12f);
                    rt.sizeDelta = new Vector2(360, 360);
                })
                .WithComponent<GameActionLogUI>();

            // Tile container with vertical layout
            builder.WithChildObject("TileContainer", container => {
                container.WithRectTransform(rt => {
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(0f, 320f);
                    rt.offsetMin = new Vector2(0f, 0f);
                    rt.offsetMax = new Vector2(0f, 320f);
                    containerRt = rt;
                });

                container.WithResponsiveContainer(rc => {
                    rc.AsVertical()
                      .WithVerticalGroupAndExpand()
                      .WithHorizontalFitInContainer()
                      .WithVerticalAlign(ResponsiveContainer.VAlignType.Bottom)
                      .WithSpacing(4f)
                      .WithRefreshOnChange();
                }, out responsiveContainer);
            });

            // Tooltip panel
            builder.WithChildObject("Tooltip", false, tooltip => {
                tooltip.WithRectTransform(rt => {
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 0f);
                    rt.pivot = new Vector2(0f, 0f);
                    rt.sizeDelta = new Vector2(400f, 200f);
                    rt.anchoredPosition = new Vector2(450f, 18f);
                });

                tooltip.WithComponent<CanvasGroup>(cg => {
                    cg.alpha = 0f;
                    tooltipGroup = cg;
                });

                tooltip.WithChildObject("Background", bg => {
                    bg.WithRoundedRect(new Color(0.12f, 0.12f, 0.16f, 0.95f), 8f);
                    bg.WithRectTransform(rt => rt.StretchFull());
                    bg.GameObject.GetComponent<MPImage>().raycastTarget = false;
                    bg.ExcludeFromLayout();
                });

                tooltip.WithResponsiveContainer(rc => {
                    rc.AsVertical()
                      .WithVerticalFitToContent()
                      .WithHorizontalFitInContainer()
                      .WithSpacing(1f)
                      .WithMargins(top: 20, bottom: 20, left: 20, right: 20);
                });

                tooltip.WithChildObject("Action name", cardName => {
                    cardName.WithComponent<CanvasRenderer>()
                            .WithComponent<TextMeshProUGUI>(tmp => {
                                tmp.font = AssetsBuilderExtensions.LoadAsset<TMP_FontAsset>(FontPath);
                                tmp.text = "action name";
                                tmp.color = new Color(0.93f, 0.81f, 0.68f, 1f);
                                tmp.fontSize = 32f;
                                tmp.fontSizeMin = 18f;
                                tmp.fontSizeMax = 32f;
                                tmp.fontStyle = FontStyles.Bold;
                                tmp.enableAutoSizing = true;
                                tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
                                tmp.verticalAlignment = VerticalAlignmentOptions.Top;
                                tmp.raycastTarget = false;
                                tooltipCardName = tmp;
                            })
                            .WithRectTransform(rt => {
                                rt.sizeDelta = new Vector2(380f, 40f);
                            });
                });

                tooltip.WithChildObject("CardDescription", cardDesc => {
                    cardDesc.WithComponent<CanvasRenderer>()
                            .WithComponent<TextMeshProUGUI>(tmp => {
                                tmp.font = AssetsBuilderExtensions.LoadAsset<TMP_FontAsset>(FontPath);
                                tmp.text = "Card description goes here.";
                                tmp.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                                tmp.fontSize = 32f;
                                tmp.fontSizeMin = 18f;
                                tmp.fontSizeMax = 32f;
                                tmp.enableAutoSizing = true;
                                tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
                                tmp.verticalAlignment = VerticalAlignmentOptions.Top;
                                tmp.textWrappingMode = TextWrappingModes.Normal;
                                tmp.raycastTarget = false;
                                tooltipCardDescription = tmp;
                            })
                            .WithRectTransform(rt => {
                                rt.sizeDelta = new Vector2(380f, 140f);
                            });
                });
            });

            builder.SetSerialized<GameActionLogUI>("_container", containerRt);
            builder.SetSerialized<GameActionLogUI>("_tooltipGroup", tooltipGroup);
            builder.SetSerialized<GameActionLogUI>("_tooltipCardName", tooltipCardName);
            builder.SetSerialized<GameActionLogUI>("_tooltipCardDescription", tooltipCardDescription);
            builder.SetSerialized<GameActionLogUI>("_responsiveContainer", responsiveContainer);

            builder.RecalculateResponsiveContainers();
        }
    }
}
#endif