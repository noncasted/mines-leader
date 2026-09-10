using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Internal
{
    // Каждой группе каталога соответствует своя addressable-группа `<prefix>_<name>`
    // ровно с одной записью — ассетом группы, адресуемым по собственному GUID.
    internal static class CatalogAddressablesSync
    {
        public static void Sync(
            string logTag,
            string groupPrefix,
            string groupsFolder,
            IReadOnlyList<ICatalogGroupDefinition> groups)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null)
            {
                Debug.LogError($"[{logTag}] Addressable settings are missing.");
                return;
            }

            var usedGroups = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in groups)
            {
                var assetPath = $"{groupsFolder}/{group.Name}.asset";
                var address = MarkGroupAsset(settings, logTag, groupPrefix, assetPath, group.Name);

                if (string.IsNullOrEmpty(address) == false)
                    group.Address = address;

                usedGroups.Add(groupPrefix + group.Name);
            }

            RemoveUnusedGroups(settings, groupPrefix, usedGroups);

            // Ссылки каталога могли поменяться, а с ними и набор общих ассетов.
            SharedAddressablesSync.ScheduleSync();
        }

        // Группа собирается в один бандл: всё её содержимое грузится вместе.
        internal static AddressableAssetGroup GetOrCreatePackedGroup(
            AddressableAssetSettings settings,
            string groupName)
        {
            var group = settings.FindGroup(groupName);

            if (group == null)
            {
                group = settings.CreateGroup(
                        groupName,
                        false,
                        false,
                        false,
                        null,
                        typeof(BundledAssetGroupSchema),
                        typeof(ContentUpdateGroupSchema)
                    );
            }

            if (group == null)
                return null;

            var schema = group.GetSchema<BundledAssetGroupSchema>();

            if (schema != null && schema.BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackTogether)
                schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;

            return group;
        }

        private static string MarkGroupAsset(
            AddressableAssetSettings settings,
            string logTag,
            string groupPrefix,
            string assetPath,
            string groupName)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);

            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"[{logTag}] Missing GUID for {assetPath}");
                return string.Empty;
            }

            var addressableGroup = GetOrCreatePackedGroup(settings, groupPrefix + groupName);

            if (addressableGroup == null)
                return guid;

            var entry = settings.CreateOrMoveEntry(guid, addressableGroup, false, false);

            if (entry == null)
            {
                Debug.LogError($"[{logTag}] Failed to mark {assetPath} addressable.");
                return guid;
            }

            if (entry.address != guid)
                entry.SetAddress(guid, false);

            RemoveOtherEntries(settings, addressableGroup, guid);
            return guid;
        }

        private static void RemoveOtherEntries(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            string keepGuid)
        {
            var stale = new List<AddressableAssetEntry>();

            foreach (var entry in group.entries)
            {
                if (entry == null || entry.guid == keepGuid)
                    continue;

                stale.Add(entry);
            }

            foreach (var entry in stale)
                settings.RemoveAssetEntry(entry.guid, false);
        }

        private static void RemoveUnusedGroups(
            AddressableAssetSettings settings,
            string groupPrefix,
            HashSet<string> usedGroups)
        {
            var stale = new List<AddressableAssetGroup>();

            foreach (var group in settings.groups)
            {
                if (group == null || group.Name.StartsWith(groupPrefix, StringComparison.Ordinal) == false)
                    continue;

                if (usedGroups.Contains(group.Name) == false)
                    stale.Add(group);
            }

            foreach (var group in stale)
                settings.RemoveGroup(group);
        }
    }
}