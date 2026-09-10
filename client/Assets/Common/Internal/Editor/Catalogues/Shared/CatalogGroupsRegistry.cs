using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Список групп каталога: то, что явно заведено руками (json), плюс то, что найдено
    // в уже размеченных ассетах. Json нужен, чтобы пустая группа не исчезала до первого ассета.
    [NoAutoStaticsCleanup]
    public abstract class CatalogGroupsRegistry
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented
        };

        private string[] _cachedGroups;

        protected CatalogGroupsRegistry(string logTag, string assetPath)
        {
            LogTag = logTag;
            AssetPath = assetPath;
        }

        public string AssetPath { get; }

        protected string LogTag { get; }

        public IReadOnlyList<string> GetGroups()
        {
            if (_cachedGroups != null)
                return _cachedGroups;

            var names = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var group in ReadFileGroups())
                names.Add(group);

            foreach (var group in DiscoverGroups())
                names.Add(group);

            _cachedGroups = new string[names.Count];
            names.CopyTo(_cachedGroups);
            return _cachedGroups;
        }

        public bool TryAddGroup(string name, out string groupName)
        {
            groupName = CatalogNaming.ToGroupName(name);

            if (string.IsNullOrEmpty(groupName))
            {
                Debug.LogError($"[{LogTag}] Group name is empty after sanitize.");
                return false;
            }

            var groups = new List<string>(ReadFileGroups());

            foreach (var existing in groups)
            {
                if (string.Equals(existing, groupName, StringComparison.OrdinalIgnoreCase))
                {
                    groupName = existing;
                    Invalidate();
                    return true;
                }
            }

            groups.Add(groupName);

            if (WriteFileGroups(groups) == false)
                return false;

            Invalidate();
            return true;
        }

        public void Invalidate()
        {
            _cachedGroups = null;
        }

        // Группы, выведенные из разметки самих ассетов.
        protected abstract IEnumerable<string> DiscoverGroups();

        private IReadOnlyList<string> ReadFileGroups()
        {
            var fullPath = CatalogPaths.ToFullPath(AssetPath);

            try
            {
                if (File.Exists(fullPath) == false)
                    return Array.Empty<string>();

                var file = JsonConvert.DeserializeObject<RegistryFile>(File.ReadAllText(fullPath));

                if (file?.groups == null)
                    return Array.Empty<string>();

                var groups = new List<string>(file.groups.Count);

                foreach (var group in file.groups)
                {
                    if (string.IsNullOrWhiteSpace(group) == false)
                        groups.Add(group);
                }

                return groups;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{LogTag}] Failed to read {AssetPath}: {exception}");
                return Array.Empty<string>();
            }
        }

        private bool WriteFileGroups(IReadOnlyList<string> groups)
        {
            var fullPath = CatalogPaths.ToFullPath(AssetPath);

            try
            {
                var directory = Path.GetDirectoryName(fullPath);

                if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
                    Directory.CreateDirectory(directory);

                var file = new RegistryFile { groups = new List<string>(groups) };
                File.WriteAllText(fullPath, JsonConvert.SerializeObject(file, JsonSettings));
                AssetDatabase.ImportAsset(AssetPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{LogTag}] Failed to write {AssetPath}: {exception}");
                return false;
            }
        }

        [Serializable]
        private sealed class RegistryFile
        {
            public List<string> groups = new();
        }
    }
}