using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Internal {
    public sealed class PrefabCatalogMetadata {
        public bool Included { get; set; }
        public string Group { get; set; } = string.Empty;
        public string ComponentType { get; set; } = string.Empty;

        // The script GUID survives type and namespace renames; ComponentType is only a readable mirror.
        public string ComponentGuid { get; set; } = string.Empty;

        public static bool TryRead(AssetImporter importer, out PrefabCatalogMetadata metadata) {
            metadata = null;

            if (importer == null)
                throw new ArgumentNullException(nameof(importer));

            var userData = importer.userData;
            if (string.IsNullOrWhiteSpace(userData))
                return false;

            try {
                var root = JObject.Parse(userData);
                if (root["prefabCatalog"] is not JObject catalog)
                    return false;

                metadata = new PrefabCatalogMetadata {
                    Included = catalog["included"] != null && catalog.Value<bool>("included"),
                    Group = catalog.Value<string>("group") ?? string.Empty,
                    ComponentType = catalog.Value<string>("componentType") ?? string.Empty,
                    ComponentGuid = catalog.Value<string>("componentGuid") ?? string.Empty
                };
                return true;
            }
            catch (Exception exception) {
                Debug.LogError($"[PrefabCatalogMetadata] Failed to read userData at {importer.assetPath}: {exception}");
                return false;
            }
        }

        public static PrefabCatalogMetadata ReadOrDefault(AssetImporter importer) {
            if (TryRead(importer, out var metadata))
                return metadata;

            return CreateDefault();
        }

        public static PrefabCatalogMetadata CreateDefault() {
            return new PrefabCatalogMetadata();
        }

        public static void Write(AssetImporter importer, PrefabCatalogMetadata metadata) {
            if (importer == null)
                throw new ArgumentNullException(nameof(importer));
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            try {
                var nextUserData = MergeUserData(importer.userData, metadata);
                if (importer.userData == nextUserData)
                    return;

                importer.userData = nextUserData;
                EditorUtility.SetDirty(importer);
                AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);
                PrefabGroupsRegistry.Invalidate();
            }
            catch (Exception exception) {
                Debug.LogError($"[PrefabCatalogMetadata] Failed to write userData at {importer.assetPath}: {exception}");
            }
        }

        public static Type ResolveType(string componentGuid, string componentType) {
            if (string.IsNullOrEmpty(componentGuid) == false) {
                var scriptPath = AssetDatabase.GUIDToAssetPath(componentGuid);
                if (string.IsNullOrEmpty(scriptPath) == false) {
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                    var scriptType = script == null ? null : script.GetClass();
                    if (scriptType != null)
                        return scriptType;
                }
            }

            if (string.IsNullOrEmpty(componentType))
                return null;

            return Type.GetType(componentType, false);
        }

        public static string ToScriptGuid(MonoBehaviour behaviour) {
            var script = MonoScript.FromMonoBehaviour(behaviour);
            if (script == null)
                return string.Empty;

            var scriptPath = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrEmpty(scriptPath))
                return string.Empty;

            return AssetDatabase.AssetPathToGUID(scriptPath);
        }

        public static string ToDisplayName(string componentType) {
            if (string.IsNullOrEmpty(componentType))
                return "(GameObject)";

            var display = componentType;
            var comma = display.IndexOf(',');
            if (comma > 0)
                display = display.Substring(0, comma);

            var lastDot = display.LastIndexOf('.');
            if (lastDot >= 0)
                display = display.Substring(lastDot + 1);

            return display;
        }

        private static string MergeUserData(string userData, PrefabCatalogMetadata metadata) {
            var catalog = new JObject {
                ["included"] = metadata.Included,
                ["group"] = metadata.Group ?? string.Empty,
                ["componentType"] = metadata.ComponentType ?? string.Empty,
                ["componentGuid"] = metadata.ComponentGuid ?? string.Empty
            };

            JObject root;
            if (string.IsNullOrWhiteSpace(userData)) {
                root = new JObject();
            }
            else {
                var parsed = JToken.Parse(userData);
                if (parsed is not JObject parsedObject)
                    throw new InvalidOperationException("userData is not a JSON object");

                root = parsedObject;
            }

            root["prefabCatalog"] = catalog;
            return root.ToString(Formatting.Indented);
        }
    }
}
