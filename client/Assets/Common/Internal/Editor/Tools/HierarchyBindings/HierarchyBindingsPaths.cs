using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal {
    // Сгенерированный класс должен лежать в сборке, которую видит пользовательский класс:
    // тот его наследует. Поэтому целимся в сборку скрипта, уже висящего на объекте, и только
    // если своих скриптов нет — в ближайший asmdef над сценой или префабом.
    internal static class HierarchyBindingsPaths {
        public const string FallbackFolder = "Assets/Common/Internal/Runtime/Tools/PrefabHierarchy/Generated";

        private const string LogTag = "HierarchyBindingsGenerator";

        private const string PluginsFolder = "Assets/Plugins/";

        public static string ResolveGeneratedFolder(GameObject root, string assetPath) {
            var ownerAssembly = ResolveOwnerAssembly(root);
            if (string.IsNullOrEmpty(ownerAssembly) == false)
                return CatalogAssemblies.GetGeneratedFolder(LogTag, ownerAssembly, FallbackFolder);

            var asmdefFolder = FindAsmdefFolder(assetPath);
            if (string.IsNullOrEmpty(asmdefFolder) == false)
                return CatalogAssemblies.GetConsumerGeneratedFolder(asmdefFolder);

            return FallbackFolder;
        }

        public static string ResolveDefaultNamespace(GameObject root) {
            var owner = FindOwner(root);
            return owner == null ? string.Empty : owner.GetType().Namespace ?? string.Empty;
        }

        private static string ResolveOwnerAssembly(GameObject root) {
            var owner = FindOwner(root);
            return owner == null ? string.Empty : owner.GetType().Assembly.GetName().Name;
        }

        // Владелец — первый скрипт проекта на объекте. Биндинги сами по себе владельцем не
        // считаются: на повторной генерации они уже висят на объекте. Скрипты плагинов тоже
        // отсекаем: сборка плагина формально «не Unity», но класть в неё свой генерат нельзя.
        private static MonoBehaviour FindOwner(GameObject root) {
            foreach (var behaviour in root.GetComponents<MonoBehaviour>()) {
                if (behaviour == null || behaviour is IObjectBindings)
                    continue;

                var assembly = behaviour.GetType().Assembly.GetName().Name;
                if (IsProjectAssembly(assembly) == false)
                    continue;

                if (IsProjectScript(behaviour) == false)
                    continue;

                return behaviour;
            }

            return null;
        }

        private static bool IsProjectAssembly(string assemblyName) {
            if (string.Equals(assemblyName, "Internal", StringComparison.Ordinal))
                return true;

            return CatalogAssemblies.IsConsumerAssembly(assemblyName);
        }

        // Тип ничего не знает о том, откуда приехал, поэтому смотрим на путь самого скрипта.
        private static bool IsProjectScript(MonoBehaviour behaviour) {
            var script = MonoScript.FromMonoBehaviour(behaviour);
            if (script == null)
                return false;

            var path = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrEmpty(path))
                return false;

            if (path.StartsWith(PluginsFolder, StringComparison.Ordinal))
                return false;

            return path.StartsWith("Assets/", StringComparison.Ordinal);
        }

        private static string FindAsmdefFolder(string assetPath) {
            if (string.IsNullOrEmpty(assetPath))
                return string.Empty;

            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            while (string.IsNullOrEmpty(folder) == false && folder.StartsWith("Assets", StringComparison.Ordinal)) {
                if (ContainsAsmdef(folder))
                    return folder;

                if (folder == "Assets")
                    return string.Empty;

                folder = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            }

            return string.Empty;
        }

        private static bool ContainsAsmdef(string folder) {
            try {
                var fullPath = CatalogPaths.ToFullPath(folder);
                return Directory.Exists(fullPath) && Directory.GetFiles(fullPath, "*.asmdef").Length > 0;
            }
            catch (Exception exception) {
                Debug.LogError($"[{LogTag}] Failed to scan {folder}: {exception}");
                return false;
            }
        }
    }
}
