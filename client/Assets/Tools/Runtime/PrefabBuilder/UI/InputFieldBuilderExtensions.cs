using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tools.PrefabBuilder
{
    /// <summary>
    /// PrefabBuilder extensions for creating input fields.
    /// TMP_InputField cannot be fully set up in PrefabBuilder because cross-references
    /// (textViewport, textComponent) are lost during prefab serialization.
    /// These extensions create the visual structure only.
    /// </summary>
    public static class InputFieldBuilderExtensions
    {
        private static readonly Color InputBgColor = new(0f, 0f, 0f, 0.12f);
        private static readonly Color TextColor = new(0.2f, 0.2f, 0.2f, 1f);

        /// <summary>
        /// Creates an input field visual structure: background + text area + text component.
        /// Returns RectTransform (for runtime TMP_InputField) and TMP_Text (for display).
        /// </summary>
        public static PrefabBuilder WithNodeInputField(
            this PrefabBuilder builder,
            string name,
            out RectTransform inputRect,
            out TextMeshProUGUI inputText,
            float height = 28f)
        {
            return builder.WithNodeInputField(builder.GameObject.transform, name, out inputRect, out inputText, height);
        }

        public static PrefabBuilder WithNodeInputField(
            this PrefabBuilder builder,
            Transform parent,
            string name,
            out RectTransform inputRect,
            out TextMeshProUGUI inputText,
            float height = 28f)
        {
            RectTransform capturedRect = null;
            TextMeshProUGUI capturedText = null;

            builder.WithChildObject(name, parent, inputParent => {
                        RectTransformBuilderExtensions.WithRectTransform(inputParent, rt => {
                                                                  rt.anchorMin = new Vector2(0, 1);
                                                                  rt.anchorMax = new Vector2(1, 1);
                                                                  RectTransformBuilderExtensions.WithPivot(rt, 0.5f, 1);
                                                                  rt.sizeDelta = new Vector2(-8, height);
                                                                  rt.anchoredPosition = new Vector2(0, -4);
                                                              }
                                                          )
                                                      .WithComponent<CanvasRenderer>();
                        capturedRect = inputParent.GameObject.GetComponent<RectTransform>();

                        inputParent.WithChildObject("Text Area", textArea => {
                                    textArea.WithRectTransform(rt => {
                                                        rt.StretchFull();
                                                        rt.offsetMin = new Vector2(6, 0);
                                                        rt.offsetMax = new Vector2(-6, 0);
                                                    }
                                                )
                                            .WithComponent<RectMask2D>();

                                    textArea.WithChildObject("Text", text => {
                                                text.WithRectTransform(rt => rt.StretchFull())
                                                    .WithComponent<CanvasRenderer>()
                                                    .WithComponent<TextMeshProUGUI>(tmp => {
                                                                tmp.text = "";
                                                                tmp.color = TextColor;
                                                                tmp.fontSize = 14f;
                                                                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                                                                tmp.overflowMode = TextOverflowModes.Overflow;
                                                                tmp.raycastTarget = true;
                                                                capturedText = tmp;
                                                            }
                                                        );
                                            }
                                        );
                                }
                            );
                    }
                );

            inputRect = capturedRect;
            inputText = capturedText;
            return builder;
        }
    }
}