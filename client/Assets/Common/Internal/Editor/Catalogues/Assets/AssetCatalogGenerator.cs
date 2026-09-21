using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public static class AssetCatalogGenerator
    {
        public const string GroupsFolder = "Assets/Common/Internal/Runtime/Catalogues/Assets/Groups";

        // Ассеты групп не из Addressables: Resources.Load ищет их по пути внутри Resources.
        private const string ResourcesSubfolder = "AssetGroups";
        private const string ResourcesFolder = GroupsFolder + "/Resources/" + ResourcesSubfolder;

        private const string GroupPrefix = "Assets_";
        private const string LogTag = "AssetCatalogGenerator";

        private static readonly CatalogGenerationRunner Runner = new(LogTag, GenerateInternal);

        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            Runner.RunDelayed();
        }

        [MenuItem("Tools/GenerateAssetsCatalog")]
        public static void Generate()
        {
            Runner.Run();
        }

        public static void ScheduleGenerate()
        {
            Runner.Schedule();
        }

        private static void GenerateInternal()
        {
            var groups = BuildGroups(CollectSources());

            // Переезд между Addressables и Resources идёт до пакетного редактирования: внутри него
            // перемещённый ассет по новому пути ещё не читается, и группа создалась бы заново.
            MoveGroupAssets(groups);

            AssetDatabase.StartAssetEditing();

            try
            {
                WriteGroupAssets(groups);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // Группы из Resources в Addressables не попадают: их addressable-группы sync снесёт как лишние.
            CatalogAddressablesSync.Sync(LogTag, GroupPrefix, GroupsFolder, groups.FindAll(group => group.IsAddressable));
            AssetsCatalogClassGenerator.Generate(groups);
            AssetGroupsRegistry.Instance.Invalidate();
            AssetDatabase.SaveAssets();

            Debug.Log($"[{LogTag}] Generated {groups.Count} asset group(s).");
        }

        private static List<AssetSource> CollectSources()
        {
            var sources = new List<AssetSource>();
            var assetsRoot = Application.dataPath;

            if (Directory.Exists(assetsRoot) == false)
                return sources;

            string[] files;

            try
            {
                files = Directory.GetFiles(assetsRoot, "*.asset", SearchOption.AllDirectories);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{LogTag}] Failed to scan Assets: {exception}");
                return sources;
            }

            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            foreach (var file in files)
            {
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

                if (string.IsNullOrEmpty(groupName))
                {
                    Debug.LogError($"[{LogTag}] Included asset has empty group: {path}");
                    continue;
                }

                var propertyName = CatalogNaming.ToGroupName(Path.GetFileNameWithoutExtension(path));

                if (string.IsNullOrEmpty(propertyName))
                {
                    Debug.LogError($"[{LogTag}] Identifier is empty for {path}");
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<EnvAsset>(path);

                if (asset == null)
                {
                    Debug.LogError($"[{LogTag}] {path} is marked for the catalog but is not an EnvAsset");
                    continue;
                }

                sources.Add(new AssetSource
                {
                    Path = path,
                    Group = groupName,
                    PropertyName = propertyName,
                    Asset = asset
                });
            }

            return sources;
        }

        private static List<AssetGroupDefinition> BuildGroups(List<AssetSource> sources)
        {
            var groups = new Dictionary<string, AssetGroupDefinition>(StringComparer.Ordinal);
            var usedNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            sources.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));

            foreach (var source in sources)
            {
                if (groups.TryGetValue(source.Group, out var group) == false)
                {
                    group = new AssetGroupDefinition
                    {
                        Name = source.Group,
                        ClassName = source.Group + "Assets",
                        IsAddressable = AssetGroupsRegistry.Instance.IsAddressable(source.Group),
                        ResourcePath = $"{ResourcesSubfolder}/{source.Group}"
                    };
                    groups.Add(source.Group, group);
                    usedNames.Add(source.Group, new HashSet<string>(StringComparer.Ordinal));
                }

                if (usedNames[source.Group].Add(source.PropertyName) == false)
                {
                    Debug.LogError(
                        $"[{LogTag}] Duplicate identifier '{source.PropertyName}' in group '{source.Group}'. Skipping {source.Path}.");
                    continue;
                }

                var type = source.Asset.GetType();

                group.Properties.Add(new AssetPropertyDefinition
                {
                    PropertyName = source.PropertyName,
                    FieldName = "_" + char.ToLowerInvariant(source.PropertyName[0]) + source.PropertyName.Substring(1),
                    Asset = source.Asset,
                    TypeFullName = (type.FullName ?? string.Empty).Replace('+', '.'),
                    TypeAssembly = type.Assembly.GetName().Name
                });
            }

            var result = new List<AssetGroupDefinition>();

            foreach (var group in groups.Values)
            {
                if (group.Properties.Count == 0)
                    continue;

                group.Properties.Sort((left, right) => string.Compare(left.PropertyName, right.PropertyName,
                    StringComparison.Ordinal));
                result.Add(group);
            }

            result.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            AssignGeneratedTargets(result);
            return result;
        }

        // Класс группы обязан лежать в сборке, которая видит типы своих ассетов,
        // поэтому группа, смешавшая сборки, не кодогенерируется вовсе.
        private static void AssignGeneratedTargets(List<AssetGroupDefinition> groups)
        {
            foreach (var group in groups)
            {
                var assemblies = new SortedSet<string>(StringComparer.Ordinal);

                foreach (var property in group.Properties)
                {
                    if (CatalogAssemblies.IsConsumerAssembly(property.TypeAssembly))
                        assemblies.Add(property.TypeAssembly);
                }

                if (assemblies.Count > 1)
                {
                    Debug.LogError(
                        $"[{LogTag}] Group '{group.Name}' mixes asset types from {string.Join(", ", assemblies)}. One group must stay in one assembly.");
                    group.SkipCodegen = true;
                    continue;
                }

                if (assemblies.Count == 1)
                {
                    group.TargetNamespace = assemblies.Min;
                    group.GeneratedFolder = AssetsCatalogClassGenerator.GetAssemblyGeneratedFolder(assemblies.Min);
                    continue;
                }

                group.TargetNamespace = "Internal";
                group.GeneratedFolder = AssetsCatalogClassGenerator.InternalGeneratedFolder;
            }
        }

        private static void WriteGroupAssets(IReadOnlyList<AssetGroupDefinition> groups)
        {
            CatalogPaths.EnsureFolder(GroupsFolder);

            var writtenPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in groups)
            {
                var path = GetGroupAssetPath(group, group.IsAddressable);
                writtenPaths.Add(path);
                WriteGroupAsset(path, group);
            }

            // Resources лежит внутри GroupsFolder, поэтому поиск устаревших ассетов захватывает и его.
            DeleteStaleAssets(writtenPaths);
        }

        private static string GetGroupAssetPath(AssetGroupDefinition group, bool isAddressable)
        {
            return isAddressable
                ? $"{GroupsFolder}/{group.Name}.asset"
                : $"{ResourcesFolder}/{group.Name}.asset";
        }

        // Ассет группы переезжает, а не пересоздаётся: GUID и ссылки на него сохраняются.
        private static void MoveGroupAssets(IReadOnlyList<AssetGroupDefinition> groups)
        {
            foreach (var group in groups)
            {
                var path = GetGroupAssetPath(group, group.IsAddressable);
                var previousPath = GetGroupAssetPath(group, group.IsAddressable == false);

                if (AssetDatabase.LoadAssetAtPath<EnvAssetGroupAsset>(previousPath) == null)
                    continue;

                CatalogPaths.EnsureFolder(group.IsAddressable ? GroupsFolder : ResourcesFolder);

                // Запись Addressables на ассете в Resources дала бы вторую копию в бандле.
                if (group.IsAddressable == false)
                    AddressableAssetSettingsDefaultObject.Settings?.RemoveAssetEntry(
                        AssetDatabase.AssetPathToGUID(previousPath), false);

                var error = AssetDatabase.MoveAsset(previousPath, path);

                if (string.IsNullOrEmpty(error) == false)
                    Debug.LogError($"[{LogTag}] Failed to move {previousPath} to {path}: {error}");
            }
        }

        private static void WriteGroupAsset(string path, AssetGroupDefinition group)
        {
            var asset = AssetDatabase.LoadAssetAtPath<EnvAssetGroupAsset>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EnvAssetGroupAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var serialized = new SerializedObject(asset);
            var entries = serialized.FindProperty("_entries");

            if (EntriesMatch(entries, group.Properties) == false)
            {
                entries.arraySize = group.Properties.Count;

                for (var i = 0; i < group.Properties.Count; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("Name").stringValue = group.Properties[i].PropertyName;
                    entry.FindPropertyRelative("Asset").objectReferenceValue = group.Properties[i].Asset;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }

            group.Address = AssetDatabase.AssetPathToGUID(path);
        }

        private static bool EntriesMatch(SerializedProperty entries, IReadOnlyList<AssetPropertyDefinition> properties)
        {
            if (entries == null || entries.isArray == false || entries.arraySize != properties.Count)
                return false;

            for (var i = 0; i < properties.Count; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);

                if (entry.FindPropertyRelative("Name").stringValue != properties[i].PropertyName)
                    return false;

                if (entry.FindPropertyRelative("Asset").objectReferenceValue != properties[i].Asset)
                    return false;
            }

            return true;
        }

        private static void DeleteStaleAssets(HashSet<string> writtenPaths)
        {
            if (AssetDatabase.IsValidFolder(GroupsFolder) == false)
                return;

            var guids = AssetDatabase.FindAssets($"t:{nameof(EnvAssetGroupAsset)}", new[] { GroupsFolder });

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (writtenPaths.Contains(path))
                    continue;

                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[{LogTag}] Deleted stale group asset: {path}");
            }
        }

        private sealed class AssetSource
        {
            public string Path;
            public string Group;
            public string PropertyName;
            public EnvAsset Asset;
        }
    }

    internal sealed class AssetGroupDefinition : ICatalogGroupDefinition
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string ClassName;
        public string TargetNamespace;
        public string GeneratedFolder;
        public bool SkipCodegen;
        public bool IsAddressable = true;
        public string ResourcePath;
        public List<AssetPropertyDefinition> Properties = new();
    }

    internal sealed class AssetPropertyDefinition
    {
        public string PropertyName;
        public string FieldName;
        public EnvAsset Asset;
        public string TypeFullName;
        public string TypeAssembly;
    }
}