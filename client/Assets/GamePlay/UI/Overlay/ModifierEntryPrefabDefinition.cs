#if UNITY_EDITOR
using Tools.PrefabBuilder;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.UI
{
    [PrefabDefinition]
    public static class ModifierEntryPrefab
    {
        public static void Define(PrefabBuilder builder)
        {
            builder
                .WithName("Game/ModifierEntry")
                .WithComponent<RectTransform>(rt => {
                    rt.sizeDelta = new Vector2(64f, 64f);
                })
                .WithComponent<Image>(img => {
                    img.color = Color.white;
                })
                .WithComponent<PlayerModifierEntryView>();

            builder.WithChildObject("Icon", child => {
                child.WithComponent<RectTransform>(rt => {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                });
                child.WithComponent<Image>();
            });

            builder.WithChildObject("Turns", child => {
                child.WithComponent<RectTransform>(rt => {
                    rt.anchorMin = new Vector2(1f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(1f, 0f);
                    rt.anchoredPosition = new Vector2(-2f, 2f);
                    rt.sizeDelta = new Vector2(24f, 24f);
                });
                child.WithComponent<Text>(text => {
                    text.alignment = TextAnchor.MiddleCenter;
                    text.fontSize = 14;
                    text.color = Color.white;
                });
            });
        }
    }
}
#endif
