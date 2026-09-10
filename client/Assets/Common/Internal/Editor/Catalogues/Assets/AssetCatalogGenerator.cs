using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal {
    [NoAutoStaticsCleanup]
    public static class AssetCatalogGenerator {
        public const string CatalogAssetPath = "Assets/Common/Resources/AssetCatalog.asset";

        private const string ResourcesFolder = "Assets/Common/Resources";
        private const string LogTag = "AssetCatalogGenerator";

        private static readonly CatalogGenerationRunner Runner = new(LogTag, GenerateInternal);

        [InitializeOnLoadMethod]
        private static void OnEditorReload() {
            Runner.RunDelayed();
        }

        [MenuItem("Tools/GenerateAssetsCatalog")]
        public static void Generate() {
            Runner.Run();
        }

        public static void ScheduleGenerate() {
            Runner.Schedule();
        }

        private static void GenerateInternal() {
            var groups = BuildGroups(CollectSources());

            AssetDatabase.StartAssetEditing();
            try {
                WriteCatalogAsset(groups);
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }

            AssetsCatalogClassGenerator.Generate(groups);
            AssetGroupsRegistry.Instance.Invalidate();
            AssetDatabase.SaveAssets();

            Debug.Log($"[{LogTag}] Generated {groups.Count} asset group(s).");
        }

        private static List<AssetSource> CollectSources() {
            var sources = new List<AssetSource>();
            var assetsRoot = Application.dataPath;
            if (Directory.Exists(assetsRoot) == false)
                return sources;

            string[] files;
            try {
                files = Directory.GetFiles(assetsRoot, "*.asset", SearchOption.AllDirectories);
            }
            catch (Exception exception) {
                Debug.LogError($"[{LogTag}] Failed to scan Assets: {exception}");
                return sources;
            }

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            foreach (var file in files) {
                var path = CatalogPaths.ToAssetPath(projectRoot, file);
                if (string.IsNullOrEmpty(path))
                    continue;

                var importer = AssetImporter.GetAtPath(path);
                if (importer == null)
                    continue;

                if (AssetCatalogMetadata.TryRead(importer, out var metadata) == false)
                    continue;

                if (metadata.Included == false)
                    continue;

                var groupName = CatalogNaming.ToGroupName(metadata.Group);
                if (string.IsNullOrEmpty(groupName)) {
                    Debug.LogError($"[{LogTag}] Included asset has empty group: {path}");
                    continue;
                }

                var propertyName = CatalogNaming.ToGroupName(Path.GetFileNameWithoutExtension(path));
                if (string.IsNullOrEmpty(propertyName)) {
                    Debug.LogError($"[{LogTag}] Identifier is empty for {path}");
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<EnvAsset>(path);
                if (asset == null) {
                    Debug.LogError($"[{LogTag}] {path} is marked for the catalog but is not an EnvAsset");
                    continue;
                }

                sources.Add(new AssetSource {
                    Path = path,
                    Group = groupName,
                    PropertyName = propertyName,
                    Asset = asset
                });
            }

            return sources;
        }

        private static List<AssetGroupDefinition> BuildGroups(List<AssetSource> sources) {
            var groups = new Dictionary<string, AssetGroupDefinition>(StringComparer.Ordinal);
            var usedNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            sources.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));

            foreach (var source in sources) {
                if (groups.TryGetValue(source.Group, out var group) == false) {
                    group = new AssetGroupDefinition {
                        Name = source.Group,
                        ClassName = source.Group + "Assets"
                    };
                    groups.Add(source.Group, group);
                    usedNames.Add(source.Group, new HashSet<string>(StringComparer.Ordinal));
                }

                if (usedNames[source.Group].Add(source.PropertyName) == false) {
                    Debug.LogError(
                        $"[{LogTag}] Duplicate identifier '{source.PropertyName}' in group '{source.Group}'. Skipping {source.Path}.");
                    continue;
                }

                var type = source.Asset.GetType();
                group.Properties.Add(new AssetPropertyDefinition {
                    PropertyName = source.PropertyName,
                    Asset = source.Asset,
                    TypeFullName = (type.FullName ?? string.Empty).Replace('+', '.'),
                    TypeAssembly = type.Assembly.GetName().Name
                });
            }

            var result = new List<AssetGroupDefinition>();
            foreach (var group in groups.Values) {
                if (group.Properties.Count == 0)
                    continue;

                group.Properties.Sort((left, right) =>
                    string.Compare(left.PropertyName, right.PropertyName, StringComparison.Ordinal));
                result.Add(group);
            }

            result.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            AssignGeneratedTargets(result);
            return result;
        }

        // Класс группы обязан лежать в сборке, которая видит типы своих ассетов,
        // поэтому группа, смешавшая сборки, не кодогенерируется вовсе.
        private static void AssignGeneratedTargets(List<AssetGroupDefinition> groups) {
            foreach (var group in groups) {
                var assemblies = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var property in group.Properties) {
                    if (CatalogAssemblies.IsConsumerAssembly(property.TypeAssembly))
                        assemblies.Add(property.TypeAssembly);
                }

                if (assemblies.Count > 1) {
                    Debug.LogError(
                        $"[{LogTag}] Group '{group.Name}' mixes asset types from {string.Join(", ", assemblies)}. One group must stay in one assembly.");
                    group.SkipCodegen = true;
                    continue;
                }

                if (assemblies.Count == 1) {
                    group.TargetNamespace = assemblies.Min;
                    group.GeneratedFolder = AssetsCatalogClassGenerator.GetAssemblyGeneratedFolder(assemblies.Min);
                    continue;
                }

                group.TargetNamespace = "Internal";
                group.GeneratedFolder = AssetsCatalogClassGenerator.InternalGeneratedFolder;
            }
        }

        private static void WriteCatalogAsset(IReadOnlyList<AssetGroupDefinition> groups) {
            CatalogPaths.EnsureFolder(ResourcesFolder);

            var catalog = AssetDatabase.LoadAssetAtPath<EnvAssetCatalogAsset>(CatalogAssetPath);
            if (catalog == null) {
                catalog = ScriptableObject.CreateInstance<EnvAssetCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            var entries = new List<EnvAssetCatalogAsset.Entry>();
            foreach (var group in groups) {
                foreach (var property in group.Properties) {
                    entries.Add(new EnvAssetCatalogAsset.Entry {
                        Group = group.Name,
                        Name = property.PropertyName,
                        Asset = property.Asset
                    });
                }
            }

            if (EntriesMatch(catalog, entries))
                return;

            var serialized = new SerializedObject(catalog);
            var array = serialized.FindProperty("_entries");
            array.arraySize = entries.Count;

            for (var i = 0; i < entries.Count; i++) {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Group").stringValue = entries[i].Group;
                element.FindPropertyRelative("Name").stringValue = entries[i].Name;
                element.FindPropertyRelative("Asset").objectReferenceValue = entries[i].Asset;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static bool EntriesMatch(EnvAssetCatalogAsset catalog, IReadOnlyList<EnvAssetCatalogAsset.Entry> entries) {
            var existing = catalog.Entries;
            if (existing == null || existing.Count != entries.Count)
                return false;

            for (var i = 0; i < entries.Count; i++) {
                var left = existing[i];
                var right = entries[i];
                if (left == null)
                    return false;

                if (string.Equals(left.Group, right.Group, StringComparison.Ordinal) == false)
                    return false;

                if (string.Equals(left.Name, right.Name, StringComparison.Ordinal) == false)
                    return false;

                if (left.Asset != right.Asset)
                    return false;
            }

            return true;
        }

        private sealed class AssetSource {
            public string Path;
            public string Group;
            public string PropertyName;
            public EnvAsset Asset;
        }
    }

    internal sealed class AssetGroupDefinition {
        public string Name;
        public string ClassName;
        public string TargetNamespace;
        public string GeneratedFolder;
        public bool SkipCodegen;
        public List<AssetPropertyDefinition> Properties = new();
    }

    internal sealed class AssetPropertyDefinition {
        public string PropertyName;
        public EnvAsset Asset;
        public string TypeFullName;
        public string TypeAssembly;
    }
}
