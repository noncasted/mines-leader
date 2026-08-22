using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Tools {
    public static class PrefabAddressablesSync {
        internal static void Sync(IReadOnlyList<PrefabGroupDefinition> groups) {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) {
                Debug.LogError("[PrefabAddressablesSync] Addressable settings are missing.");
                return;
            }

            var usedGroups = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in groups) {
                var assetPath = $"{PrefabCatalogGenerator.GroupsFolder}/{group.Name}.asset";
                var address = MarkGroupAsset(settings, assetPath, group.Name);
                if (string.IsNullOrEmpty(address) == false)
                    group.Address = address;

                usedGroups.Add(AddressableGroupName(group.Name));
            }

            RemoveUnusedPrefabGroups(settings, usedGroups);
        }

        private static string MarkGroupAsset(AddressableAssetSettings settings, string assetPath, string groupName) {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) {
                Debug.LogError($"[PrefabAddressablesSync] Missing GUID for {assetPath}");
                return string.Empty;
            }

            var addressableGroup = GetOrCreateGroup(settings, AddressableGroupName(groupName));
            if (addressableGroup == null)
                return guid;

            var schema = addressableGroup.GetSchema<BundledAssetGroupSchema>();
            if (schema != null && schema.BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackTogether)
                schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;

            var entry = settings.CreateOrMoveEntry(guid, addressableGroup, false, false);
            if (entry == null) {
                Debug.LogError($"[PrefabAddressablesSync] Failed to mark {assetPath} addressable.");
                return guid;
            }

            if (entry.address != guid)
                entry.SetAddress(guid, false);

            RemoveOtherEntries(settings, addressableGroup, guid);
            return guid;
        }

        private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string groupName) {
            var group = settings.FindGroup(groupName);
            if (group != null)
                return group;

            return settings.CreateGroup(
                groupName,
                false,
                false,
                false,
                null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema)
            );
        }

        private static void RemoveOtherEntries(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            string keepGuid) {
            var stale = new List<AddressableAssetEntry>();
            foreach (var entry in group.entries) {
                if (entry == null || entry.guid == keepGuid)
                    continue;

                stale.Add(entry);
            }

            foreach (var entry in stale)
                settings.RemoveAssetEntry(entry.guid, false);
        }

        private static void RemoveUnusedPrefabGroups(AddressableAssetSettings settings, HashSet<string> usedGroups) {
            var stale = new List<AddressableAssetGroup>();
            foreach (var group in settings.groups) {
                if (group == null || group.Name.StartsWith("Prefabs_", StringComparison.Ordinal) == false)
                    continue;

                if (usedGroups.Contains(group.Name) == false)
                    stale.Add(group);
            }

            foreach (var group in stale)
                settings.RemoveGroup(group);
        }

        private static string AddressableGroupName(string groupName) {
            return "Prefabs_" + groupName;
        }
    }
}
