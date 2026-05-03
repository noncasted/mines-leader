using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace Internal {
    [Serializable]
    public class OptionsContainer {
        private const string ResourceName = "ProjectToolsSettings";

        [SerializeField] private AssetsOptions _assets = new();
        [SerializeField] private DebugOptions _debug = new();
        [SerializeField] private VersionOptions _version = new();
        [SerializeField] private BackendOptions _backend = new();
        [SerializeField] private PlatformOptions _platform = new();

        public AssetsOptions AssetsOptions => _assets;
        public DebugOptions DebugOptions => _debug;
        public VersionOptions VersionOptions => _version;
        public BackendOptions BackendOptions => _backend;
        public PlatformOptions PlatformOptions => _platform;

        public void Register(IContainerBuilder builder) {
            builder.RegisterInstance(this);
            builder.RegisterInstance(AssetsOptions);
            builder.RegisterInstance(PlatformOptions);
            builder.RegisterInstance(BackendOptions);
            builder.RegisterInstance(DebugOptions);
            builder.RegisterInstance(VersionOptions);
        }

        public static OptionsContainer Load() {
            try {
                var textAsset = Resources.Load<TextAsset>(ResourceName);

                if (textAsset == null) {
                    Debug.LogWarning("[OptionsContainer] Settings not found in Resources, using defaults");
                    return new OptionsContainer();
                }

                return JsonUtility.FromJson<OptionsContainer>(textAsset.text) ?? new OptionsContainer();
            }
            catch (Exception e) {
                Debug.LogError($"[OptionsContainer] Failed to load settings: {e.Message}");
                return new OptionsContainer();
            }
        }

        public void Save() {
            var path = Path.Combine(Application.dataPath, "Resources", $"{ResourceName}.json");

            try {
                var json = JsonUtility.ToJson(this, true);
                File.WriteAllText(path, json);

#if UNITY_EDITOR
                AssetDatabase.Refresh();
#endif
            }
            catch (Exception e) {
                Debug.LogError($"[OptionsContainer] Failed to save settings: {e.Message}");
            }
        }
    }
}
