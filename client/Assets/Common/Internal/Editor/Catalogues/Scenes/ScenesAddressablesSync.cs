using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Internal
{
    // Все найденные сцены лежат в addressable-группе `Scenes`: StaticScene грузит их по GUID,
    // поэтому сцена без записи в Addressables падает с InvalidKeyException на старте.
    public static class ScenesAddressablesSync
    {
        private const string LogTag = "ScenesAddressablesSync";
        private const string GroupName = "Scenes";

        public static void Sync(IReadOnlyList<(string sceneName, string sceneGuid)> scenes)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null)
            {
                Debug.LogError($"[{LogTag}] Addressable settings are missing.");
                return;
            }

            var group = GetOrCreateGroup(settings);
            var used = new HashSet<string>(StringComparer.Ordinal);
            var changed = false;

            foreach (var (_, guid) in scenes)
            {
                used.Add(guid);

                var address = AssetDatabase.GUIDToAssetPath(guid);
                var entry = settings.FindAssetEntry(guid);

                if (entry == null || entry.parentGroup != group)
                {
                    entry = settings.CreateOrMoveEntry(guid, group, false, false);
                    changed = true;

                    if (entry == null)
                    {
                        Debug.LogError($"[{LogTag}] Failed to mark {address} addressable.");
                        continue;
                    }
                }

                if (entry.address != address)
                {
                    entry.SetAddress(address, false);
                    changed = true;
                }
            }

            var stale = new List<AddressableAssetEntry>();

            foreach (var entry in group.entries)
            {
                if (entry != null && used.Contains(entry.guid) == false)
                    stale.Add(entry);
            }

            foreach (var entry in stale)
            {
                settings.RemoveAssetEntry(entry.guid, false);
                changed = true;
            }

            if (changed == false)
                return;

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            Debug.Log($"[{LogTag}] Synced {scenes.Count} scene(s) into '{GroupName}' group.");
        }

        private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings)
        {
            var group = settings.FindGroup(GroupName);

            if (group != null)
                return group;

            return settings.CreateGroup(
                GroupName,
                false,
                false,
                false,
                null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema)
            );
        }
    }
}
