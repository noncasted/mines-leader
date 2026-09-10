using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Internal {
    // Ассет без собственной записи, на который ссылаются несколько addressable-групп, Addressables
    // копирует в бандл каждой из них. Такие ассеты становятся явными записями групп Shared_*,
    // и бандлы потребителей получают на них зависимость вместо копии.
    // Бандл тянет свои зависимости целиком, поэтому общие ассеты разложены по фазам потребителей:
    // меню не должно поднимать бандл с тем, что нужно только игре.
    [NoAutoStaticsCleanup]
    public static class SharedAddressablesSync {
        private const string LogTag = "SharedAddressablesSync";
        private const string GroupPrefix = "Shared_";
        private const string CommonGroup = GroupPrefix + "Common";
        private const string MenuGroup = GroupPrefix + "Menu";
        private const string GamePlayGroup = GroupPrefix + "GamePlay";

        // Шрифты, шейдеры и дефолтные ассеты пакетов нужны всем фазам.
        private static readonly string[] CommonPaths = {
            "Packages/",
            "Assets/Plugins/",
            "Assets/Art/Fonts/"
        };

        // Группы, которые грузит только одна фаза: Request/Load*Group в MenuScopeExtensions и
        // GamePlayScopeExtensions. Всё остальное (Global, Meta, группы обеих фаз) живёт в обеих.
        // Ошибка в таблице дублей не вернёт, только заставит фазу поднять лишний бандл.
        private static readonly Dictionary<string, Phase> GroupPhases = new(StringComparer.Ordinal) {
            ["Scenes_Menu"] = Phase.Menu,
            ["Prefabs_Menu"] = Phase.Menu,
            ["Sprites_MenuNavigation"] = Phase.Menu,
            ["Sprites_MenuUnlocks"] = Phase.Menu,
            ["Scenes_GamePlay"] = Phase.GamePlay,
            ["Sprites_Cards"] = Phase.GamePlay,
            ["Sprites_GameUIPlate"] = Phase.GamePlay
        };

        private static readonly CatalogGenerationRunner Runner = new(LogTag, Sync);

        [MenuItem("Tools/SyncSharedAddressables")]
        public static void Generate() {
            Runner.Run();
        }

        public static void ScheduleSync() {
            Runner.Schedule();
        }

        public static void Sync() {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) {
                Debug.LogError($"[{LogTag}] Addressable settings are missing.");
                return;
            }

            var targets = CollectTargets(settings);
            var changed = ApplyTargets(settings, targets);
            changed |= RemoveStale(settings, targets);

            if (changed == false)
                return;

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            Debug.Log($"[{LogTag}] Synced {targets.Count} shared asset(s).");
        }

        // GUID общего ассета -> имя Shared-группы.
        private static Dictionary<string, string> CollectTargets(AddressableAssetSettings settings) {
            var roots = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var explicitGuids = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in settings.groups) {
                if (group == null || IsSharedGroup(group.Name))
                    continue;

                var paths = new List<string>();
                foreach (var entry in group.entries) {
                    if (entry == null)
                        continue;

                    explicitGuids.Add(entry.guid);
                    paths.Add(entry.AssetPath);
                }

                if (paths.Count > 0)
                    roots.Add(group.Name, paths);
            }

            var consumers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();

            foreach (var root in roots) {
                visited.Clear();
                foreach (var path in root.Value) {
                    visited.Add(path);
                    pending.Push(path);
                }

                while (pending.Count > 0) {
                    foreach (var dependency in AssetDatabase.GetDependencies(pending.Pop(), false)) {
                        // Через скрипты приходят только иконки редактора, в бандлы они не попадают.
                        if (visited.Add(dependency) == false || IsScript(dependency))
                            continue;

                        // Запись другой группы — это зависимость на её бандл, её содержимое считает она сама.
                        if (explicitGuids.Contains(AssetDatabase.AssetPathToGUID(dependency)))
                            continue;

                        if (consumers.TryGetValue(dependency, out var groups) == false) {
                            groups = new HashSet<string>(StringComparer.Ordinal);
                            consumers.Add(dependency, groups);
                        }

                        groups.Add(root.Key);
                        pending.Push(dependency);
                    }
                }
            }

            var targets = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var consumer in consumers) {
                if (consumer.Value.Count < 2 || CanBeEntry(consumer.Key) == false)
                    continue;

                targets.Add(AssetDatabase.AssetPathToGUID(consumer.Key), ResolveGroup(consumer.Key, consumer.Value));
            }

            return targets;
        }

        private static string ResolveGroup(string path, HashSet<string> consumers) {
            foreach (var prefix in CommonPaths) {
                if (path.StartsWith(prefix, StringComparison.Ordinal))
                    return CommonGroup;
            }

            var phase = Phase.None;
            foreach (var consumer in consumers)
                phase |= GroupPhases.TryGetValue(consumer, out var groupPhase) ? groupPhase : Phase.Both;

            return phase switch {
                Phase.Menu => MenuGroup,
                Phase.GamePlay => GamePlayGroup,
                _ => CommonGroup
            };
        }

        private static bool ApplyTargets(AddressableAssetSettings settings, Dictionary<string, string> targets) {
            var changed = false;
            foreach (var target in targets) {
                var group = CatalogAddressablesSync.GetOrCreatePackedGroup(settings, target.Value);
                if (group == null) {
                    Debug.LogError($"[{LogTag}] Failed to create group '{target.Value}'.");
                    continue;
                }

                var entry = settings.FindAssetEntry(target.Key);
                if (entry != null && entry.parentGroup == group)
                    continue;

                if (settings.CreateOrMoveEntry(target.Key, group, false, false) == null) {
                    Debug.LogError($"[{LogTag}] Failed to mark {AssetDatabase.GUIDToAssetPath(target.Key)} addressable.");
                    continue;
                }

                changed = true;
            }

            return changed;
        }

        private static bool RemoveStale(AddressableAssetSettings settings, Dictionary<string, string> targets) {
            var changed = false;
            var staleGroups = new List<AddressableAssetGroup>();

            foreach (var group in settings.groups) {
                if (group == null || IsSharedGroup(group.Name) == false)
                    continue;

                var staleEntries = new List<AddressableAssetEntry>();
                foreach (var entry in group.entries) {
                    if (entry != null && targets.ContainsKey(entry.guid) == false)
                        staleEntries.Add(entry);
                }

                foreach (var entry in staleEntries) {
                    settings.RemoveAssetEntry(entry.guid, false);
                    changed = true;
                }

                if (group.entries.Count == 0)
                    staleGroups.Add(group);
            }

            foreach (var group in staleGroups) {
                settings.RemoveGroup(group);
                changed = true;
            }

            return changed;
        }

        // Повторяет AddressableAssetUtility.IsPathValidForEntry (internal) и не пускает Resources:
        // ассет оттуда всё равно уйдёт в плеер, запись только добавит вторую копию.
        private static bool CanBeEntry(string path) {
            if (path.StartsWith("Assets/", StringComparison.Ordinal) == false &&
                path.StartsWith("Packages/", StringComparison.Ordinal) == false)
                return false;

            if (path.Contains("/Editor/") || path.Contains("/Resources/"))
                return false;

            var extension = Path.GetExtension(path);
            return extension != ".unity" && extension != ".preset" && extension != ".asmdef";
        }

        private static bool IsScript(string path) {
            var extension = Path.GetExtension(path);
            return extension == ".cs" || extension == ".dll";
        }

        private static bool IsSharedGroup(string groupName) {
            return groupName.StartsWith(GroupPrefix, StringComparison.Ordinal);
        }

        [Flags]
        private enum Phase {
            None = 0,
            Menu = 1,
            GamePlay = 2,
            Both = Menu | GamePlay
        }
    }
}
