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
    // Там же лежат настройки групп, которые не выводятся из разметки ассетов.
    [NoAutoStaticsCleanup]
    public abstract class CatalogGroupsRegistry
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        private string[] _cachedGroups;
        private HashSet<string> _cachedNonAddressable;

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

            foreach (var group in ReadFile().groups)
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

            var file = ReadFile();

            foreach (var existing in file.groups)
            {
                if (string.Equals(existing, groupName, StringComparison.OrdinalIgnoreCase))
                {
                    groupName = existing;
                    Invalidate();
                    return true;
                }
            }

            file.groups.Add(groupName);

            if (WriteFile(file) == false)
                return false;

            Invalidate();
            return true;
        }

        // Группа по умолчанию адресуемая: в json перечислены только те, что лежат в Resources.
        public bool IsAddressable(string group)
        {
            _cachedNonAddressable ??= new HashSet<string>(ReadFile().nonAddressable ?? new List<string>(),
                StringComparer.Ordinal);

            return _cachedNonAddressable.Contains(CatalogNaming.ToGroupName(group)) == false;
        }

        // Возвращает true, если настройка поменялась и каталог надо перегенерировать.
        public bool SetAddressable(string group, bool isAddressable)
        {
            var groupName = CatalogNaming.ToGroupName(group);

            if (string.IsNullOrEmpty(groupName) || IsAddressable(groupName) == isAddressable)
                return false;

            var file = ReadFile();
            var nonAddressable = file.nonAddressable ?? new List<string>();

            if (isAddressable)
                nonAddressable.RemoveAll(name => string.Equals(name, groupName, StringComparison.Ordinal));
            else
                nonAddressable.Add(groupName);

            nonAddressable.Sort(StringComparer.Ordinal);
            file.nonAddressable = nonAddressable.Count > 0 ? nonAddressable : null;

            if (WriteFile(file) == false)
                return false;

            Invalidate();
            return true;
        }

        public void Invalidate()
        {
            _cachedGroups = null;
            _cachedNonAddressable = null;
        }

        // Группы, выведенные из разметки самих ассетов.
        protected abstract IEnumerable<string> DiscoverGroups();

        private RegistryFile ReadFile()
        {
            var fullPath = CatalogPaths.ToFullPath(AssetPath);

            try
            {
                if (File.Exists(fullPath) == false)
                    return new RegistryFile();

                var file = JsonConvert.DeserializeObject<RegistryFile>(File.ReadAllText(fullPath)) ?? new RegistryFile();
                file.groups = file.groups?.FindAll(group => string.IsNullOrWhiteSpace(group) == false) ??
                              new List<string>();

                return file;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{LogTag}] Failed to read {AssetPath}: {exception}");
                return new RegistryFile();
            }
        }

        private bool WriteFile(RegistryFile file)
        {
            var fullPath = CatalogPaths.ToFullPath(AssetPath);

            try
            {
                var directory = Path.GetDirectoryName(fullPath);

                if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
                    Directory.CreateDirectory(directory);

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
            public List<string> nonAddressable;
        }
    }
}
