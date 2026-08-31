using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Internal {
    public enum SpriteCatalogKind {
        Sheet,
        Animation
    }

    public sealed class SpriteCatalogMetadata {
        public const float DefaultTime = 0.8f;
        public static readonly Color DefaultColor = Color.white;

        public bool Included { get; set; }
        public string Group { get; set; } = string.Empty;
        public SpriteCatalogKind Kind { get; set; } = SpriteCatalogKind.Sheet;
        public float Time { get; set; } = DefaultTime;
        public Color Color { get; set; } = DefaultColor;

        public static bool TryRead(AssetImporter importer, out SpriteCatalogMetadata metadata) {
            metadata = null;

            if (importer == null)
                throw new ArgumentNullException(nameof(importer));

            var userData = importer.userData;
            if (string.IsNullOrWhiteSpace(userData))
                return false;

            try {
                var root = JObject.Parse(userData);
                if (root["spriteCatalog"] is not JObject catalog)
                    return false;

                metadata = new SpriteCatalogMetadata {
                    Included = catalog["included"] != null && catalog.Value<bool>("included"),
                    Group = catalog.Value<string>("group") ?? string.Empty,
                    Kind = ParseKind(catalog.Value<string>("kind")),
                    Time = catalog["time"] != null ? catalog.Value<float>("time") : DefaultTime,
                    Color = ReadColor(catalog["color"])
                };
                return true;
            }
            catch (Exception exception) {
                Debug.LogError($"[SpriteCatalogMetadata] Failed to read userData at {importer.assetPath}: {exception}");
                return false;
            }
        }

        public static SpriteCatalogMetadata ReadOrDefault(AssetImporter importer) {
            if (TryRead(importer, out var metadata))
                return metadata;

            return CreateDefault(importer);
        }

        public static SpriteCatalogMetadata CreateDefault(AssetImporter importer) {
            if (importer == null)
                throw new ArgumentNullException(nameof(importer));

            return new SpriteCatalogMetadata {
                Group = GetDefaultGroup(importer.assetPath),
                Kind = GetDefaultKind(importer),
                Time = DefaultTime,
                Color = DefaultColor
            };
        }

        public static void Write(AssetImporter importer, SpriteCatalogMetadata metadata) {
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
                SpriteGroupsRegistry.Invalidate();
            }
            catch (Exception exception) {
                Debug.LogError($"[SpriteCatalogMetadata] Failed to write userData at {importer.assetPath}: {exception}");
            }
        }

        public static string GetDefaultGroup(string assetPath) {
            const string artPrefix = "Assets/Art/";
            if (string.IsNullOrEmpty(assetPath) ||
                assetPath.StartsWith(artPrefix, StringComparison.OrdinalIgnoreCase) == false)
                return string.Empty;

            var relative = assetPath.Substring(artPrefix.Length);
            var directory = Path.GetDirectoryName(relative);
            if (string.IsNullOrEmpty(directory))
                return string.Empty;

            var joined = directory.Replace("\\", string.Empty).Replace("/", string.Empty);
            return ToGroupName(joined);
        }

        public static SpriteCatalogKind GetDefaultKind(AssetImporter importer) {
            if (importer == null)
                throw new ArgumentNullException(nameof(importer));

            if (IsAsepriteImporter(importer) && HasMultipleAsepriteFrames(importer))
                return SpriteCatalogKind.Animation;

            return SpriteCatalogKind.Sheet;
        }

        public static string ToGroupName(string name) {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var identifier = SanitizeIdentifier(name);
            if (identifier.Length == 0)
                return string.Empty;

            if (char.IsDigit(identifier[0]))
                return "G" + identifier;

            return identifier;
        }

        public static string SanitizeIdentifier(string name) {
            var builder = new StringBuilder(name.Length);

            foreach (var character in name) {
                if (char.IsLetterOrDigit(character))
                    builder.Append(character);
            }

            return builder.ToString();
        }

        public static bool IsAsepriteImporter(AssetImporter importer) {
            if (importer == null)
                return false;

            if (importer.GetType().Name == "AsepriteImporter")
                return true;

            var extension = Path.GetExtension(importer.assetPath);
            return extension.Equals(".aseprite", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".ase", StringComparison.OrdinalIgnoreCase);
        }

        private static string MergeUserData(string userData, SpriteCatalogMetadata metadata) {
            var catalog = new JObject {
                ["included"] = metadata.Included,
                ["group"] = metadata.Group ?? string.Empty,
                ["kind"] = metadata.Kind.ToString(),
                ["time"] = metadata.Time,
                ["color"] = new JObject {
                    ["r"] = metadata.Color.r,
                    ["g"] = metadata.Color.g,
                    ["b"] = metadata.Color.b,
                    ["a"] = metadata.Color.a
                }
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

            root["spriteCatalog"] = catalog;
            return root.ToString(Formatting.Indented);
        }

        private static SpriteCatalogKind ParseKind(string kind) {
            if (string.Equals(kind, nameof(SpriteCatalogKind.Animation), StringComparison.Ordinal))
                return SpriteCatalogKind.Animation;

            return SpriteCatalogKind.Sheet;
        }

        private static Color ReadColor(JToken token) {
            if (token is not JObject color)
                return DefaultColor;

            return new Color(
                color["r"] != null ? color.Value<float>("r") : 1f,
                color["g"] != null ? color.Value<float>("g") : 1f,
                color["b"] != null ? color.Value<float>("b") : 1f,
                color["a"] != null ? color.Value<float>("a") : 1f
            );
        }

        private static bool HasMultipleAsepriteFrames(AssetImporter importer) {
            var serializedObject = new SerializedObject(importer);
            var frames = serializedObject.FindProperty("m_AnimatedSpriteImportData");
            if (frames != null && frames.isArray && frames.arraySize > 1)
                return true;

            var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(importer.assetPath);
            var spriteCount = 0;
            foreach (var asset in assets) {
                if (asset is Sprite)
                    spriteCount++;
            }

            return spriteCount > 1;
        }
    }
}
