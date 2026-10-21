using System;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Runs every editor generator once, in dependency order, synchronously:
    //   1. inputs without type names: colors, scenes, sprites, audio;
    //   2. hierarchy bindings (code of prefab assets and loaded scenes);
    //   3. catalogs that spell type names: env assets, prefabs;
    //   4. container asset map (views and bindings of prefabs and scenes, read by the container generator);
    //   5. shared addressables (after every catalog group exists);
    // then one refresh: changed scripts recompile and the Roslyn container generator runs inside that compile.
    // Bindings whose code changed are bound after the reload.
    //
    // Works after a failed compile too: the editor keeps the previous domain and its menus, and the generators read
    // type names from the script sources while the last compile failed (ScriptTypeNames). That unblocks a namespace
    // rename and code that already references a not yet generated catalog member.
    public static class RegenerateAll
    {
        private const string LogTag = "RegenerateAll";

        [MenuItem("Tools/Regenerate All", priority = 0)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException($"[{LogTag}] Exit Play mode first.");

            if (EditorApplication.isCompiling)
                throw new InvalidOperationException($"[{LogTag}] Scripts are compiling, run it after the reload.");

            if (ScriptTypeNames.IsDomainStale)
                Debug.LogWarning(
                    $"[{LogTag}] The last compile failed: the previous domain is running, type names are read from the script sources.");

            Step("Colors", ColorGenerator.GenerateNow);
            Step("Scenes", SceneGenerator.Generate);
            Step("Sprites", SpriteGenerator.GenerateNow);
            Step("Audio", AudioCatalogGenerator.GenerateNow);

            var bindingsChanged = false;
            var bindings = 0;
            Step("Hierarchy bindings", () => bindings = HierarchyBindingsBatch.PrepareProject(out bindingsChanged));

            Step("Env assets", AssetCatalogGenerator.GenerateNow);
            Step("Prefabs", PrefabCatalogGenerator.GenerateNow);
            Step("Container asset map", ContainerGraphPostprocessor.GenerateNow);
            Step("Shared addressables", SharedAddressablesSync.GenerateNow);

            // Refresh covers every written script; binding of changed bindings waits for the reload.
            HierarchyBindingsGenerator.Flush(bindingsChanged);
            AssetDatabase.Refresh();

            Debug.Log($"[{LogTag}] Done: {bindings} bindings checked. Changed scripts recompile now.");
        }

        private static void Step(string name, Action action)
        {
            Debug.Log($"[{LogTag}] {name}");
            action();
        }
    }
}
