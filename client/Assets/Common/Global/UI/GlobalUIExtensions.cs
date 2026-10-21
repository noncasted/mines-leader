using Internal;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using Global.Web;
#endif

namespace Global.UI
{
    public static class GlobalUIExtensions
    {
        public static IScopeBuilder AddGlobalUI(this IScopeBuilder builder)
        {
            builder.AddUI();

            // On the web the loading screen lives in index.html: it also covers the build download.
            // The choice is made with #if, not if: the container generator builds all installer branches at once,
            // and a prefab or an alternative not registered at runtime breaks the scope creation.
#if UNITY_WEBGL && !UNITY_EDITOR
            builder.AddWebLoadingScreen();
#else
            builder.AddLoadingScreen(GlobalPrefabs.LoadingScreen);
#endif

            builder.AddClientOutdatedNotice(CreateOutdatedNoticeStyle());

            return builder;
        }

        // The menu font and the colors of the menu panels and texts; sizes and texts are the defaults of the style
        // (the menu canvas: 1920x1080, matched by height).
        private static ClientOutdatedNoticeStyle CreateOutdatedNoticeStyle()
        {
            return new ClientOutdatedNoticeStyle
            {
                Font = GlobalAssets.GlobalPublisherOptions.OutdatedNoticeFont,
                PanelBorderColor = new Color(0.22352941f, 0.2901961f, 0.3137255f, 1f),
                PanelColor = new Color(0.1254902f, 0.18039216f, 0.21568628f, 1f),
                TitleColor = new Color(0.9098039f, 0.75686276f, 0.4392157f, 1f),
                TextColor = new Color(0.78039217f, 0.8117647f, 0.8f, 1f),
                ButtonColor = new Color(0.8396226f, 0.43952417f, 0.11485406f, 1f),
                ButtonTextColor = Color.white
            };
        }
    }
}
