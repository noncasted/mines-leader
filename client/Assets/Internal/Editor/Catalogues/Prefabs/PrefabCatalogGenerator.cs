using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal {
    public static class PrefabCatalogGenerator {
        public const string GroupsFolder = "Assets/Internal/Runtime/Catalogues/Prefabs/Groups";

        private const float DebounceSeconds = 0.5f;

        private static readonly List<MetadataFix> _metadataFixes = new();

        private static bool _isGenerating;
        private static bool _isScheduled;
        private static double _scheduledTime;

        [InitializeOnLoadMethod]
        private static void OnEditorReload() {
            EditorApplication.delayCall += Generate;
        }

        [MenuItem("Tools/GeneratePrefabsCatalog")]
        public static void Generate() {
            if (_isGenerating)
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling) {
                ScheduleGenerate();
                return;
            }

            if (EditorApplication.isUpdating) {
                EditorApplication.delayCall += Generate;
                return;
            }

            _isGenerating = true;

            try {
                GenerateInternal();
            }
            catch (Exception exception) {
                Debug.LogError($"[PrefabCatalogGenerator] Generate failed: {exception}");
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
            var groups = new List<PrefabGroupDefinition>();
            _metadataFixes.Clear();
            AssetDatabase.StartAssetEditing();
            try {
                groups = BuildGroups(CollectSources());
                WriteGroupAssets(groups);
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }

            ApplyMetadataFixes();

            PrefabAddressablesSync.Sync(groups);
            PrefabsCatalogClassGenerator.Generate(groups);
            PrefabGroupsRegistry.Invalidate();
            AssetDatabase.SaveAssets();

            Debug.Log($"[PrefabCatalogGenerator] Generated {groups.Count} prefab group(s).");
        }

        private static List<PrefabSource> CollectSources() {
            var sources = new List<PrefabSource>();
            var assetsRoot = Application.dataPath;
            if (Directory.Exists(assetsRoot) == false)
                return sources;

            string[] files;
            try {
                files = Directory.GetFiles(assetsRoot, "*.prefab", SearchOption.AllDirectories);
            }
            catch (Exception exception) {
                Debug.LogError($"[PrefabCatalogGenerator] Failed to scan Assets: {exception}");
                return sources;
            }

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            foreach (var file in files) {
                var path = ToAssetPath(projectRoot, file);
                if (string.IsNullOrEmpty(path))
                    continue;

                var importer = AssetImporter.GetAtPath(path);
                if (importer == null)
                    continue;

                if (PrefabCatalogMetadata.TryRead(importer, out var metadata) == false)
                    continue;

                if (metadata.Included == false)
                    continue;

                var groupName = SpriteCatalogMetadata.ToGroupName(metadata.Group);
                if (string.IsNullOrEmpty(groupName)) {
                    Debug.LogError($"[PrefabCatalogGenerator] Included prefab has empty group: {path}");
                    continue;
                }

                var propertyName = SpriteCatalogMetadata.ToGroupName(Path.GetFileNameWithoutExtension(path));
                if (string.IsNullOrEmpty(propertyName)) {
                    Debug.LogError($"[PrefabCatalogGenerator] Identifier is empty for {path}");
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) {
                    Debug.LogError($"[PrefabCatalogGenerator] Failed to load prefab at {path}");
                    continue;
                }

                sources.Add(new PrefabSource {
                    Path = path,
                    Group = groupName,
                    PropertyName = propertyName,
                    Prefab = prefab,
                    ComponentType = ResolveComponentType(path, prefab, metadata)
                });
            }

            return sources;
        }

        // userData cannot hold a live type reference, so the root component is stored as a script GUID
        // with a readable name beside it. The GUID survives renames; the name is re-derived every run.
        private static string ResolveComponentType(string path, GameObject prefab, PrefabCatalogMetadata metadata) {
            var componentType = metadata.ComponentType ?? string.Empty;
            var componentGuid = metadata.ComponentGuid ?? string.Empty;
            if (string.IsNullOrEmpty(componentType) && string.IsNullOrEmpty(componentGuid))
                return string.Empty;

            var resolved = PrefabCatalogMetadata.ResolveType(componentGuid, componentType);
            var behaviour = FindBehaviour(prefab, resolved, componentType);
            if (behaviour == null) {
                Debug.LogError(
                    $"[PrefabCatalogGenerator] Root type '{PrefabCatalogMetadata.ToDisplayName(componentType)}' is missing on {path}. Falling back to GameObject, reassign the root in the inspector."
                );
                return string.Empty;
            }

            var actualType = behaviour.GetType().AssemblyQualifiedName ?? string.Empty;
            var actualGuid = PrefabCatalogMetadata.ToScriptGuid(behaviour);
            if (actualType == componentType && actualGuid == componentGuid)
                return componentType;

            _metadataFixes.Add(new MetadataFix {
                Path = path,
                ComponentType = actualType,
                ComponentGuid = actualGuid
            });

            if (string.IsNullOrEmpty(componentType) == false && actualType != componentType)
                Debug.Log($"[PrefabCatalogGenerator] Root type moved: '{componentType}' -> '{actualType}' for {path}.");

            return actualType;
        }

        private static MonoBehaviour FindBehaviour(GameObject prefab, Type resolved, string componentType) {
            if (resolved != null)
                return prefab.GetComponent(resolved) as MonoBehaviour;

            // Legacy metadata without a GUID: the type moved namespaces but kept its name.
            var shortName = PrefabCatalogMetadata.ToDisplayName(componentType);
            MonoBehaviour matched = null;
            foreach (var behaviour in prefab.GetComponents<MonoBehaviour>()) {
                if (behaviour == null || behaviour.GetType().Name != shortName)
                    continue;

                if (matched != null)
                    return null;

                matched = behaviour;
            }

            return matched;
        }

        private static void ApplyMetadataFixes() {
            foreach (var fix in _metadataFixes) {
                var importer = AssetImporter.GetAtPath(fix.Path);
                if (importer == null)
                    continue;

                var metadata = PrefabCatalogMetadata.ReadOrDefault(importer);
                metadata.ComponentType = fix.ComponentType;
                metadata.ComponentGuid = fix.ComponentGuid;
                PrefabCatalogMetadata.Write(importer, metadata);
            }

            _metadataFixes.Clear();
        }

        private static List<PrefabGroupDefinition> BuildGroups(List<PrefabSource> sources) {
            sources.Sort((left, right) => {
                var group = string.Compare(left.Group, right.Group, StringComparison.Ordinal);
                if (group != 0)
                    return group;

                return string.Compare(left.Path, right.Path, StringComparison.Ordinal);
            });

            var groups = new Dictionary<string, PrefabGroupDefinition>(StringComparer.Ordinal);
            var usedNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var source in sources) {
                if (groups.TryGetValue(source.Group, out var group) == false) {
                    group = new PrefabGroupDefinition {
                        Name = source.Group,
                        ClassName = source.Group + "Prefabs",
                        Properties = new List<PrefabPropertyDefinition>()
                    };
                    groups.Add(source.Group, group);
                    usedNames.Add(source.Group, new HashSet<string>(StringComparer.Ordinal));
                }

                if (usedNames[source.Group].Add(source.PropertyName) == false) {
                    Debug.LogError(
                        $"[PrefabCatalogGenerator] Duplicate identifier '{source.PropertyName}' in group '{source.Group}'. Skipping {source.Path}."
                    );
                    continue;
                }

                group.Properties.Add(new PrefabPropertyDefinition {
                    PropertyName = source.PropertyName,
                    FieldName = ToFieldName(source.PropertyName),
                    Prefab = source.Prefab,
                    ComponentType = source.ComponentType,
                    ComponentDisplayName = ToComponentDisplayName(source.ComponentType)
                });
            }

            var result = new List<PrefabGroupDefinition>();
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

        private static void AssignGeneratedTargets(List<PrefabGroupDefinition> groups) {
            foreach (var group in groups) {
                var assemblies = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var property in group.Properties) {
                    FillPropertyType(property);
                    if (IsConsumerAssembly(property.TypeAssembly))
                        assemblies.Add(property.TypeAssembly);
                }

                if (assemblies.Count > 1) {
                    Debug.LogError(
                        $"[PrefabCatalogGenerator] Group '{group.Name}' mixes root types from {string.Join(", ", assemblies)}. One group must stay in one assembly."
                    );
                    group.SkipCodegen = true;
                    continue;
                }

                if (assemblies.Count == 1) {
                    group.TargetNamespace = assemblies.Min;
                    group.GeneratedFolder = PrefabsCatalogClassGenerator.GetAssemblyGeneratedFolder(assemblies.Min);
                    group.EmitInternalPartial = false;
                    continue;
                }

                group.TargetNamespace = "Internal";
                group.GeneratedFolder = PrefabsCatalogClassGenerator.InternalGeneratedFolder;
                group.EmitInternalPartial = true;
            }
        }

        private static void FillPropertyType(PrefabPropertyDefinition property) {
            if (string.IsNullOrEmpty(property.ComponentType)) {
                property.IsGameObject = true;
                property.TypeFullName = "UnityEngine.GameObject";
                property.TypeAssembly = "UnityEngine";
                property.CodeTypeName = "global::UnityEngine.GameObject";
                return;
            }

            ParseTypeName(property.ComponentType, out var fullName, out var assemblyName);
            property.IsGameObject = false;
            property.TypeFullName = fullName.Replace('+', '.');
            property.TypeAssembly = assemblyName;
            property.CodeTypeName = "global::" + property.TypeFullName;
        }

        private static void ParseTypeName(string componentType, out string fullName, out string assemblyName) {
            var comma = componentType.IndexOf(',');
            if (comma < 0) {
                fullName = componentType.Trim();
                assemblyName = string.Empty;
                return;
            }

            fullName = componentType.Substring(0, comma).Trim();
            assemblyName = componentType.Substring(comma + 1).Trim();
            var assemblyComma = assemblyName.IndexOf(',');
            if (assemblyComma > 0)
                assemblyName = assemblyName.Substring(0, assemblyComma).Trim();
        }

        private static bool IsConsumerAssembly(string assemblyName) {
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            if (assemblyName.StartsWith("Unity", StringComparison.Ordinal))
                return false;

            if (assemblyName.StartsWith("System", StringComparison.Ordinal))
                return false;

            if (assemblyName == "mscorlib" || assemblyName == "netstandard")
                return false;

            if (assemblyName == "Internal" || assemblyName == "Internal.Editor")
                return false;

            return true;
        }

        private static void WriteGroupAssets(IReadOnlyList<PrefabGroupDefinition> groups) {
            EnsureFolder(GroupsFolder);

            var writtenPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in groups) {
                var path = $"{GroupsFolder}/{group.Name}.asset";
                writtenPaths.Add(path);
                WriteGroupAsset(path, group);
            }

            DeleteStaleAssets(writtenPaths);
        }

        private static void WriteGroupAsset(string path, PrefabGroupDefinition group) {
            var asset = AssetDatabase.LoadAssetAtPath<PrefabGroupAsset>(path);
            if (asset == null) {
                asset = ScriptableObject.CreateInstance<PrefabGroupAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            if (EntriesMatch(asset, group.Properties) == false) {
                ApplyEntries(asset, group.Properties);
                EditorUtility.SetDirty(asset);
            }

            group.Address = AssetDatabase.AssetPathToGUID(path);
        }

        private static bool EntriesMatch(PrefabGroupAsset asset, IReadOnlyList<PrefabPropertyDefinition> properties) {
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

                if (entry.FindPropertyRelative("Prefab").objectReferenceValue != property.Prefab)
                    return false;

                if (entry.FindPropertyRelative("ComponentType").stringValue != property.ComponentType)
                    return false;
            }

            return true;
        }

        private static void ApplyEntries(PrefabGroupAsset asset, IReadOnlyList<PrefabPropertyDefinition> properties) {
            var serializedObject = new SerializedObject(asset);
            var entries = serializedObject.FindProperty("_entries");
            entries.arraySize = properties.Count;

            for (var i = 0; i < properties.Count; i++) {
                var property = properties[i];
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Name").stringValue = property.PropertyName;
                entry.FindPropertyRelative("Prefab").objectReferenceValue = property.Prefab;
                entry.FindPropertyRelative("ComponentType").stringValue = property.ComponentType;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DeleteStaleAssets(HashSet<string> writtenPaths) {
            if (AssetDatabase.IsValidFolder(GroupsFolder) == false)
                return;

            var guids = AssetDatabase.FindAssets("t:PrefabGroupAsset", new[] { GroupsFolder });
            foreach (var guid in guids) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (writtenPaths.Contains(path))
                    continue;

                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[PrefabCatalogGenerator] Deleted stale group asset: {path}");
            }
        }

        private static string ToAssetPath(string projectRoot, string fullPath) {
            var normalized = Path.GetFullPath(fullPath).Replace('\\', '/');
            var root = projectRoot.Replace('\\', '/');
            if (normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase) == false)
                return normalized;

            return normalized.Substring(root.Length).TrimStart('/');
        }

        private static string ToFieldName(string propertyName) {
            return "_" + char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
        }

        private static string ToComponentDisplayName(string componentType) {
            if (string.IsNullOrEmpty(componentType))
                return "UnityEngine.GameObject";

            var display = componentType;
            var comma = display.IndexOf(',');
            if (comma > 0)
                display = display.Substring(0, comma).Trim();

            return display;
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

        private struct MetadataFix {
            public string Path;
            public string ComponentType;
            public string ComponentGuid;
        }

        private sealed class PrefabSource {
            public string Path;
            public string Group;
            public string PropertyName;
            public GameObject Prefab;
            public string ComponentType;
        }
    }

    internal sealed class PrefabGroupDefinition {
        public string Name;
        public string ClassName;
        public string Address;
        public string TargetNamespace;
        public string GeneratedFolder;
        public bool EmitInternalPartial;
        public bool SkipCodegen;
        public List<PrefabPropertyDefinition> Properties = new();
    }

    internal sealed class PrefabPropertyDefinition {
        public string PropertyName;
        public string FieldName;
        public GameObject Prefab;
        public string ComponentType;
        public string ComponentDisplayName;
        public bool IsGameObject;
        public string TypeFullName;
        public string TypeAssembly;
        public string CodeTypeName;
    }
}
