using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Internal
{
    // Пакетная перегенерация: код всех биндингов пишется до перезагрузки домена, а связывание
    // происходит уже после неё из общей очереди заявок.
    internal static class HierarchyBindingsBatch
    {
        private const string LogTag = "HierarchyBindingsGenerator";

        public static string RegenerateScene(Scene scene)
        {
            if (scene.IsValid() == false || scene.isLoaded == false)
                return "No loaded scene.";

            var bindings = Collect(scene);

            if (bindings.Count == 0)
                return $"No bindings found in '{scene.name}'.";

            var prepared = PrepareAll(bindings, true, new HashSet<string>(StringComparer.Ordinal), out var changed);

            if (prepared == 0)
                return $"Nothing regenerated in '{scene.name}'. See the console.";

            HierarchyBindingsGenerator.Flush(changed);
            return $"Regenerating {prepared} bindings in '{scene.name}'...";
        }

        // Every bindings component owned by a prefab asset or by a loaded scene. Scenes that are not loaded are not
        // opened (opening scenes from tools crashes the shared editor): they are listed in the log. Only bindings whose
        // code changed are bound again after the reload. The caller flushes once (changed = a file was written).
        public static int PrepareProject(out bool changed)
        {
            var types = new HashSet<string>(StringComparer.Ordinal);
            var owned = new List<ObjectBindings>();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));

                if (prefab == null)
                    continue;

                foreach (var component in prefab.GetComponentsInChildren<ObjectBindings>(true))
                {
                    // Nested prefabs and variants carry the bindings of their source asset.
                    if (PrefabUtility.GetCorrespondingObjectFromSource(component) == null)
                        owned.Add(component);
                }
            }

            var loadedScenes = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);

                if (scene.isLoaded == false)
                    continue;

                loadedScenes.Add(scene.path);

                // Scene objects of prefab instances are owned by the prefab asset.
                foreach (var component in Collect(scene))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(component) == false)
                        owned.Add(component);
                }
            }

            var prepared = PrepareAll(owned, false, types, out changed);
            LogSkippedScenes(loadedScenes);
            return prepared;
        }

        public static int Count(Scene scene)
        {
            return scene.IsValid() && scene.isLoaded ? Collect(scene).Count : 0;
        }

        private static int PrepareAll(
            IReadOnlyList<ObjectBindings> bindings,
            bool bindUnchanged,
            HashSet<string> types,
            out bool changed)
        {
            var prepared = 0;
            changed = false;

            foreach (var component in bindings)
            {
                var type = HierarchyBindingsGenerator.ResolveGeneratedType(component);

                if (type == null)
                {
                    Debug.LogError($"[{LogTag}] Failed to resolve the generated type.", component);
                    continue;
                }

                // Один класс — один объект: два владельца одного типа переписали бы файл друг
                // за другом, и выиграл бы последний.
                if (types.Add(type.FullName) == false)
                {
                    Debug.LogError($"[{LogTag}] '{type.FullName}' is used by more than one object. Skipped.",
                        component);
                    continue;
                }

                var ok = HierarchyBindingsGenerator.Prepare(
                        component.gameObject,
                        type.Name,
                        type.Namespace ?? string.Empty,
                        component.IsSceneService,
                        component.IsEntityComponent,
                        out var fileChanged,
                        bindUnchanged
                    );

                if (ok == false)
                    continue;

                prepared++;
                changed |= fileChanged;
            }

            return prepared;
        }

        private static List<ObjectBindings> Collect(Scene scene)
        {
            var result = new List<ObjectBindings>();

            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<ObjectBindings>(true));

            return result;
        }

        private static void LogSkippedScenes(HashSet<string> loadedScenes)
        {
            var scriptGuids = CollectBindingsScriptGuids();
            var skipped = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (loadedScenes.Contains(path) || ReferencesAny(path, scriptGuids) == false)
                    continue;

                skipped.Add(path);
            }

            if (skipped.Count == 0)
                return;

            Debug.Log(
                $"[{LogTag}] Not loaded scenes with bindings were skipped (open each and use Project Tools > " +
                $"Current Scene > Regenerate All Bindings): {string.Join(", ", skipped)}");
        }

        private static List<string> CollectBindingsScriptGuids()
        {
            var result = new List<string>();

            foreach (var type in TypeCache.GetTypesDerivedFrom<ObjectBindings>())
            {
                foreach (var guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
                {
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));

                    if (script != null && script.GetClass() == type)
                        result.Add(guid);
                }
            }

            return result;
        }

        private static bool ReferencesAny(string scenePath, List<string> scriptGuids)
        {
            var text = File.ReadAllText(CatalogPaths.ToFullPath(scenePath));

            foreach (var guid in scriptGuids)
            {
                if (text.Contains("guid: " + guid, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
