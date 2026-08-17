using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tools {
    public static class SpriteGenerator {
        public const string GroupsFolder = "Assets/Tools/Runtime/SpriteBuilder/Groups";

        private const string ArtFolder = "Assets/Art";
        private const float DebounceSeconds = 0.5f;

        private static readonly string[] SourceExtensions = {
            ".aseprite",
            ".psd",
            ".png"
        };

        private static bool _isGenerating;
        private static bool _isScheduled;
        private static double _scheduledTime;

        [InitializeOnLoadMethod]
        private static void OnEditorReload() {
            EditorApplication.delayCall += Generate;
        }

        [MenuItem("Tools/GenerateSprites")]
        public static void Generate() {
            if (_isGenerating)
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling)
                return;

            if (EditorApplication.isUpdating) {
                EditorApplication.delayCall += Generate;
                return;
            }

            _isGenerating = true;

            try {
                GenerateInternal();
            }
            catch (Exception exception) {
                Debug.LogError($"[SpriteGenerator] Generate failed: {exception}");
            }
            finally {
                _isGenerating = false;
            }
        }

        public static void ScheduleGenerate() {
            _scheduledTime = EditorApplication.timeSinceStartup + DebounceSeconds;
            if (_isScheduled)
                return;

            _isScheduled = true;
            EditorApplication.update += TickSchedule;
        }

        private static void TickSchedule() {
            if (EditorApplication.timeSinceStartup < _scheduledTime)
                return;

            EditorApplication.update -= TickSchedule;
            _isScheduled = false;
            Generate();
        }

        private static void GenerateInternal() {
            if (AssetDatabase.IsValidFolder(ArtFolder) == false)
                return;

            var groups = new List<SpriteGroupDefinition>();
            AssetDatabase.StartAssetEditing();
            try {
                groups = BuildGroups(CollectSources());
                WriteGroupAssets(groups);
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }

            SpriteAddressablesSync.Sync(groups);
            SpritesClassGenerator.Generate(groups);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SpriteGenerator] Generated {groups.Count} sprite group(s).");
        }

        private static List<SpriteSource> CollectSources() {
            var sources = new List<SpriteSource>();
            var artRoot = Path.Combine(Application.dataPath, "Art");
            if (Directory.Exists(artRoot) == false)
                return sources;

            string[] files;
            try {
                files = Directory.GetFiles(artRoot, "*.*", SearchOption.AllDirectories);
            }
            catch (Exception exception) {
                Debug.LogError($"[SpriteGenerator] Failed to scan {ArtFolder}: {exception}");
                return sources;
            }

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            foreach (var file in files) {
                if (IsSourceFile(file) == false)
                    continue;

                var path = ToAssetPath(projectRoot, file);
                if (IsSourcePath(path) == false)
                    continue;

                var importer = AssetImporter.GetAtPath(path);
                if (importer == null)
                    continue;

                var metadata = ResolveMetadata(importer);
                if (metadata == null)
                    continue;

                var groupName = SpriteCatalogMetadata.ToGroupName(metadata.Group);
                if (string.IsNullOrEmpty(groupName))
                    continue;

                var propertyName = SpriteCatalogMetadata.ToGroupName(Path.GetFileNameWithoutExtension(path));
                if (string.IsNullOrEmpty(propertyName)) {
                    Debug.LogError($"[SpriteGenerator] Identifier is empty for {path}");
                    continue;
                }

                sources.Add(new SpriteSource {
                    Path = path,
                    Group = groupName,
                    PropertyName = propertyName,
                    Kind = metadata.Kind == SpriteCatalogKind.Animation
                        ? SpriteKind.Animation
                        : SpriteKind.Sheet,
                    Time = metadata.Time,
                    Color = metadata.Color,
                    Priority = GetPriority(path)
                });
            }

            return sources;
        }

        private static SpriteCatalogMetadata ResolveMetadata(AssetImporter importer) {
            if (SpriteCatalogMetadata.TryRead(importer, out var metadata) == false)
                return null;

            if (metadata.Included == false)
                return null;

            if (string.IsNullOrEmpty(metadata.Group) == false)
                return metadata;

            metadata.Group = SpriteCatalogMetadata.GetDefaultGroup(importer.assetPath);
            if (string.IsNullOrEmpty(metadata.Group))
                return null;

            SpriteCatalogMetadata.Write(importer, metadata);
            return metadata;
        }

        private static List<SpriteGroupDefinition> BuildGroups(List<SpriteSource> sources) {
            sources.Sort(CompareSources);

            var groups = new Dictionary<string, SpriteGroupDefinition>(StringComparer.Ordinal);
            var usedNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var source in sources) {
                if (groups.TryGetValue(source.Group, out var group) == false) {
                    group = new SpriteGroupDefinition {
                        Name = source.Group,
                        ClassName = source.Group + "Sprites",
                        Properties = new List<SpritePropertyDefinition>()
                    };
                    groups.Add(source.Group, group);
                    usedNames.Add(source.Group, new HashSet<string>(StringComparer.Ordinal));
                }

                var sprites = CollectSprites(source.Path, source.Kind);
                if (sprites.Length == 0) {
                    Debug.LogError($"[SpriteGenerator] No sprites found at {source.Path}");
                    continue;
                }

                if (source.Kind == SpriteKind.Animation) {
                    TryAddProperty(
                        group,
                        usedNames[source.Group],
                        source.PropertyName,
                        SpriteKind.Animation,
                        sprites,
                        source.Time,
                        source.Color,
                        source.Path);
                    continue;
                }

                for (var i = 0; i < sprites.Length; i++) {
                    var sprite = sprites[i];
                    var propertyName = ToSheetPropertyName(source.Path, sprite.name);
                    if (string.IsNullOrEmpty(propertyName)) {
                        Debug.LogError($"[SpriteGenerator] Identifier is empty for sprite '{sprite.name}' in {source.Path}");
                        continue;
                    }

                    TryAddProperty(
                        group,
                        usedNames[source.Group],
                        propertyName,
                        SpriteKind.Sheet,
                        new[] { sprite },
                        source.Time,
                        source.Color,
                        source.Path);
                }
            }

            var result = new List<SpriteGroupDefinition>();
            foreach (var group in groups.Values) {
                if (group.Properties.Count == 0)
                    continue;

                group.Properties.Sort((left, right) =>
                    string.Compare(left.PropertyName, right.PropertyName, StringComparison.Ordinal));
                result.Add(group);
            }

            result.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            return result;
        }

        private static void WriteGroupAssets(IReadOnlyList<SpriteGroupDefinition> groups) {
            EnsureFolder(GroupsFolder);

            var writtenPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in groups) {
                var path = $"{GroupsFolder}/{group.Name}.asset";
                writtenPaths.Add(path);
                WriteGroupAsset(path, group);
            }

            DeleteStaleAssets(writtenPaths);
        }

        private static void WriteGroupAsset(string path, SpriteGroupDefinition group) {
            var asset = AssetDatabase.LoadAssetAtPath<SpriteGroupAsset>(path);
            if (asset == null) {
                asset = ScriptableObject.CreateInstance<SpriteGroupAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            if (EntriesMatch(asset, group.Properties)) {
                group.Address = AssetDatabase.AssetPathToGUID(path);
                return;
            }

            ApplyEntries(asset, group.Properties);
            EditorUtility.SetDirty(asset);
            group.Address = AssetDatabase.AssetPathToGUID(path);
        }

        private static bool EntriesMatch(SpriteGroupAsset asset, IReadOnlyList<SpritePropertyDefinition> properties) {
            var serializedObject = new SerializedObject(asset);
            var entries = serializedObject.FindProperty("_entries");
            if (entries == null || entries.isArray == false)
                return false;

            if (entries.arraySize != properties.Count)
                return false;

            for (var i = 0; i < properties.Count; i++) {
                var property = properties[i];
                var entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("Name").stringValue != property.PropertyName)
                    return false;

                if (entry.FindPropertyRelative("Kind").enumValueIndex != (int)property.Kind)
                    return false;

                if (Mathf.Approximately(entry.FindPropertyRelative("Time").floatValue, property.Time) == false)
                    return false;

                if (entry.FindPropertyRelative("Color").colorValue != property.Color)
                    return false;

                var sprites = entry.FindPropertyRelative("Sprites");
                if (sprites == null || sprites.isArray == false || sprites.arraySize != property.Sprites.Length)
                    return false;

                for (var spriteIndex = 0; spriteIndex < property.Sprites.Length; spriteIndex++) {
                    if (sprites.GetArrayElementAtIndex(spriteIndex).objectReferenceValue != property.Sprites[spriteIndex])
                        return false;
                }
            }

            return true;
        }

        private static void ApplyEntries(SpriteGroupAsset asset, IReadOnlyList<SpritePropertyDefinition> properties) {
            var serializedObject = new SerializedObject(asset);
            var entries = serializedObject.FindProperty("_entries");
            entries.arraySize = properties.Count;

            for (var i = 0; i < properties.Count; i++) {
                var property = properties[i];
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Name").stringValue = property.PropertyName;
                entry.FindPropertyRelative("Kind").enumValueIndex = (int)property.Kind;
                entry.FindPropertyRelative("Time").floatValue = property.Time;
                entry.FindPropertyRelative("Color").colorValue = property.Color;

                var sprites = entry.FindPropertyRelative("Sprites");
                sprites.arraySize = property.Sprites.Length;
                for (var spriteIndex = 0; spriteIndex < property.Sprites.Length; spriteIndex++)
                    sprites.GetArrayElementAtIndex(spriteIndex).objectReferenceValue = property.Sprites[spriteIndex];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DeleteStaleAssets(HashSet<string> writtenPaths) {
            if (AssetDatabase.IsValidFolder(GroupsFolder) == false)
                return;

            var guids = AssetDatabase.FindAssets("t:SpriteGroupAsset", new[] { GroupsFolder });
            foreach (var guid in guids) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (writtenPaths.Contains(path))
                    continue;

                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[SpriteGenerator] Deleted stale group asset: {path}");
            }
        }

        private static Sprite[] CollectSprites(string assetPath, SpriteKind kind) {
            var sprites = LoadSprites(assetPath);
            if (sprites.Count == 0)
                return Array.Empty<Sprite>();

            if (kind == SpriteKind.Sheet)
                return ToArray(sprites);

            return OrderAnimationSprites(assetPath, sprites);
        }

        private static List<Sprite> LoadSprites(string assetPath) {
            var sprites = new List<Sprite>();
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (assets == null)
                return sprites;

            foreach (var asset in assets) {
                if (asset is Sprite sprite)
                    sprites.Add(sprite);
            }

            return sprites;
        }

        private static Sprite[] OrderAnimationSprites(string assetPath, IReadOnlyList<Sprite> sprites) {
            var names = ReadFrameNames(assetPath);
            if (names.Count == 0)
                return ToArray(sprites);

            var byName = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            for (var i = 0; i < sprites.Count; i++) {
                if (byName.ContainsKey(sprites[i].name) == false)
                    byName.Add(sprites[i].name, sprites[i]);
            }

            var ordered = new List<Sprite>(names.Count);
            for (var i = 0; i < names.Count; i++) {
                if (byName.TryGetValue(names[i], out var sprite))
                    ordered.Add(sprite);
            }

            return ordered.Count > 0 ? ordered.ToArray() : ToArray(sprites);
        }

        private static IReadOnlyList<string> ReadFrameNames(string assetPath) {
            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
                return Array.Empty<string>();

            if (SpriteCatalogMetadata.IsAsepriteImporter(importer)) {
                var names = ReadSerializedNames(importer, "m_AnimatedSpriteImportData");
                if (names.Count > 0)
                    return names;
            }

            if (importer is TextureImporter textureImporter &&
                textureImporter.spritesheet != null &&
                textureImporter.spritesheet.Length > 0) {
                var names = new string[textureImporter.spritesheet.Length];
                for (var i = 0; i < textureImporter.spritesheet.Length; i++)
                    names[i] = textureImporter.spritesheet[i].name;

                return names;
            }

            return Array.Empty<string>();
        }

        private static IReadOnlyList<string> ReadSerializedNames(AssetImporter importer, string propertyName) {
            var serializedObject = new SerializedObject(importer);
            var frames = serializedObject.FindProperty(propertyName);
            if (frames == null || frames.isArray == false || frames.arraySize == 0)
                return Array.Empty<string>();

            var names = new List<string>(frames.arraySize);
            for (var i = 0; i < frames.arraySize; i++) {
                var nameProperty = frames.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name");
                if (nameProperty == null)
                    nameProperty = frames.GetArrayElementAtIndex(i).FindPropertyRelative("name");

                if (nameProperty == null || string.IsNullOrEmpty(nameProperty.stringValue))
                    continue;

                names.Add(nameProperty.stringValue);
            }

            return names;
        }

        private static Sprite[] ToArray(IReadOnlyList<Sprite> sprites) {
            var array = new Sprite[sprites.Count];
            for (var i = 0; i < sprites.Count; i++)
                array[i] = sprites[i];

            return array;
        }

        private static bool IsSourceFile(string path) {
            var extension = Path.GetExtension(path);
            foreach (var sourceExtension in SourceExtensions) {
                if (extension.Equals(sourceExtension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsSourcePath(string path) {
            return string.IsNullOrEmpty(path) == false &&
                   path.StartsWith(ArtFolder + "/", StringComparison.OrdinalIgnoreCase) &&
                   IsSourceFile(path);
        }

        private static string ToAssetPath(string projectRoot, string fullPath) {
            var normalized = Path.GetFullPath(fullPath).Replace('\\', '/');
            var root = projectRoot.Replace('\\', '/');
            if (normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase) == false)
                return normalized;

            return normalized.Substring(root.Length).TrimStart('/');
        }

        private static int GetPriority(string path) {
            var extension = Path.GetExtension(path);
            if (extension.Equals(".aseprite", StringComparison.OrdinalIgnoreCase))
                return 0;

            if (extension.Equals(".psd", StringComparison.OrdinalIgnoreCase))
                return 1;

            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
                return 2;

            return 3;
        }

        private static int CompareSources(SpriteSource left, SpriteSource right) {
            var group = string.Compare(left.Group, right.Group, StringComparison.Ordinal);
            if (group != 0)
                return group;

            var priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
                return priority;

            return string.Compare(left.Path, right.Path, StringComparison.Ordinal);
        }

        private static bool TryAddProperty(
            SpriteGroupDefinition group,
            HashSet<string> usedNames,
            string propertyName,
            SpriteKind kind,
            Sprite[] sprites,
            float time,
            Color color,
            string sourcePath) {
            if (usedNames.Add(propertyName) == false) {
                Debug.LogError(
                    $"[SpriteGenerator] Duplicate identifier '{propertyName}' in group '{group.Name}'. Skipping lower-priority source {sourcePath}."
                );
                return false;
            }

            group.Properties.Add(new SpritePropertyDefinition {
                PropertyName = propertyName,
                FieldName = ToFieldName(propertyName),
                Kind = kind,
                Sprites = sprites,
                Time = time,
                Color = color
            });
            return true;
        }

        private static string ToSheetPropertyName(string assetPath, string spriteName) {
            if (string.IsNullOrEmpty(spriteName))
                return string.Empty;

            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            var remainder = spriteName;
            if (string.IsNullOrEmpty(fileName) == false &&
                remainder.StartsWith(fileName, StringComparison.OrdinalIgnoreCase)) {
                remainder = remainder.Substring(fileName.Length);
                if (remainder.StartsWith("_") || remainder.StartsWith("-") || remainder.StartsWith(" "))
                    remainder = remainder.Substring(1);
            }

            if (string.IsNullOrWhiteSpace(remainder))
                remainder = spriteName;

            return SpriteCatalogMetadata.ToGroupName(remainder);
        }

        private static string ToFieldName(string propertyName) {
            return "_" + char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
        }

        private static void EnsureFolder(string folderPath) {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++) {
                var next = current + "/" + parts[i];
                if (AssetDatabase.IsValidFolder(next) == false)
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        private sealed class SpriteSource {
            public string Path;
            public string Group;
            public string PropertyName;
            public SpriteKind Kind;
            public float Time;
            public Color Color;
            public int Priority;
        }
    }

    internal sealed class SpriteGroupDefinition {
        public string Name;
        public string ClassName;
        public string Address;
        public List<SpritePropertyDefinition> Properties = new();
    }

    internal sealed class SpritePropertyDefinition {
        public string PropertyName;
        public string FieldName;
        public SpriteKind Kind;
        public Sprite[] Sprites;
        public float Time;
        public Color Color;
    }
}
