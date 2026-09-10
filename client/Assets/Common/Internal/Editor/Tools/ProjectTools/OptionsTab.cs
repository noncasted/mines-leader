using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Internal
{
    public class OptionsTab : ProjectToolsTab
    {
        public override string Title => "Options";

        protected override void BuildContent(VisualElement parent)
        {
            var options = Host.Options;

            if (options == null)
            {
                parent.Add(new Label("OptionsContainer asset is missing"));
                return;
            }

            var debugSection = BuildSubSection("Debug Options");
            var gizmos = new Toggle("Enable Gizmos") { value = options.DebugOptions.EnableGizmos };
            gizmos.RegisterValueChangedCallback(e => Apply(() => options.DebugOptions.EnableGizmos = e.newValue));
            debugSection.Add(gizmos);

            var logs = new Toggle("Enable Logs") { value = options.DebugOptions.EnableLogs };
            logs.RegisterValueChangedCallback(e => Apply(() => options.DebugOptions.EnableLogs = e.newValue));
            debugSection.Add(logs);
            parent.Add(debugSection);

            var versionSection = BuildSubSection("Version Options");
            var field = new TextField("Version") { value = options.VersionOptions.Value };
            field.RegisterValueChangedCallback(e => Apply(() => options.VersionOptions.Value = e.newValue));
            versionSection.Add(field);
            parent.Add(versionSection);

            var backendSection = BuildSubSection("Backend Options");
            var envField = new EnumField("Environment", options.BackendOptions.Environment);

            envField.RegisterValueChangedCallback(e =>
                Apply(() => options.BackendOptions.Environment = (BackendEnvironment)e.newValue));
            backendSection.Add(envField);

            var prodUrl = new TextField("Production URL") { value = options.BackendOptions.ProductionApiUrl };

            prodUrl.RegisterValueChangedCallback(e =>
                Apply(() => options.BackendOptions.ProductionApiUrl = e.newValue));
            backendSection.Add(prodUrl);

            var localUrl = new TextField("Local URL") { value = options.BackendOptions.LocalApiUrl };
            localUrl.RegisterValueChangedCallback(e => Apply(() => options.BackendOptions.LocalApiUrl = e.newValue));
            backendSection.Add(localUrl);
            parent.Add(backendSection);

            var platformSection = BuildSubSection("Platform Options");
            var platformField = new EnumField("Platform Type", options.PlatformOptions.PlatformType);

            platformField.RegisterValueChangedCallback(e =>
                Apply(() => options.PlatformOptions.PlatformType = (PlatformType)e.newValue));
            platformSection.Add(platformField);
            parent.Add(platformSection);
        }

        // Опции теперь живут в ассете, поэтому правка должна пометить его грязным:
        // иначе перезагрузка домена откатит её ещё до нажатия Save.
        private void Apply(Action mutate)
        {
            var options = Host.Options;
            Undo.RecordObject(options, "Project Options");
            mutate();
            EditorUtility.SetDirty(options);
        }
    }
}