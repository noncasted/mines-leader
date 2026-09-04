using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Internal {
    // Пакетная перегенерация: код всех биндингов пишется до перезагрузки домена, а связывание
    // происходит уже после неё из общей очереди заявок.
    internal static class HierarchyBindingsBatch {
        private const string LogTag = "HierarchyBindingsGenerator";

        public static string RegenerateScene(Scene scene) {
            if (scene.IsValid() == false || scene.isLoaded == false)
                return "No loaded scene.";

            var bindings = Collect(scene);
            if (bindings.Count == 0)
                return $"No bindings found in '{scene.name}'.";

            var types = new HashSet<string>(StringComparer.Ordinal);
            var prepared = 0;
            var changed = false;

            foreach (var component in bindings) {
                var type = HierarchyBindingsGenerator.ResolveGeneratedType(component);
                if (type == null) {
                    Debug.LogError($"[{LogTag}] Failed to resolve the generated type.", component);
                    continue;
                }

                // Один класс — один объект: два владельца одного типа переписали бы файл друг
                // за другом, и выиграл бы последний.
                if (types.Add(type.FullName) == false) {
                    Debug.LogError($"[{LogTag}] '{type.FullName}' is used by more than one object. Skipped.", component);
                    continue;
                }

                var ok = HierarchyBindingsGenerator.Prepare(
                    component.gameObject,
                    type.Name,
                    type.Namespace ?? string.Empty,
                    component.IsSceneService,
                    component.IsEntityComponent,
                    out var fileChanged
                );

                if (ok == false)
                    continue;

                prepared++;
                changed |= fileChanged;
            }

            if (prepared == 0)
                return $"Nothing regenerated in '{scene.name}'. See the console.";

            HierarchyBindingsGenerator.Flush(changed);
            return $"Regenerating {prepared} bindings in '{scene.name}'...";
        }

        public static int Count(Scene scene) {
            return scene.IsValid() && scene.isLoaded ? Collect(scene).Count : 0;
        }

        private static List<ObjectBindings> Collect(Scene scene) {
            var result = new List<ObjectBindings>();

            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<ObjectBindings>(true));

            return result;
        }
    }
}
