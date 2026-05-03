#if UNITY_EDITOR
using Tools.PrefabBuilder;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global.Settings
{
    [PrefabDefinition]
    public static class SettingsPanelPrefab
    {
        private const string UxmlPath = "Assets/Global/UI/SettingsPanel.uxml";
        private const string PanelSettingsPath = "Assets/Menu/UI/MenuPanelSettings.asset";

        public static void Define(PrefabBuilder builder)
        {
            builder
                .WithName("Global/SettingsPanel")
                .WithComponent<UIDocument>();

            var uxml = AssetsBuilderExtensions.LoadAsset<VisualTreeAsset>(UxmlPath);
            var panelSettings = AssetsBuilderExtensions.LoadAsset<PanelSettings>(PanelSettingsPath);

            builder.SetSerialized<UIDocument>("sourceAsset", uxml);
            builder.SetSerialized<UIDocument>("m_PanelSettings", panelSettings);
        }
    }
}
#endif
