#if UNITY_EDITOR
using System;
using Global.UI;
using MPUIKIT;
using TMPro;
using Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Global.Settings.Prefabs
{
    // ResponsiveContainer is in a predefined assembly (no asmdef) — access via reflection
    internal static class ResponsiveContainerHelper
    {
        private static Type _type;

        public static Component Add(GameObject go)
        {
            // Ensure RectTransform exists (ResponsiveContainer requires it)
            if (go.GetComponent<RectTransform>() == null)
                go.AddComponent<RectTransform>();

            _type ??= Type.GetType("Exoa.Responsive.ResponsiveContainer, Assembly-CSharp-firstpass");
            return _type != null ? go.AddComponent(_type) : null;
        }
    }

    [PrefabDefinition]
    public static class SettingsSliderPrefab
    {
        private const string FontDreiFraktur = "Assets/Common/Artwork/DreiFraktur SDF.asset";

        public static void Define(PrefabBuilder builder)
        {
            builder.WithName("SettingsSlider");

            var rootRt = builder.GameObject.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = new Vector2(0f, 59.5f);
            rootRt.sizeDelta = new Vector2(180f, 10f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);

            builder.WithChildObject("Header", header =>
                {
                    header.WithComponent<TextMeshProUGUI>(tmp =>
                        {
                            tmp.font = PrefabBuilder.LoadAsset<TMP_FontAsset>(FontDreiFraktur);
                            tmp.text = "Music";
                            tmp.color = Color.white;
                            tmp.fontSize = 8f;
                            tmp.enableAutoSizing = false;
                            tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
                            tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
                            tmp.textWrappingMode = TextWrappingModes.Normal;
                            tmp.raycastTarget = true;
                        }
                    );

                    var headerRt = header.GameObject.GetComponent<RectTransform>();
                    headerRt.anchorMin = new Vector2(0f, 0f);
                    headerRt.anchorMax = new Vector2(0f, 1f);
                    headerRt.anchoredPosition = Vector2.zero;
                    headerRt.sizeDelta = new Vector2(40f, 0f);
                    headerRt.pivot = new Vector2(0f, 0.5f);
                }
            );

            RectTransform fillRt = null;
            RectTransform handleRt = null;
            Image handleImage = null;

            builder.WithChildObject("Slider", slider =>
                {
                    slider.WithChildObject("Background", bg =>
                        {
                            bg.WithComponent<MPImage>(img =>
                                {
                                    img.color = new Color(0.075f, 0f, 0.106f, 1f);
                                    img.raycastTarget = true;
                                }
                            );

                            var bgRt = bg.GameObject.GetComponent<RectTransform>();
                            bgRt.anchorMin = Vector2.zero;
                            bgRt.anchorMax = Vector2.one;
                            bgRt.anchoredPosition = Vector2.zero;
                            bgRt.sizeDelta = Vector2.zero;
                            bgRt.pivot = new Vector2(0.5f, 0.5f);
                        }
                    );

                    slider.WithChildObject("Fill Area", fillArea =>
                        {
                            fillArea.WithComponent<RectMask2D>();

                            var fillAreaRt = fillArea.GameObject.GetComponent<RectTransform>();
                            fillAreaRt.anchorMin = Vector2.zero;
                            fillAreaRt.anchorMax = Vector2.one;
                            fillAreaRt.anchoredPosition = Vector2.zero;
                            fillAreaRt.sizeDelta = Vector2.zero;
                            fillAreaRt.pivot = new Vector2(0.5f, 0.5f);

                            fillArea.WithChildObject("Fill", fill =>
                                {
                                    fill.WithComponent<MPImage>(img =>
                                        {
                                            img.color = new Color(0.388f, 0.392f, 0.498f, 1f);
                                            img.raycastTarget = true;
                                        }
                                    );

                                    fillRt = fill.GameObject.GetComponent<RectTransform>();
                                    fillRt.anchorMin = Vector2.zero;
                                    fillRt.anchorMax = Vector2.zero;
                                    fillRt.anchoredPosition = Vector2.zero;
                                    fillRt.sizeDelta = Vector2.zero;
                                    fillRt.pivot = new Vector2(0.5f, 0.5f);
                                }
                            );
                        }
                    );

                    slider.WithChildObject("Handle Slide Area", handleArea =>
                        {
                            var handleAreaRt = handleArea.GameObject.GetComponent<RectTransform>();
                            if (handleAreaRt == null)
                                handleAreaRt = handleArea.GameObject.AddComponent<RectTransform>();
                            handleAreaRt.anchorMin = Vector2.zero;
                            handleAreaRt.anchorMax = Vector2.one;
                            handleAreaRt.anchoredPosition = Vector2.zero;
                            handleAreaRt.sizeDelta = new Vector2(-20f, 0f);
                            handleAreaRt.pivot = new Vector2(0.5f, 0.5f);

                            handleArea.WithChildObject("Handle", handle =>
                                {
                                    handle.WithComponent<Image>(img =>
                                        {
                                            img.color = new Color(1f, 1f, 1f, 0f);
                                            img.raycastTarget = true;
                                            handleImage = img;
                                        }
                                    );

                                    handleRt = handle.GameObject.GetComponent<RectTransform>();
                                    handleRt.anchorMin = Vector2.zero;
                                    handleRt.anchorMax = Vector2.zero;
                                    handleRt.anchoredPosition = Vector2.zero;
                                    handleRt.sizeDelta = new Vector2(20f, 0f);
                                    handleRt.pivot = new Vector2(0.5f, 0.5f);
                                }
                            );
                        }
                    );

                    slider.WithComponent<Slider>(s =>
                        {
                            s.fillRect = fillRt;
                            s.handleRect = handleRt;
                            s.targetGraphic = handleImage;
                            s.direction = Slider.Direction.LeftToRight;
                            s.minValue = 0f;
                            s.maxValue = 1f;
                            s.wholeNumbers = false;
                            s.value = 0f;
                            s.interactable = true;
                        }
                    );

                    var sliderRt = slider.GameObject.GetComponent<RectTransform>();
                    sliderRt.anchorMin = Vector2.zero;
                    sliderRt.anchorMax = Vector2.one;
                    sliderRt.anchoredPosition = new Vector2(20f, 0f);
                    sliderRt.sizeDelta = new Vector2(-40f, -2f);
                    sliderRt.pivot = new Vector2(0.5f, 0.5f);
                }
            );
        }
    }

    [PrefabDefinition]
    public static class SettingsPrefab
    {
        private const string FontDreiFraktur = "Assets/Common/Artwork/DreiFraktur SDF.asset";
        private const string PlateSprite = "Assets/Menu/Artowrk/chat_background.psd";
        private const string ButtonSprite = "Assets/Menu/Artowrk/button_background.psd";
        private const string TextConfigAsset = "Assets/Menu/Common/Options/Visual/Menu_Text_Primary.asset";
        private const string SliderPrefabPath = "Assets/Resources/Generated/SettingsSlider.prefab";

        public static void Define(PrefabBuilder builder)
        {
            DesignButton applyDesignButton = null;
            DesignButton cancelDesignButton = null;
            Slider masterSlider = null;
            Slider musicSlider = null;
            Slider sfxSlider = null;
            Slider shakeSlider = null;
            DesignGroupSelection vsyncSelection = null;

            builder
                .WithName("Settings")
                .WithComponent<Canvas>(c =>
                    {
                        c.renderMode = RenderMode.ScreenSpaceOverlay;
                        c.sortingOrder = 0;
                    }
                )
                .WithComponent<CanvasScaler>(cs =>
                    {
                        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        cs.referenceResolution = new Vector2(512f, 288f);
                        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                        cs.matchWidthOrHeight = 1f;
                    }
                )
                .WithComponent<GraphicRaycaster>()
                .WithComponent<SettingsView>();

            var rootRt = builder.GameObject.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.zero;
            rootRt.anchoredPosition = Vector2.zero;
            rootRt.sizeDelta = Vector2.zero;
            rootRt.pivot = Vector2.zero;

            builder.WithChildObject("Plate", plate =>
                {
                    plate.WithComponent<Image>(img =>
                        {
                            img.sprite = PrefabBuilder.LoadAsset<Sprite>(PlateSprite);
                            img.type = Image.Type.Sliced;
                            img.color = Color.white;
                            img.raycastTarget = true;
                        }
                    );

                    var plateRt = plate.GameObject.GetComponent<RectTransform>();
                    plateRt.anchorMin = new Vector2(0.5f, 0.5f);
                    plateRt.anchorMax = new Vector2(0.5f, 0.5f);
                    plateRt.anchoredPosition = Vector2.zero;
                    plateRt.sizeDelta = new Vector2(200f, 200f);
                    plateRt.pivot = new Vector2(0.5f, 0.5f);

                    // Header
                    plate.WithChildObject("Header", header =>
                        {
                            header.WithComponent<TextMeshProUGUI>(tmp =>
                                {
                                    tmp.font = PrefabBuilder.LoadAsset<TMP_FontAsset>(FontDreiFraktur);
                                    tmp.text = "Settings";
                                    tmp.color = new Color(0.388f, 0.392f, 0.498f, 1f);
                                    tmp.fontSize = 10f;
                                    tmp.enableAutoSizing = true;
                                    tmp.fontSizeMin = 1f;
                                    tmp.fontSizeMax = 10f;
                                    tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
                                    tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
                                    tmp.textWrappingMode = TextWrappingModes.Normal;
                                }
                            );

                            var headerRt = header.GameObject.GetComponent<RectTransform>();
                            headerRt.anchorMin = new Vector2(0f, 1f);
                            headerRt.anchorMax = new Vector2(1f, 1f);
                            headerRt.anchoredPosition = new Vector2(0f, -4.6f);
                            headerRt.sizeDelta = new Vector2(0f, 20.9f);
                            headerRt.pivot = new Vector2(0.5f, 1f);
                        }
                    );

                    // Volumes container with 3 sliders (Master, Music, SFX)
                    plate.WithChildObject("Volumes", volumes =>
                        {
                            var rc = ResponsiveContainerHelper.Add(volumes.GameObject);
                            if (rc != null)
                            {
                                var so = new SerializedObject(rc);
                                so.FindProperty("axis").enumValueIndex = 1; // Vertical
                                so.FindProperty("hBehaviour").enumValueIndex = 1; // FitContentInContainer
                                so.FindProperty("vBehaviour").enumValueIndex = 6; // GroupContentInContainerAndExpand
                                so.FindProperty("spacing").floatValue = 5f;
                                so.ApplyModifiedPropertiesWithoutUndo();
                            }

                            var volumesRt = volumes.GameObject.GetComponent<RectTransform>();
                            volumesRt.anchorMin = new Vector2(0f, 0.5f);
                            volumesRt.anchorMax = new Vector2(1f, 0.5f);
                            volumesRt.anchoredPosition = new Vector2(0f, 42.2f);
                            volumesRt.sizeDelta = new Vector2(-30f, 40f);
                            volumesRt.pivot = new Vector2(0.5f, 0.5f);

                            var masterGo = volumes.WithPrefabChild(SliderPrefabPath, "Master");
                            if (masterGo != null)
                            {
                                masterSlider = masterGo.GetComponentInChildren<Slider>();
                            }

                            var musicGo = volumes.WithPrefabChild(SliderPrefabPath, "Music");
                            if (musicGo != null)
                            {
                                musicSlider = musicGo.GetComponentInChildren<Slider>();
                            }

                            var sfxGo = volumes.WithPrefabChild(SliderPrefabPath, "SFX");
                            if (sfxGo != null)
                            {
                                sfxSlider = sfxGo.GetComponentInChildren<Slider>();
                            }
                        }
                    );

                    // Shake slider (direct child of Plate, not in Volumes)
                    var shakeGo = plate.WithPrefabChild(SliderPrefabPath, "Shake");
                    if (shakeGo != null)
                    {
                        shakeSlider = shakeGo.GetComponentInChildren<Slider>();
                    }

                    // Vsync group
                    plate.WithChildObject("Vsync", vsync =>
                        {
                            vsync.WithRectTransform();
                            vsync.WithComponent<DesignGroupSelection>(dgs => vsyncSelection = dgs);

                            var vsyncRt = vsync.GameObject.GetComponent<RectTransform>();
                            vsyncRt.anchorMin = new Vector2(0f, 0.75f);
                            vsyncRt.anchorMax = new Vector2(0f, 0.75f);
                            vsyncRt.anchoredPosition = new Vector2(15f, -82.8f);
                            vsyncRt.sizeDelta = new Vector2(170f, 10f);
                            vsyncRt.pivot = new Vector2(0f, 1f);

                            // Vsync Header
                            vsync.WithChildObject("Header", vsyncHeader =>
                                {
                                    vsyncHeader.WithComponent<TextMeshProUGUI>(tmp =>
                                        {
                                            tmp.font = PrefabBuilder.LoadAsset<TMP_FontAsset>(FontDreiFraktur);
                                            tmp.text = "Vsync\n";
                                            tmp.color = Color.white;
                                            tmp.fontSize = 8f;
                                            tmp.enableAutoSizing = false;
                                            tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
                                            tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
                                            tmp.textWrappingMode = TextWrappingModes.Normal;
                                        }
                                    );

                                    var vsyncHeaderRt = vsyncHeader.GameObject.GetComponent<RectTransform>();
                                    vsyncHeaderRt.anchorMin = new Vector2(0f, 0f);
                                    vsyncHeaderRt.anchorMax = new Vector2(0f, 1f);
                                    vsyncHeaderRt.anchoredPosition = Vector2.zero;
                                    vsyncHeaderRt.sizeDelta = new Vector2(40f, 0f);
                                    vsyncHeaderRt.pivot = new Vector2(0f, 0.5f);
                                }
                            );

                            // Vsync Bottom (On/Off buttons)
                            TMP_Text onText = null;
                            MaskableGraphic onPlate = null;
                            DesignButton onButton = null;

                            TMP_Text offText = null;
                            MaskableGraphic offPlate = null;
                            DesignButton offButton = null;

                            vsync.WithChildObject("Bottom", vsyncBottom =>
                                {
                                    var vsyncRc = ResponsiveContainerHelper.Add(vsyncBottom.GameObject);
                                    if (vsyncRc != null)
                                    {
                                        var so = new SerializedObject(vsyncRc);
                                        so.FindProperty("axis").enumValueIndex = 0; // Horizontal
                                        so.FindProperty("hBehaviour").enumValueIndex = 1; // FitContentInContainer
                                        so.FindProperty("vBehaviour").enumValueIndex = 1; // FitContentInContainer
                                        so.ApplyModifiedPropertiesWithoutUndo();
                                    }

                                    var bottomRt = vsyncBottom.GameObject.GetComponent<RectTransform>();
                                    bottomRt.anchorMin = new Vector2(0f, 0f);
                                    bottomRt.anchorMax = new Vector2(1f, 0f);
                                    bottomRt.anchoredPosition = new Vector2(20f, 0f);
                                    bottomRt.sizeDelta = new Vector2(-40f, 10f);
                                    bottomRt.pivot = new Vector2(0.5f, 0f);

                                    CreateSelectionButton(vsyncBottom, "On", "on",
                                        out onPlate, out onButton, out onText
                                    );
                                    CreateSelectionButton(vsyncBottom, "Off", "off",
                                        out offPlate, out offButton, out offText
                                    );
                                }
                            );

                            vsync.SetSerialized<DesignGroupSelection>("_textOnColor",
                                new Color(0.729f, 0.729f, 0.753f, 1f)
                            );
                            vsync.SetSerialized<DesignGroupSelection>("_textOffColor",
                                new Color(0.480f, 0.480f, 0.547f, 1f)
                            );
                            vsync.SetSerialized<DesignGroupSelection>("_plateOnColor", Color.white);
                            vsync.SetSerialized<DesignGroupSelection>("_plateOffColor",
                                new Color(0.667f, 0.667f, 0.667f, 1f)
                            );
                            vsync.SetSerialized<DesignGroupSelection>("_onText", onText);
                            vsync.SetSerialized<DesignGroupSelection>("_onPlate", onPlate);
                            vsync.SetSerialized<DesignGroupSelection>("_offText", offText);
                            vsync.SetSerialized<DesignGroupSelection>("_offPlate", offPlate);
                            vsync.SetSerialized<DesignGroupSelection>("_onButton", onButton);
                            vsync.SetSerialized<DesignGroupSelection>("_offButton", offButton);
                        }
                    );

                    // Bottom buttons (Cancel, Apply)
                    plate.WithChildObject("Bottom", bottom =>
                        {
                            var bottomRc = ResponsiveContainerHelper.Add(bottom.GameObject);
                            if (bottomRc != null)
                            {
                                var so = new SerializedObject(bottomRc);
                                so.FindProperty("axis").enumValueIndex = 0; // Horizontal
                                so.FindProperty("hBehaviour").enumValueIndex = 1; // FitContentInContainer
                                so.FindProperty("vBehaviour").enumValueIndex = 1; // FitContentInContainer
                                so.ApplyModifiedPropertiesWithoutUndo();
                            }

                            var bottomRt = bottom.GameObject.GetComponent<RectTransform>();
                            bottomRt.anchorMin = new Vector2(0f, 0f);
                            bottomRt.anchorMax = new Vector2(1f, 0f);
                            bottomRt.anchoredPosition = new Vector2(0.1f, 1f);
                            bottomRt.sizeDelta = new Vector2(-3.8f, 21f);
                            bottomRt.pivot = new Vector2(0.5f, 0f);

                            CreateActionButton(bottom, "Cancel", "cancel",
                                new Vector2(98.1f, 21f), out cancelDesignButton
                            );
                            CreateActionButton(bottom, "Apply", "apply",
                                new Vector2(98.1f, 21f), out applyDesignButton
                            );
                        }
                    );
                }
            );

            builder.SetSerialized<SettingsView>("_applyButton", applyDesignButton);
            builder.SetSerialized<SettingsView>("_cancelButton", cancelDesignButton);
            builder.SetSerialized<SettingsView>("_masterVolumeSlider", masterSlider);
            builder.SetSerialized<SettingsView>("_soundsVolumeSlider", sfxSlider);
            builder.SetSerialized<SettingsView>("_musicVolumeSlider", musicSlider);
            builder.SetSerialized<SettingsView>("_shakeIntensitySlider", shakeSlider);
            builder.SetSerialized<SettingsView>("_vsyncSelection", vsyncSelection);
        }

        private static void CreateSelectionButton(
            PrefabBuilder parent,
            string name,
            string text,
            out MaskableGraphic plateRef,
            out DesignButton designButtonRef,
            out TMP_Text textRef)
        {
            MaskableGraphic capturedPlate = null;
            DesignButton capturedDesignButton = null;
            TMP_Text capturedText = null;

            parent.WithChildObject(name, btn =>
                {
                    DesignElement designElement = null;

                    btn.WithComponent<Image>(img =>
                        {
                            img.sprite = PrefabBuilder.LoadAsset<Sprite>(ButtonSprite);
                            img.type = Image.Type.Sliced;
                            img.color = Color.white;
                            img.raycastTarget = true;
                            capturedPlate = img;
                        }
                    );

                    btn.WithComponent<Button>();
                    btn.WithComponent<DesignButton>(db =>
                        {
                            capturedDesignButton = db;
                            designElement = db.Element; // OnValidate auto-creates DesignElement
                        }
                    );

                    var btnRt = btn.GameObject.GetComponent<RectTransform>();
                    btnRt.anchorMin = new Vector2(0.5f, 1f);
                    btnRt.anchorMax = new Vector2(0.5f, 1f);
                    btnRt.anchoredPosition = Vector2.zero;
                    btnRt.sizeDelta = new Vector2(65f, 10f);
                    btnRt.pivot = new Vector2(0f, 1f);

                    DesignElementTextColor textColorBehaviour = null;

                    btn.WithChildObject("Text", textChild =>
                        {
                            textChild.WithComponent<TextMeshProUGUI>(tmp =>
                                {
                                    tmp.font = PrefabBuilder.LoadAsset<TMP_FontAsset>(FontDreiFraktur);
                                    tmp.text = text;
                                    tmp.color = new Color(0.730f, 0.729f, 0.755f, 1f);
                                    tmp.fontSize = 5.9f;
                                    tmp.enableAutoSizing = true;
                                    tmp.fontSizeMin = 1f;
                                    tmp.fontSizeMax = 10f;
                                    tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
                                    tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
                                    tmp.textWrappingMode = TextWrappingModes.Normal;
                                    capturedText = tmp;
                                }
                            );

                            textChild.WithComponent<DesignElementTextColor>(dtc =>
                                {
                                    textColorBehaviour = dtc;
                                }
                            );

                            var textRt = textChild.GameObject.GetComponent<RectTransform>();
                            textRt.anchorMin = new Vector2(0f, 0.5f);
                            textRt.anchorMax = new Vector2(1f, 0.5f);
                            textRt.anchoredPosition = Vector2.zero;
                            textRt.sizeDelta = new Vector2(0f, 8.9f);
                            textRt.pivot = new Vector2(0.5f, 0.5f);

                            textChild.SetSerialized<DesignElementTextColor>("_text", capturedText);
                            textChild.SetSerialized<DesignElementTextColor>("_config",
                                PrefabBuilder.LoadAsset<BaseElementConfig>(TextConfigAsset)
                            );
                        }
                    );

                    btn.SetSerialized<DesignButton>("_element", designElement);
                    btn.SetSerialized<DesignElement>("_behaviours", new[] { textColorBehaviour });
                }
            );

            plateRef = capturedPlate;
            designButtonRef = capturedDesignButton;
            textRef = capturedText;
        }

        private static void CreateActionButton(
            PrefabBuilder parent,
            string name,
            string text,
            Vector2 size,
            out DesignButton designButtonRef)
        {
            DesignButton capturedDesignButton = null;

            parent.WithChildObject(name, btn =>
                {
                    DesignElement designElement = null;

                    btn.WithComponent<Image>(img =>
                        {
                            img.sprite = PrefabBuilder.LoadAsset<Sprite>(ButtonSprite);
                            img.type = Image.Type.Sliced;
                            img.color = Color.white;
                            img.raycastTarget = true;
                        }
                    );

                    btn.WithComponent<Button>();
                    btn.WithComponent<DesignButton>(db =>
                        {
                            capturedDesignButton = db;
                            designElement = db.Element; // OnValidate auto-creates DesignElement
                        }
                    );

                    var btnRt = btn.GameObject.GetComponent<RectTransform>();
                    btnRt.anchorMin = new Vector2(0.5f, 1f);
                    btnRt.anchorMax = new Vector2(0.5f, 1f);
                    btnRt.anchoredPosition = Vector2.zero;
                    btnRt.sizeDelta = size;
                    btnRt.pivot = new Vector2(0f, 1f);

                    DesignElementTextColor textColorBehaviour = null;

                    btn.WithChildObject("Text", textChild =>
                        {
                            textChild.WithComponent<TextMeshProUGUI>(tmp =>
                                {
                                    tmp.font = PrefabBuilder.LoadAsset<TMP_FontAsset>(FontDreiFraktur);
                                    tmp.text = text;
                                    tmp.color = new Color(0.730f, 0.729f, 0.755f, 1f);
                                    tmp.fontSize = 10f;
                                    tmp.enableAutoSizing = true;
                                    tmp.fontSizeMin = 1f;
                                    tmp.fontSizeMax = 10f;
                                    tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
                                    tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
                                    tmp.textWrappingMode = TextWrappingModes.Normal;
                                }
                            );

                            textChild.WithComponent<DesignElementTextColor>(dtc =>
                                {
                                    textColorBehaviour = dtc;
                                }
                            );

                            var textRt = textChild.GameObject.GetComponent<RectTransform>();
                            textRt.anchorMin = new Vector2(0f, 0.5f);
                            textRt.anchorMax = new Vector2(1f, 0.5f);
                            textRt.anchoredPosition = Vector2.zero;
                            textRt.sizeDelta = new Vector2(0f, 25f);
                            textRt.pivot = new Vector2(0.5f, 0.5f);

                            textChild.SetSerialized<DesignElementTextColor>("_text",
                                textChild.GameObject.GetComponent<TMP_Text>()
                            );
                            textChild.SetSerialized<DesignElementTextColor>("_config",
                                PrefabBuilder.LoadAsset<BaseElementConfig>(TextConfigAsset)
                            );
                        }
                    );

                    btn.SetSerialized<DesignButton>("_element", designElement);
                    btn.SetSerialized<DesignElement>("_behaviours", new[] { textColorBehaviour });
                }
            );

            designButtonRef = capturedDesignButton;
        }
    }
}
#endif