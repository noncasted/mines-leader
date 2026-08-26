#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GamePlay.UI;

namespace GamePlay.UI.Editor
{
    public static class SetupPlayerModifiersOverlay
    {
        [MenuItem("Tools/Setup/PlayerModifiers Overlay")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Game_Overlay")
            {
                Debug.LogError("Active scene must be Game_Overlay. Current: " + scene.name);
                return;
            }

            var uiRoot = GameObject.Find("UI");
            if (uiRoot == null)
            {
                Debug.LogError("UI Canvas not found in scene");
                return;
            }

            // Create or find PlayerModifiersView
            var viewGo = uiRoot.transform.Find("PlayerModifiersView")?.gameObject;
            if (viewGo != null)
            {
                Undo.DestroyObjectImmediate(viewGo);
            }

            viewGo = new GameObject("PlayerModifiersView");
            Undo.RegisterCreatedObjectUndo(viewGo, "Create PlayerModifiersView");
            viewGo.transform.SetParent(uiRoot.transform, false);

            var rect = viewGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(20, -20);
            rect.sizeDelta = new Vector2(80, 400);

            // Add ResponsiveContainer (vertical)
            var container = viewGo.AddComponent<Exoa.Responsive.ResponsiveContainer>();
            var containerType = container.GetType();
            var verticalField = containerType.GetField("isVertical", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (verticalField != null) verticalField.SetValue(container, true);
            var spacingField = containerType.GetField("spacing", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (spacingField != null) spacingField.SetValue(container, 8f);

            // Add PlayerModifiersView
            var view = viewGo.AddComponent<PlayerModifiersView>();

            // Create entry prefab
            var entryGo = new GameObject("ModifierEntryPrefab");
            Undo.RegisterCreatedObjectUndo(entryGo, "Create ModifierEntryPrefab");
            entryGo.transform.SetParent(viewGo.transform, false);
            entryGo.SetActive(false);

            var entryRect = entryGo.AddComponent<RectTransform>();
            entryRect.sizeDelta = new Vector2(64f, 64f);

            var bgImg = entryGo.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            // Icon
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(entryGo.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(4, 4);
            iconRect.offsetMax = new Vector2(-4, -4);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = Color.white;

            // Turns text
            var turnsGo = new GameObject("Turns");
            turnsGo.transform.SetParent(entryGo.transform, false);
            var turnsRect = turnsGo.AddComponent<RectTransform>();
            turnsRect.anchorMin = new Vector2(1, 0);
            turnsRect.anchorMax = new Vector2(1, 0);
            turnsRect.pivot = new Vector2(1, 0);
            turnsRect.anchoredPosition = new Vector2(-2, 2);
            turnsRect.sizeDelta = new Vector2(24, 24);
            var turnsText = turnsGo.AddComponent<Text>();
            turnsText.alignment = TextAnchor.MiddleCenter;
            turnsText.fontSize = 14;
            turnsText.color = Color.white;

            var entryView = entryGo.AddComponent<PlayerModifierEntryView>();
            entryView.GetType().GetField("_iconImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(entryView, iconImg);
            entryView.GetType().GetField("_turnsText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(entryView, turnsText);
            var pointerHandler = entryGo.AddComponent<UIElementPointerHandler>();
            entryView.GetType().GetField("_pointerHandler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(entryView, pointerHandler);

            // Create tooltip prefab
            var tooltipGo = new GameObject("TooltipPrefab");
            Undo.RegisterCreatedObjectUndo(tooltipGo, "Create TooltipPrefab");
            tooltipGo.transform.SetParent(viewGo.transform, false);
            tooltipGo.SetActive(false);

            var tooltipRect = tooltipGo.AddComponent<RectTransform>();
            tooltipRect.sizeDelta = new Vector2(200f, 80f);

            var tooltipBg = tooltipGo.AddComponent<Image>();
            tooltipBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            var textGo = new GameObject("Description");
            textGo.transform.SetParent(tooltipGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 8);
            textRect.offsetMax = new Vector2(-8, -8);
            var descText = textGo.AddComponent<Text>();
            descText.alignment = TextAnchor.UpperLeft;
            descText.fontSize = 14;
            descText.color = Color.white;

            var tooltipView = tooltipGo.AddComponent<PlayerModifierTooltipView>();
            tooltipView.GetType().GetField("_descriptionText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(tooltipView, descText);
            tooltipView.GetType().GetField("_rectTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(tooltipView, tooltipRect);

            view.GetType().GetField("_container", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(view, viewGo.transform);
            view.GetType().GetField("_entryPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(view, entryView);
            view.GetType().GetField("_tooltipPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(view, tooltipView);
            view.GetType().GetField("_tooltipOffset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(view, new Vector2(70f, 0f));

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("PlayerModifiersView setup complete!");
        }
    }
}
#endif
