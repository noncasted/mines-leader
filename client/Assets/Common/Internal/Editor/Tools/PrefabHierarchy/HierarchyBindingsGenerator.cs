using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Internal {
    // Генерация идёт в две фазы: сначала пишем класс, потом, уже после компиляции, вешаем его
    // на объект и заполняем ссылки. Заявка между фазами живёт в SessionState, потому что домен
    // между ними перезагружается.
    public static class HierarchyBindingsGenerator {
        public const string TypeNameSuffix = "Bindings";

        private const string LogTag = "HierarchyBindingsGenerator";

        public static string ToTypeName(string bindingsName) {
            var identifier = HierarchyBindingsNaming.ToIdentifier(bindingsName);
            if (string.IsNullOrEmpty(identifier))
                return string.Empty;

            if (identifier.EndsWith(TypeNameSuffix, StringComparison.Ordinal))
                return identifier;

            return identifier + TypeNameSuffix;
        }

        [InitializeOnLoadMethod]
        private static void InstallRegenerateHandler() {
            ObjectBindings.RegenerateHandler = Regenerate;
        }

        public static void Regenerate(ObjectBindings bindings) {
            var type = ResolveGeneratedType(bindings);
            if (type == null) {
                Debug.LogError($"[{LogTag}] Failed to resolve the generated type.", bindings);
                return;
            }

            Generate(
                bindings.gameObject,
                type.Name,
                type.Namespace ?? string.Empty,
                bindings.IsSceneService,
                bindings.IsEntityComponent
            );
        }

        // Пользовательский класс наследует сгенерированный, поэтому по компоненту на объекте
        // нужно подняться до типа, который лежит прямо под ObjectBindings — именно он генерируется.
        public static Type ResolveGeneratedType(Component bindings) {
            if (bindings == null)
                return null;

            var type = bindings.GetType();
            while (type != null && type.BaseType != typeof(ObjectBindings))
                type = type.BaseType;

            return type;
        }

        public static void Generate(
            GameObject root,
            string bindingsName,
            string namespaceName,
            bool isSceneService,
            bool isEntityComponent) {
            var typeName = ToTypeName(bindingsName);
            if (string.IsNullOrEmpty(typeName)) {
                Debug.LogError($"[{LogTag}] '{bindingsName}' is not a C# identifier.");
                return;
            }

            if (HierarchyBindingsTarget.TryDescribe(root, out var assetPath, out var objectPath, out var isPrefab, out var describeError) == false) {
                Debug.LogError($"[{LogTag}] {describeError}", root);
                return;
            }

            var errors = new List<string>();
            var node = HierarchyBindingsScanner.Scan(root, typeName, errors);
            if (node == null) {
                Report(errors, root);
                return;
            }

            var folder = HierarchyBindingsPaths.ResolveGeneratedFolder(root, assetPath);
            var filePath = $"{folder}/{typeName}.g.cs";
            var code = HierarchyBindingsCodeGenerator.Build(
                node,
                namespaceName,
                HierarchyBindingsStructure.ComputeHash(node),
                isSceneService,
                isEntityComponent
            );

            HierarchyBindingsRequest.Save(new HierarchyBindingsRequest {
                AssetPath = assetPath,
                ObjectPath = objectPath,
                TypeName = typeName,
                Namespace = namespaceName,
                IsPrefabAsset = isPrefab,
                IsSceneService = isSceneService,
                IsEntityComponent = isEntityComponent
            });

            var changed = HasChanged(filePath, code);
            CatalogPaths.EnsureFolder(folder);
            GeneratedFile.WriteIfChanged(LogTag, filePath, code);

            // Без изменений в коде компиляции не будет, а значит и DidReloadScripts не сработает,
            // поэтому связываем сразу.
            if (changed)
                AssetDatabase.Refresh();
            else
                EditorApplication.delayCall += ProcessPending;
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded() {
            if (HierarchyBindingsRequest.Load() == null)
                return;

            EditorApplication.delayCall += ProcessPending;
        }

        private static void ProcessPending() {
            var request = HierarchyBindingsRequest.Load();
            if (request == null)
                return;

            HierarchyBindingsRequest.Clear();

            var type = FindType(request);
            if (type == null) {
                Debug.LogError($"[{LogTag}] Generated type '{request.TypeName}' not found. Fix compilation errors and generate again.");
                return;
            }

            using var target = HierarchyBindingsTarget.Resolve(request, out var resolveError);
            if (target == null) {
                Debug.LogError($"[{LogTag}] {resolveError}");
                return;
            }

            var errors = new List<string>();
            if (HierarchyBindingsBinder.Bind(target.Root, type, request, errors) == false) {
                Report(errors, target.Root);
                return;
            }

            target.Commit();
            AssetDatabase.SaveAssets();
            Debug.Log($"[{LogTag}] Bound {request.TypeName} on '{request.ObjectPath}' in {request.AssetPath}.");
        }

        private static Type FindType(HierarchyBindingsRequest request) {
            var fullName = string.IsNullOrEmpty(request.Namespace)
                ? request.TypeName
                : request.Namespace + "." + request.TypeName;

            foreach (var candidate in TypeCache.GetTypesDerivedFrom<MonoBehaviour>()) {
                if (string.Equals(candidate.FullName, fullName, StringComparison.Ordinal))
                    return candidate;
            }

            return null;
        }

        private static bool HasChanged(string assetPath, string content) {
            try {
                var fullPath = CatalogPaths.ToFullPath(assetPath);
                return File.Exists(fullPath) == false || File.ReadAllText(fullPath) != content;
            }
            catch (Exception) {
                return true;
            }
        }

        private static void Report(List<string> errors, UnityEngine.Object context) {
            foreach (var error in errors)
                Debug.LogError($"[{LogTag}] {error}", context);
        }
    }
}
