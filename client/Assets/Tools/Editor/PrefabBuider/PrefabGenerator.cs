using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Tools.PrefabBuilder;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tools
{
    public static class PrefabGenerator
    {
        private const string OutputFolder = "Assets/Resources/Generated";

        [MenuItem("Tools/GeneratePrefabs")]
        public static void Generate()
        {
            EnsureFolder(OutputFolder);

            var definitionTypes = TypeCache.GetTypesWithAttribute<PrefabDefinitionAttribute>();

            if (definitionTypes.Count == 0)
                return;

            // Sort: base prefabs (void Define(PrefabBuilder)) first, derived (any that returns PrefabBuilder) second
            var baseTypes = new List<Type>();
            var derivedTypes = new List<Type>();

            foreach (var type in definitionTypes)
            {
                var defineMethod = type.GetMethod("Define", BindingFlags.Public | BindingFlags.Static);

                if (defineMethod == null)
                    continue;
                var parameters = defineMethod.GetParameters();
                var returnsVoid = defineMethod.ReturnType == typeof(void);

                if (parameters.Length == 1 &&
                    parameters[0].ParameterType == typeof(PrefabBuilder.PrefabBuilder) &&
                    returnsVoid)
                    baseTypes.Add(type);
                else
                    derivedTypes.Add(type);
            }

            var generatedPrefabs = new List<(string asmdefName, string prefabName, string prefabPath)>();
            var hasErrors = false;

            foreach (var type in baseTypes)
            {
                try
                {
                    var result = GeneratePrefab(type);

                    if (result.HasValue)
                        generatedPrefabs.Add(result.Value);
                }
                catch (Exception ex)
                {
                    hasErrors = true;
                    Debug.LogError($"[PrefabGenerator] Failed to generate base prefab from {type.Name}: {ex}");
                }
            }

            foreach (var type in derivedTypes)
            {
                try
                {
                    var result = GeneratePrefab(type);

                    if (result.HasValue)
                        generatedPrefabs.Add(result.Value);
                }
                catch (Exception ex)
                {
                    hasErrors = true;
                    Debug.LogError($"[PrefabGenerator] Failed to generate prefab from {type.Name}: {ex}");
                }
            }

            if (hasErrors)
            {
                Debug.LogError("[PrefabGenerator] Skipping Prefabs.cs update due to errors above.");
                return;
            }

            if (generatedPrefabs.Count > 0)
            {
                CleanupStalePrefabs(generatedPrefabs);
                PrefabsClassGenerator.Generate(generatedPrefabs);
                AssetDatabase.Refresh();
                Debug.Log($"[PrefabGenerator] Generated {generatedPrefabs.Count} prefab(s).");
            }
        }

        private static void CleanupStalePrefabs(
            List<(string asmdefName, string prefabName, string prefabPath)> generatedPrefabs)
        {
            var generatedPaths = new HashSet<string>();

            foreach (var (_, prefabName, _) in generatedPrefabs)
            {
                generatedPaths.Add(Path.GetFullPath(OutputFolder + "/" + prefabName + ".prefab"));
            }

            var existingFiles = Directory.GetFiles(OutputFolder, "*.prefab", SearchOption.AllDirectories);

            foreach (var filePath in existingFiles)
            {
                var fullPath = Path.GetFullPath(filePath);

                if (generatedPaths.Contains(fullPath))
                    continue;

                var assetPath = filePath.Replace('\\', '/');
                AssetDatabase.DeleteAsset(assetPath);
                Debug.Log($"[PrefabGenerator] Deleted stale prefab: {assetPath}");
            }
        }

        private static (string asmdefName, string prefabName, string prefabPath)? GeneratePrefab(Type type)
        {
            var defineMethod = type.GetMethod("Define", BindingFlags.Public | BindingFlags.Static);

            if (defineMethod == null)
            {
                Debug.LogError(
                        $"[PrefabGenerator] {type.Name} has [PrefabDefinition] but no public static Define() method."
                    );
                return null;
            }

            var parameters = defineMethod.GetParameters();
            var returnsBuilder = defineMethod.ReturnType == typeof(PrefabBuilder.PrefabBuilder);

            // Support two signatures:
            //   void Define(PrefabBuilder builder)  -- base prefabs (builder created by generator)
            //   PrefabBuilder Define()              -- derived prefabs (builder created by Define itself, e.g. via FromPrefab)
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(PrefabBuilder.PrefabBuilder))
            {
                return GenerateWithProvidedBuilder(type, defineMethod);
            }

            if (parameters.Length == 0 && returnsBuilder)
            {
                return GenerateWithReturnedBuilder(type, defineMethod);
            }

            Debug.LogError(
                    $"[PrefabGenerator] {type.Name}.Define() must be either void Define(PrefabBuilder) or PrefabBuilder Define()."
                );
            return null;
        }

        private static (string asmdefName, string prefabName, string prefabPath)? GenerateWithProvidedBuilder(
            Type type,
            MethodInfo defineMethod)
        {
            var builder = new PrefabBuilder.PrefabBuilder();

            try
            {
                defineMethod.Invoke(null, new object[] { builder });

                var prefabName = builder.PrefabName;
                var prefabPath = $"{OutputFolder}/{prefabName}.prefab";
                EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
                builder.Build(prefabPath);
                AssetDatabase.ImportAsset(prefabPath);

                return (string.Empty, prefabName, prefabName);
            }
            catch
            {
                if (builder.GameObject != null)
                    Object.DestroyImmediate(builder.GameObject);
                throw;
            }
        }

        private static (string asmdefName, string prefabName, string prefabPath)? GenerateWithReturnedBuilder(
            Type type,
            MethodInfo defineMethod)
        {
            PrefabBuilder.PrefabBuilder builder = null;

            try
            {
                builder = (PrefabBuilder.PrefabBuilder)defineMethod.Invoke(null, null);

                if (builder == null)
                {
                    Debug.LogError($"[PrefabGenerator] {type.Name}.Define() returned null.");
                    return null;
                }

                var prefabName = builder.PrefabName;
                var prefabPath = $"{OutputFolder}/{prefabName}.prefab";
                EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));

                builder.Build(prefabPath);

                return (string.Empty, prefabName, prefabName);
            }
            catch
            {
                if (builder?.GameObject != null)
                    Object.DestroyImmediate(builder.GameObject);
                throw;
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parts = folderPath.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}