using Internal;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Global.UI
{
    // "Update available": the server closed a socket because this build speaks another protocol (a cached web build
    // after a deploy). Nothing works over the old protocol, so the notice covers everything for good: its own canvas
    // over every other one, built from code in the colors and the font of the menu. The button reloads the page on
    // the web; in the editor and a native build there is nothing to reload, it only says so.
    public class ClientOutdatedNotice : IClientOutdatedNotice
    {
        public ClientOutdatedNotice(IReadOnlyLifetime lifetime, ILoadingScreen loadingScreen)
        {
            _lifetime = lifetime;
            _loadingScreen = loadingScreen;
        }

        private const string Title = "Update available";
        private const string Text = "The game has been updated. This version can no longer connect.";
        private const string ButtonText = "RELOAD";

        // The menu canvas: 1920x1080, matched by height.
        private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        private static readonly Vector2 PanelSize = new(900f, 420f);
        private static readonly Vector2 ButtonSize = new(360f, 100f);

        private const float TitleSize = 84f;
        private const float TextSize = 48f;
        private const float ButtonTextSize = 64f;
        private const float Border = 6f;

        // Menu colors (panels and texts of the menu prefabs).
        private static readonly Color ShadeColor = new(0f, 0f, 0f, 0.8f);
        private static readonly Color PanelBorderColor = new(0.22352941f, 0.2901961f, 0.3137255f, 1f);
        private static readonly Color PanelColor = new(0.1254902f, 0.18039216f, 0.21568628f, 1f);
        private static readonly Color TitleColor = new(0.9098039f, 0.75686276f, 0.4392157f, 1f);
        private static readonly Color TextColor = new(0.78039217f, 0.8117647f, 0.8f, 1f);
        private static readonly Color ButtonColor = new(0.8396226f, 0.43952417f, 0.11485406f, 1f);
        private static readonly Color ButtonTextColor = Color.white;

        private readonly IReadOnlyLifetime _lifetime;
        private readonly ILoadingScreen _loadingScreen;

        private GameObject _root;

        public bool IsShown => _root != null;

        public void Show()
        {
            if (_root != null)
                return;

            Debug.LogWarning("[Network] The server runs another protocol: this client is outdated");

            // On the web the loading screen is the page itself, over the canvas: it would hide the notice.
            _loadingScreen.Hide();

            var font = GlobalAssets.GlobalPublisherOptions.OutdatedNoticeFont;
            _root = CreateCanvas();
            var root = _root;

            _lifetime.Listen(() => {
                // Exit from Play destroys the scene objects before the scope ends.
                if (root != null)
                    Object.Destroy(root);
            });

            var border = CreateRect("Panel", _root.transform);
            border.sizeDelta = PanelSize;
            border.gameObject.AddComponent<Image>().color = PanelBorderColor;

            var panel = CreateRect("Body", border);
            Stretch(panel, Border);
            panel.gameObject.AddComponent<Image>().color = PanelColor;

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset(40, 40, 30, 30);
            layout.spacing = 24f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateLabel(panel, font, Title, TitleSize, TitleColor);
            CreateLabel(panel, font, Text, TextSize, TextColor);
            CreateButton(panel, font);
        }

        private static GameObject CreateCanvas()
        {
            var gameObject = new GameObject("ClientOutdatedNotice", typeof(RectTransform));
            Object.DontDestroyOnLoad(gameObject);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 1f;

            gameObject.AddComponent<GraphicRaycaster>();

            // Takes every click and touch: nothing under the notice reacts any more.
            var shade = CreateRect("Shade", gameObject.transform);
            Stretch(shade, 0f);
            shade.gameObject.AddComponent<Image>().color = ShadeColor;

            return gameObject;
        }

        private static void CreateLabel(RectTransform parent, TMP_FontAsset font, string text, float size, Color color)
        {
            var rect = CreateRect("Label", parent);
            rect.gameObject.AddComponent<LayoutElement>().preferredWidth = PanelSize.x - 80f;

            var label = AddText(rect, font);
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.text = text;
        }

        private static void CreateButton(RectTransform parent, TMP_FontAsset font)
        {
            var rect = CreateRect("Button", parent);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = ButtonSize.x;
            layout.preferredHeight = ButtonSize.y;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonColor;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(OnReloadClicked);

            var label = AddText(CreateRect("Label", rect), font);
            Stretch((RectTransform)label.transform, 0f);
            label.fontSize = ButtonTextSize;
            label.color = ButtonTextColor;
            label.alignment = TextAlignmentOptions.Center;
            label.text = ButtonText;
        }

        // TextMeshProUGUI.Awake (run by AddComponent on an active object) loads the font: with none assigned yet it
        // takes TMP_Settings.defaultFontAsset and warns that it is missing. On an inactive object Awake waits for the
        // activation, so the font is assigned first. Other properties go after Awake: its LoadDefaultSettings
        // would replace them.
        private static TextMeshProUGUI AddText(RectTransform rect, TMP_FontAsset font)
        {
            var gameObject = rect.gameObject;
            gameObject.SetActive(false);

            var text = gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;

            gameObject.SetActive(true);
            return text;
        }

        private static void OnReloadClicked()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ClientOutdatedReload();
#else
            Debug.LogWarning("[Network] The client is outdated: restart it with the current build");
#endif
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);

            return rect;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // ClientOutdatedNotice.jslib: loads the page again, the new build comes with it.
        [DllImport("__Internal")]
        private static extern void ClientOutdatedReload();
#endif
    }
}
